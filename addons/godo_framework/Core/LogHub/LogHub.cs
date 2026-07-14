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
    internal const int DebugHistoryCapacity = 64;

    private static readonly LogEntry[] _debugHistory = new LogEntry[DebugHistoryCapacity];
    private static int _debugHistoryStart;
    private static int _debugHistoryCount;
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

    internal static void Initialize()
    {
#if DEBUG
        MainThreadGuard.VerifyAccess();
        ClearDebugHistory();
#endif
    }

    internal static void Shutdown()
    {
#if DEBUG
        MainThreadGuard.VerifyAccess();
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
        GD.Print(FormatForConsole(level, message, module, context));

#if DEBUG
        int writeIndex = (_debugHistoryStart + _debugHistoryCount) % DebugHistoryCapacity;
        _debugHistory[writeIndex] = new LogEntry(DateTime.UtcNow, level, module, message, context);

        if (_debugHistoryCount < DebugHistoryCapacity)
        {
            _debugHistoryCount++;
            return;
        }

        _debugHistoryStart = (_debugHistoryStart + 1) % DebugHistoryCapacity;
#endif
    }

#if DEBUG
    private static void ClearDebugHistory()
    {
        Array.Clear(_debugHistory);
        _debugHistoryStart = 0;
        _debugHistoryCount = 0;
    }
#endif

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "未知日志等级。"),
    };
}
