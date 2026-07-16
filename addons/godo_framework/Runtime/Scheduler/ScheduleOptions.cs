using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>指定调度任务的时钟、派发阶段和可选场景 Owner。</summary>
public readonly struct ScheduleOptions
{
    /// <summary>使用游戏时间与普通 Process 阶段的默认选项。</summary>
    public static ScheduleOptions Default => default;

    /// <summary>使用不受 TimeScale 影响、但受暂停影响的普通 Process 选项。</summary>
    public static ScheduleOptions UnscaledGameTime =>
        new(ScheduleClock.UnscaledGameTime);

    /// <summary>使用不受 TimeScale 和暂停影响的普通 Process 选项。</summary>
    public static ScheduleOptions RealTime =>
        new(ScheduleClock.RealTime);

    /// <summary>任务使用的时间语义。</summary>
    public ScheduleClock Clock { get; }

    /// <summary>任务的主线程派发阶段。</summary>
    public SchedulePhase Phase { get; }

    /// <summary>可选场景 Owner；退出场景树时由 SchedulerService 取消关联任务。</summary>
    public Node? Owner { get; }

    /// <summary>创建调度选项。</summary>
    public ScheduleOptions(
        ScheduleClock clock = ScheduleClock.GameTime,
        SchedulePhase phase = SchedulePhase.Process,
        Node? owner = null)
    {
        if (!Enum.IsDefined(clock))
            throw new ArgumentOutOfRangeException(nameof(clock), clock, "未知的调度时钟。");
        if (!Enum.IsDefined(phase))
            throw new ArgumentOutOfRangeException(nameof(phase), phase, "未知的调度阶段。");

        Clock = clock;
        Phase = phase;
        Owner = owner;
    }
}
