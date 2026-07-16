#nullable enable

namespace GoDo;

/// <summary>调度任务使用的时间推进语义。</summary>
public enum ScheduleClock
{
    /// <summary>受 Engine.TimeScale 影响，并在 SceneTree 暂停时停止推进。</summary>
    GameTime = 0,

    /// <summary>不受 Engine.TimeScale 影响，但在 SceneTree 暂停时停止推进。</summary>
    UnscaledGameTime = 1,

    /// <summary>不受 Engine.TimeScale 和 SceneTree 暂停影响。</summary>
    RealTime = 2,
}
