using System;

#nullable enable

namespace GoDo;

/// <summary>仅供 Debugger 读取的普通日志快照。</summary>
internal readonly struct LogEntry
{
    public DateTime TimestampUtc { get; }
    public LogLevel Level { get; }
    public string Module { get; }
    public string Message { get; }
    public string? Context { get; }

    public LogEntry(DateTime timestampUtc, LogLevel level, string module, string message, string? context)
    {
        TimestampUtc = timestampUtc;
        Level = level;
        Module = module;
        Message = message;
        Context = context;
    }
}
