using System;

#nullable enable

namespace GoDo;

/// <summary>
/// 一条错误报告的只读数据快照。使用 struct 避免为报告容器额外分配对象。
/// </summary>
public readonly struct ErrorReport
{
    /// <summary>错误等级。</summary>
    public ErrorLevel Level { get; init; }

    /// <summary>错误来源，如框架的 "EventChannel" 或业务层的 "Game.Save"。</summary>
    public string Module { get; init; }

    /// <summary>人类可读的错误描述。</summary>
    public string Message { get; init; }

    /// <summary>
    /// 额外上下文信息，例如触发错误的节点名、事件类型名等。
    /// 可为 null 或空字符串。
    /// </summary>
    public string? Context { get; init; }

    /// <summary>原始异常对象，无异常时为 null。</summary>
    public Exception? Exception { get; init; }

    /// <summary>错误发生的 UTC 时间。</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// 调用栈字符串。
    /// 有异常时来自 <see cref="Exception.StackTrace"/>；Debug 模式下，无异常的 Fatal
    /// 报告也会主动捕获当前调用栈。
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    /// 返回适合日志输出的单行摘要字符串。
    /// </summary>
    public override string ToString()
    {
        var ctx = string.IsNullOrEmpty(Context) ? string.Empty : $" | ctx={Context}";
        return $"[{Module}] [{Level}]{ctx} {Message}";
    }
}
