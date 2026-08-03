using System;

#nullable enable

namespace GoDo;

/// <summary>表示顶层流程切换失败。</summary>
public sealed class ProcedureChangeException : Exception
{
    /// <summary>与失败阶段关联的流程诊断名称。</summary>
    public string ProcedureName { get; }

    /// <summary>失败发生时所在的流程切换阶段。</summary>
    public ProcedureChangePhase Phase { get; }

    /// <summary>
    /// 创建未指定失败阶段的流程切换异常。
    /// <para><see cref="Phase"/> 将设为 <see cref="ProcedureChangePhase.Unknown"/>。</para>
    /// </summary>
    /// <param name="procedureName">失败流程的诊断名称。</param>
    /// <param name="message">描述失败原因的消息。</param>
    /// <param name="innerException">导致流程切换失败的内部异常。</param>
    public ProcedureChangeException(string procedureName, string message, Exception? innerException = null)
        : this(procedureName, ProcedureChangePhase.Unknown, message, innerException)
    {
    }

    /// <summary>创建包含结构化失败阶段的流程切换异常。</summary>
    /// <param name="procedureName">失败流程的诊断名称。</param>
    /// <param name="phase">失败发生时所在的生命周期阶段。</param>
    /// <param name="message">描述失败原因的消息。</param>
    /// <param name="innerException">导致流程切换失败的内部异常。</param>
    public ProcedureChangeException(
        string procedureName,
        ProcedureChangePhase phase,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProcedureName = procedureName;
        Phase = phase;
    }
}
