using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

#nullable enable

namespace GoDo;

/// <summary>将普通日志与错误报告写入有界后台队列，并按文件大小滚动保存。</summary>
internal sealed class RollingFileLogWriter : IErrorReporter, IDisposable
{
    internal const long DefaultMaxFileBytes = 2 * 1024 * 1024;
    internal const int DefaultArchiveCount = 4;
    internal const int DefaultQueueCapacity = 2048;

    internal const string FileNamePrefix = "godo_framework";
    internal const string CurrentFileName = FileNamePrefix + ".log";

    private readonly string _logDirectory;
    private readonly long _maxFileBytes;
    private readonly int _archiveCount;
    private readonly BlockingCollection<string> _pendingLines;
    private readonly Thread? _worker;
    private int _disposed;
    private int _failed;
    private int _failureConsumed;
    private int _droppedLineCount;
    private int _unreportedDroppedLineCount;
    private string? _failureDetail;

    internal RollingFileLogWriter(
        string logDirectory,
        long maxFileBytes = DefaultMaxFileBytes,
        int archiveCount = DefaultArchiveCount,
        int queueCapacity = DefaultQueueCapacity,
        bool startWorker = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(archiveCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueCapacity);

        _logDirectory = logDirectory;
        _maxFileBytes = maxFileBytes;
        _archiveCount = archiveCount;
        _pendingLines = new BlockingCollection<string>(
            new ConcurrentQueue<string>(),
            queueCapacity);
        if (!startWorker)
            return;

        _worker = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "GoDo.RollingFileLog",
        };
        _worker.Start();
    }

    internal int DroppedLineCount => Volatile.Read(ref _droppedLineCount);
    internal bool HasFailed => Volatile.Read(ref _failed) != 0;

    internal bool TryConsumeFailure(out string message)
    {
        if (Volatile.Read(ref _failed) == 0 ||
            Interlocked.Exchange(ref _failureConsumed, 1) != 0)
        {
            message = string.Empty;
            return false;
        }

        message =
            $"文件日志写入失败，已停用本次运行的文件日志。目录：{_logDirectory}；" +
            $"原因：{_failureDetail ?? "未知"}";
        return true;
    }

    internal bool TryConsumeDroppedLineCount(out int count)
    {
        count = Interlocked.Exchange(ref _unreportedDroppedLineCount, 0);
        return count > 0;
    }

    internal void Write(
        DateTime timestampUtc,
        LogLevel level,
        string message,
        string module,
        string? context)
    {
        Enqueue(FormatLine(timestampUtc, LevelLabel(level), module, message, context));
    }

    public void Report(in ErrorReport report)
    {
        if (string.Equals(report.Module, "LogFile", StringComparison.Ordinal))
            return;

        string message = report.Message;
        if (report.Exception != null)
            message += $" | Exception={report.Exception.GetType().Name}";
        if (!string.IsNullOrWhiteSpace(report.StackTrace))
            message += $" | StackTrace={SingleLine(report.StackTrace)}";

        Enqueue(FormatLine(
            report.Timestamp,
            report.Level.ToString().ToUpperInvariant(),
            report.Module,
            message,
            report.Context));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _pendingLines.CompleteAdding();
        if (_worker?.IsAlive == true)
            _worker.Join(TimeSpan.FromSeconds(2));
        if (_worker?.IsAlive != true)
            _pendingLines.Dispose();
    }

    private void Enqueue(string line)
    {
        if (Volatile.Read(ref _disposed) != 0 ||
            Volatile.Read(ref _failed) != 0)
        {
            return;
        }

        if (!_pendingLines.TryAdd(line))
        {
            Interlocked.Increment(ref _droppedLineCount);
            Interlocked.Increment(ref _unreportedDroppedLineCount);
        }
    }

    private void WriteLoop()
    {
        FileStream? stream = null;
        StreamWriter? writer = null;
        try
        {
            Directory.CreateDirectory(_logDirectory);
            string currentPath = Path.Combine(_logDirectory, CurrentFileName);
            OpenWriter(currentPath, FileMode.Append, out stream, out writer);
            long currentBytes = stream.Length;

            foreach (string line in _pendingLines.GetConsumingEnumerable())
            {
                int byteCount = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                if (currentBytes > 0 && currentBytes + byteCount > _maxFileBytes)
                {
                    writer.Flush();
                    writer.Dispose();
                    stream.Dispose();
                    writer = null;
                    stream = null;
                    RotateFiles(currentPath);
                    OpenWriter(currentPath, FileMode.Create, out stream, out writer);
                    currentBytes = 0;
                }

                writer.WriteLine(line);
                currentBytes += byteCount;
            }

            writer.Flush();
        }
        catch (Exception exception)
        {
            _failureDetail = exception.Message;
            Volatile.Write(ref _failed, 1);
        }
        finally
        {
            writer?.Dispose();
            stream?.Dispose();
        }
    }

    private static void OpenWriter(
        string path,
        FileMode mode,
        out FileStream stream,
        out StreamWriter writer)
    {
        stream = new FileStream(path, mode, FileAccess.Write, FileShare.Read);
        writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private void RotateFiles(string currentPath)
    {
        if (_archiveCount == 0)
        {
            File.Delete(currentPath);
            return;
        }

        string oldestPath = ArchivePath(_archiveCount);
        if (File.Exists(oldestPath))
            File.Delete(oldestPath);

        for (int index = _archiveCount - 1; index >= 1; index--)
        {
            string sourcePath = ArchivePath(index);
            if (File.Exists(sourcePath))
                File.Move(sourcePath, ArchivePath(index + 1));
        }

        if (File.Exists(currentPath))
            File.Move(currentPath, ArchivePath(1));
    }

    private string ArchivePath(int index) =>
        Path.Combine(_logDirectory, $"{FileNamePrefix}.{index}.log");

    private static string FormatLine(
        DateTime timestampUtc,
        string level,
        string module,
        string message,
        string? context)
    {
        string head = $"{timestampUtc:O} [{module}] [{level}]";
        return string.IsNullOrWhiteSpace(context)
            ? $"{head} {SingleLine(message)}"
            : $"{head} ({SingleLine(context)}) {SingleLine(message)}";
    }

    private static string SingleLine(string value) =>
        value.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO",
        _ => "UNKNOWN",
    };
}
