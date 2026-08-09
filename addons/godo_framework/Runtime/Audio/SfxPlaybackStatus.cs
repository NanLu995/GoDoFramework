namespace GoDo;

/// <summary>受控 SFX 请求的准入与启动结果。</summary>
public enum SfxPlaybackStatus
{
    /// <summary>未产生有效结果。</summary>
    None = 0,

    /// <summary>音效已经开始播放并返回有效句柄。</summary>
    Started = 1,

    /// <summary>全局 Voice 容量已满，且请求未获准抢占。</summary>
    GlobalCapacityReached = 2,

    /// <summary>相同资源的活动与待加载请求已达到本次指定上限。</summary>
    PerKeyLimitReached = 3,

    /// <summary>请求在加载完成前被更高优先级请求取代。</summary>
    PreemptedBeforeStart = 4,

    /// <summary>3D 跟随目标在资源加载完成前已经失效或离开场景树。</summary>
    TargetUnavailableBeforeStart = 5,

    /// <summary>活动与待加载的 3D 跟随请求已经达到独立跟随上限。</summary>
    FollowCapacityReached = 6,
}
