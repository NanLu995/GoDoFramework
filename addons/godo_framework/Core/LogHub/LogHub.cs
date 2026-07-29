using System;
using System.Diagnostics;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 统一输出仅供开发诊断的普通日志。
/// <para>仅提供正常流程的 Debug 与 Info 日志；异常、降级和失败应使用 ErrorHub。</para>
/// <para>调用仅限 Godot 主线程，且在 Release 构建中会从调用点移除。</para>
/// </summary>
public static class LogHub
{
#if DEBUG
    internal const int DebugHistoryCapacity = 1000;
    internal const int ConsoleOutputLimitPerSecond = 100;

    private static readonly LogEntry[] _debugHistory = new LogEntry[DebugHistoryCapacity];
    private static int _debugHistoryStart;
    private static int _debugHistoryCount;
    private static int _debugHistoryVersion;
    private static long _consoleOutputWindowStart = Stopwatch.GetTimestamp();
    private static int _consoleOutputCount;
    private static int _suppressedConsoleOutputCount;
    private static RollingFileLogWriter? _fileWriter;

    internal static int DebugHistoryVersion => _debugHistoryVersion;
#endif

    /// <summary>输出开发期细节日志。</summary>
    [Conditional("DEBUG")]
    public static void Debug(string message, string module, string? context = null)
    {
#if DEBUG
        Write(LogLevel.Debug, message, module, context);
#endif
    }

    /// <summary>输出开发期的正常流程日志。</summary>
    [Conditional("DEBUG")]
    public static void Info(string message, string module, string? context = null)
    {
#if DEBUG
        Write(LogLevel.Info, message, module, context);
#endif
    }

    internal static void Initialize(RollingFileLogWriter? fileWriter = null)
    {
#if DEBUG
        MainThreadGuard.VerifyAccess();
        _fileWriter = fileWriter;
        ClearDebugHistory();
#endif
    }

    internal static void Shutdown()
    {
#if DEBUG
        MainThreadGuard.VerifyAccess();
        FlushSuppressedConsoleOutput();
        _fileWriter = null;
        ClearDebugHistory();
#endif
    }

    internal static LogEntry[] GetDebugSnapshot()
    {
#if DEBUG
        MainThreadGuard.VerifyAccess();

        var snapshot = new LogEntry[_debugHistoryCount];
        for (int i = 0; i < _debugHistoryCount; i++)
            snapshot[i] = _debugHistory[(_debugHistoryStart + i) % DebugHistoryCapacity];
        return snapshot;
#else
        return Array.Empty<LogEntry>();
#endif
    }

    internal static FileLogDebugSnapshot GetFileLogDebugSnapshot()
    {
#if DEBUG
        MainThreadGuard.VerifyAccess();
        return _fileWriter?.GetDebugSnapshot() ?? FileLogDebugSnapshot.Disabled;
#else
        return FileLogDebugSnapshot.Disabled;
#endif
    }

    internal static string FormatForConsole(
        LogLevel level,
        string message,
        string module,
        string? context = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

        return string.IsNullOrWhiteSpace(context)
            ? $"[{module}] [{LevelLabel(level)}] {message}"
            : $"[{module}] [{LevelLabel(level)}] ({context}) {message}";
    }

    private static void Write(LogLevel level, string message, string module, string? context)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);

#if DEBUG
        DateTime timestampUtc = DateTime.UtcNow;
        _fileWriter?.Write(timestampUtc, level, message, module, context);
        int repeatCount = 1;
        if (_debugHistoryCount > 0)
        {
            int lastIndex =
                (_debugHistoryStart + _debugHistoryCount - 1) % DebugHistoryCapacity;
            LogEntry lastEntry = _debugHistory[lastIndex];
            if (lastEntry.Matches(level, module, message, context))
            {
                LogEntry repeatedEntry = lastEntry.Repeat(timestampUtc);
                _debugHistory[lastIndex] = repeatedEntry;
                repeatCount = repeatedEntry.RepeatCount;
                IncrementDebugHistoryVersion();
                WriteConsoleOutput(level, message, module, context, repeatCount);
                return;
            }
        }

        int writeIndex = (_debugHistoryStart + _debugHistoryCount) % DebugHistoryCapacity;
        _debugHistory[writeIndex] =
            new LogEntry(timestampUtc, timestampUtc, level, module, message, context);
        IncrementDebugHistoryVersion();

        if (_debugHistoryCount < DebugHistoryCapacity)
        {
            _debugHistoryCount++;
        }
        else
        {
            _debugHistoryStart = (_debugHistoryStart + 1) % DebugHistoryCapacity;
        }

        WriteConsoleOutput(level, message, module, context, repeatCount);
#endif
    }

#if DEBUG
    private static void WriteConsoleOutput(
        LogLevel level,
        string message,
        string module,
        string? context,
        int repeatCount)
    {
        AdvanceConsoleOutputWindow();

        bool shouldPrint = repeatCount == 1 || IsPowerOfTwo(repeatCount);
        if (!shouldPrint || _consoleOutputCount >= ConsoleOutputLimitPerSecond)
        {
            _suppressedConsoleOutputCount++;
            return;
        }

        _consoleOutputCount++;
        string formatted = FormatForConsole(level, message, module, context);
        GD.Print(repeatCount == 1 ? formatted : $"{formatted} ×{repeatCount}");
    }

    private static void AdvanceConsoleOutputWindow()
    {
        long now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_consoleOutputWindowStart, now) < TimeSpan.FromSeconds(1))
            return;

        FlushSuppressedConsoleOutput();
        _consoleOutputWindowStart = now;
        _consoleOutputCount = 0;
    }

    private static void FlushSuppressedConsoleOutput()
    {
        if (_suppressedConsoleOutputCount == 0)
            return;

        GD.Print(
            $"[LogHub] [INFO] 已抑制 {_suppressedConsoleOutputCount} 条控制台输出，" +
            "完整记录仍保留在 Debugger 内存历史中");
        _suppressedConsoleOutputCount = 0;
    }

    private static bool IsPowerOfTwo(int value) =>
        value > 0 && (value & (value - 1)) == 0;

    private static void IncrementDebugHistoryVersion()
    {
        unchecked
        {
            _debugHistoryVersion++;
        }
    }

    private static void ClearDebugHistory()
    {
        Array.Clear(_debugHistory);
        _debugHistoryStart = 0;
        _debugHistoryCount = 0;
        _consoleOutputWindowStart = Stopwatch.GetTimestamp();
        _consoleOutputCount = 0;
        _suppressedConsoleOutputCount = 0;
        IncrementDebugHistoryVersion();
    }
#endif

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "未知日志等级。"),
    };
}
