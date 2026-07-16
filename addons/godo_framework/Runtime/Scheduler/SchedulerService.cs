using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>在 Godot 主线程中推进游戏、非缩放游戏与真实时间任务的长期调度服务。</summary>
public sealed partial class SchedulerService : Node, ISchedulerService
{
    private const double MicrosecondsToSeconds = 1d / 1_000_000d;

    private readonly SchedulerCore _core;
    private ulong _lastProcessTicksUsec;
    private ulong _lastPhysicsTicksUsec;
    private bool _processDispatchLimitWasHit;
    private bool _physicsDispatchLimitWasHit;

    /// <summary>创建尚未进入场景树的调度服务节点。</summary>
    public SchedulerService()
    {
        _core = new SchedulerCore(OnCallbackError);
    }

    /// <inheritdoc />
    public override void _EnterTree()
    {
        ProcessMode = ProcessModeEnum.Always;
        ulong ticksUsec = Time.GetTicksUsec();
        _lastProcessTicksUsec = ticksUsec;
        _lastPhysicsTicksUsec = ticksUsec;
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        Advance(
            SchedulePhase.Process,
            delta,
            ref _lastProcessTicksUsec,
            ref _processDispatchLimitWasHit);
    }

    /// <inheritdoc />
    public override void _PhysicsProcess(double delta)
    {
        Advance(
            SchedulePhase.Physics,
            delta,
            ref _lastPhysicsTicksUsec,
            ref _physicsDispatchLimitWasHit);
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _core.Shutdown();
        _processDispatchLimitWasHit = false;
        _physicsDispatchLimitWasHit = false;
    }

    /// <inheritdoc />
    public ScheduleHandle Schedule(
        double delaySeconds,
        Action callback,
        ScheduleOptions options = default)
    {
        VerifyAccess();
        return _core.Schedule(delaySeconds, callback, options);
    }

    /// <inheritdoc />
    public ScheduleHandle ScheduleRepeating(
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default)
    {
        VerifyAccess();
        return _core.ScheduleRepeating(intervalSeconds, callback, options);
    }

    /// <inheritdoc />
    public ScheduleHandle ScheduleRepeating(
        double initialDelaySeconds,
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default)
    {
        VerifyAccess();
        return _core.ScheduleRepeating(
            initialDelaySeconds,
            intervalSeconds,
            callback,
            options);
    }

    /// <inheritdoc />
    public Task DelayAsync(
        double delaySeconds,
        ScheduleOptions options = default,
        CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        return _core.DelayAsync(delaySeconds, options, cancellationToken);
    }

    /// <inheritdoc />
    public bool Cancel(ScheduleHandle handle)
    {
        VerifyAccess();
        return _core.Cancel(handle);
    }

    /// <inheritdoc />
    public bool Pause(ScheduleHandle handle)
    {
        VerifyAccess();
        return _core.Pause(handle);
    }

    /// <inheritdoc />
    public bool Resume(ScheduleHandle handle)
    {
        VerifyAccess();
        return _core.Resume(handle);
    }

    /// <inheritdoc />
    public bool IsScheduled(ScheduleHandle handle)
    {
        VerifyAccess();
        return _core.IsScheduled(handle);
    }

    /// <inheritdoc />
    public bool TryGetRemainingSeconds(ScheduleHandle handle, out double remainingSeconds)
    {
        VerifyAccess();
        return _core.TryGetRemainingSeconds(handle, out remainingSeconds);
    }

    /// <summary>取消全部任务并永久关闭当前服务实例。</summary>
    internal void Shutdown()
    {
        MainThreadGuard.VerifyAccess();
        _core.Shutdown();
    }

#if DEBUG
    /// <summary>返回当前调度状态的 Debug-only 只读快照。</summary>
    internal SchedulerDebugSnapshot GetDebugSnapshot()
    {
        VerifyAccess();
        return _core.GetDebugSnapshot();
    }
#endif

    private void Advance(
        SchedulePhase phase,
        double scaledDeltaSeconds,
        ref ulong previousTicksUsec,
        ref bool dispatchLimitWasHit)
    {
        ulong currentTicksUsec = Time.GetTicksUsec();
        double unscaledDeltaSeconds =
            unchecked(currentTicksUsec - previousTicksUsec) * MicrosecondsToSeconds;
        previousTicksUsec = currentTicksUsec;

        _core.Advance(
            phase,
            scaledDeltaSeconds,
            unscaledDeltaSeconds,
            GetTree().Paused);

        bool limitWasHit = _core.LastAdvanceHitDispatchLimit;
        if (limitWasHit && !dispatchLimitWasHit)
        {
            ErrorHub.Warn(
                "单轮 Scheduler 回调达到派发上限，其余到期任务将延后到下一轮",
                "Scheduler",
                $"Phase={phase}");
        }

        dispatchLimitWasHit = limitWasHit;
    }

    private void VerifyAccess()
    {
        MainThreadGuard.VerifyAccess();
        if (!IsInsideTree())
            throw new InvalidOperationException("SchedulerService 必须进入场景树后才能使用。");
    }

    private static void OnCallbackError(SchedulerCallbackError error)
    {
        ErrorHub.Report(
            error.Exception,
            "Scheduler",
            $"Handle={error.Handle}; Clock={error.Clock}; Phase={error.Phase}; Repeating={error.IsRepeating}");
    }
}
