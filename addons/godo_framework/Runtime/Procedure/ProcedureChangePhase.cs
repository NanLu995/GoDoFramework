#nullable enable

namespace GoDo;

/// <summary>标识顶层流程切换失败时所在的生命周期阶段。</summary>
public enum ProcedureChangePhase
{
    /// <summary>异常由旧版构造函数或无法确定阶段的外部代码创建。</summary>
    Unknown,

    /// <summary>切换请求在进入生命周期方法前被拒绝。</summary>
    Requesting,

    /// <summary>正在退出当前流程。</summary>
    Exiting,

    /// <summary>当前流程已经退出，但其激活资源清理失败。</summary>
    Cleanup,

    /// <summary>正在进入目标流程，包括进入失败后的组合清理错误。</summary>
    Entering,
}
