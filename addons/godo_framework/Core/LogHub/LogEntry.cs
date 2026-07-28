using System;

#nullable enable

namespace GoDo;

/// <summary>仅供 Debugger 读取的普通日志快照。</summary>
internal readonly struct LogEntry
{
    public DateTime FirstTimestampUtc { get; }
    public DateTime TimestampUtc { get; }
    public LogLevel Level { get; }
    public string Module { get; }
    public string Message { get; }
    public string? Context { get; }
    public int RepeatCount { get; }

    public LogEntry(
        DateTime firstTimestampUtc,
        DateTime timestampUtc,
        LogLevel level,
        string module,
        string message,
        string? context,
        int repeatCount = 1)
    {
        FirstTimestampUtc = firstTimestampUtc;
        TimestampUtc = timestampUtc;
        Level = level;
        Module = module;
        Message = message;
        Context = context;
        RepeatCount = repeatCount;
    }

    public bool Matches(LogLevel level, string module, string message, string? context) =>
        Level == level &&
        string.Equals(Module, module, StringComparison.Ordinal) &&
        string.Equals(Message, message, StringComparison.Ordinal) &&
        string.Equals(Context, context, StringComparison.Ordinal);

    public LogEntry Repeat(DateTime timestampUtc) =>
        new(
            FirstTimestampUtc,
            timestampUtc,
            Level,
            Module,
            Message,
            Context,
            RepeatCount == int.MaxValue ? int.MaxValue : RepeatCount + 1);
}
