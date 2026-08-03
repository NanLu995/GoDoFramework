using System;
using System.Diagnostics;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 统一的日志调用入口。
/// <para>Debug 与 Info 用于正常流程的开发诊断，仅限 Godot 主线程，并在 Release 构建中从调用点移除。</para>
/// <para>Warning、Error 与 Fatal 复用 ErrorHub 的结构化报告和 Reporter 管线。</para>
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

    /// <summary>
    /// 创建绑定固定模块名的轻量日志通道，减少同一类型内重复传递模块参数。
    /// </summary>
    /// <param name="module">稳定的来源模块名称；不能为 null、空或全空白。</param>
    /// <returns>不分配托管对象、可复用的只读日志通道。</returns>
    /// <exception cref="ArgumentException"><paramref name="module"/> 为空或全空白。</exception>
    public static LogChannel For(string module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        return new LogChannel(module);
    }

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

    /// <summary>
    /// 上报可恢复的异常情况或降级结果。
    /// <para>该调用复用 ErrorHub，在 Release 构建中仍然保留。</para>
    /// </summary>
    /// <param name="message">可读的降级描述；不能为 null、空或全空白。</param>
    /// <param name="module">稳定的来源模块名称；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> 或 <paramref name="module"/> 为空或全空白。</exception>
    public static void Warn(string message, string module, string? context = null)
    {
        ValidateReportArguments(message, module);
        ErrorHub.Warn(message, module, context);
    }

    /// <summary>
    /// 上报没有关联异常对象的当前操作失败。
    /// <para>该调用复用 ErrorHub，在 Release 构建中仍然保留。</para>
    /// </summary>
    /// <param name="message">可读的失败描述；不能为 null、空或全空白。</param>
    /// <param name="module">稳定的来源模块名称；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> 或 <paramref name="module"/> 为空或全空白。</exception>
    public static void Error(string message, string module, string? context = null)
    {
        ValidateReportArguments(message, module);
        ErrorHub.Report(ErrorLevel.Error, message, module, context);
    }

    /// <summary>
    /// 上报带有原始异常对象的当前操作失败。
    /// <para>该调用复用 ErrorHub，在 Release 构建中仍然保留。</para>
    /// </summary>
    /// <param name="exception">需要保留的原始异常对象。</param>
    /// <param name="module">稳定的来源模块名称；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> 为 null。</exception>
    /// <exception cref="ArgumentException"><paramref name="module"/> 为空或全空白。</exception>
    public static void Error(Exception exception, string module, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ErrorHub.Report(exception, module, context);
    }

    /// <summary>
    /// 上报没有关联异常对象的最高严重等级错误。
    /// <para>Fatal 不会主动退出游戏；恢复或退出策略仍由调用边界决定。</para>
    /// </summary>
    /// <param name="message">可读的致命错误描述；不能为 null、空或全空白。</param>
    /// <param name="module">稳定的来源模块名称；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentException"><paramref name="message"/> 或 <paramref name="module"/> 为空或全空白。</exception>
    public static void Fatal(string message, string module, string? context = null)
    {
        ValidateReportArguments(message, module);
        ErrorHub.Fatal(message, module, context);
    }

    /// <summary>
    /// 上报带有原始异常对象的最高严重等级错误。
    /// <para>Fatal 不会主动退出游戏；恢复或退出策略仍由调用边界决定。</para>
    /// </summary>
    /// <param name="exception">需要保留的原始异常对象。</param>
    /// <param name="module">稳定的来源模块名称；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> 为 null。</exception>
    /// <exception cref="ArgumentException"><paramref name="module"/> 为空或全空白。</exception>
    public static void Fatal(Exception exception, string module, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ErrorHub.Fatal(exception, module, context);
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

#if DEBUG
    internal static void AddDebugHistoryEntryForTesting(
        string message,
        string module,
        string? context = null)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        RecordDebugHistory(DateTime.UtcNow, LogLevel.Debug, message, module, context);
    }
#endif

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
        int repeatCount = RecordDebugHistory(timestampUtc, level, message, module, context);
        WriteConsoleOutput(level, message, module, context, repeatCount);
#endif
    }

    private static void ValidateReportArguments(string message, string module)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
    }

#if DEBUG
    private static int RecordDebugHistory(
        DateTime timestampUtc,
        LogLevel level,
        string message,
        string module,
        string? context)
    {
        if (_debugHistoryCount > 0)
        {
            int lastIndex =
                (_debugHistoryStart + _debugHistoryCount - 1) % DebugHistoryCapacity;
            LogEntry lastEntry = _debugHistory[lastIndex];
            if (lastEntry.Matches(level, module, message, context))
            {
                LogEntry repeatedEntry = lastEntry.Repeat(timestampUtc);
                _debugHistory[lastIndex] = repeatedEntry;
                IncrementDebugHistoryVersion();
                return repeatedEntry.RepeatCount;
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

        return 1;
    }

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
