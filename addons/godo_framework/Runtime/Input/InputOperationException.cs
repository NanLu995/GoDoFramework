using System;

#nullable enable

namespace GoDo;

/// <summary>输入后端、采样、Context 或 Action 操作失败。</summary>
public sealed class InputOperationException : Exception
{
    /// <summary>创建输入操作异常。</summary>
    /// <param name="message">描述失败操作与原因的消息。</param>
    public InputOperationException(string message)
        : base(message)
    {
    }

    /// <summary>创建包含底层原因的输入操作异常。</summary>
    /// <param name="message">描述失败操作与边界语义的消息。</param>
    /// <param name="innerException">输入后端、资源或信号处理抛出的底层异常。</param>
    public InputOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
