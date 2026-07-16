#nullable enable

namespace GoDo;

/// <summary>调度任务的主线程派发阶段。</summary>
public enum SchedulePhase
{
    /// <summary>在 Scheduler 的普通 Process 更新中派发。</summary>
    Process = 0,

    /// <summary>在 Scheduler 的 Physics Process 更新中派发。</summary>
    Physics = 1,
}
