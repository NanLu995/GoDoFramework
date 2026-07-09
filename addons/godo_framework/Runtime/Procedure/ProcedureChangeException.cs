using System;

#nullable enable

namespace GoDo;

/// <summary>表示顶层流程切换失败。</summary>
public sealed class ProcedureChangeException : Exception
{
    /// <summary>正在退出或进入的流程名称。</summary>
    public string ProcedureName { get; }

    /// <summary>创建流程切换异常。</summary>
    public ProcedureChangeException(string procedureName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProcedureName = procedureName;
    }
}
