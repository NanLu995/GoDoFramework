using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>Scheduler 人工时钟、阶段隔离和确定性派发顺序的无交互回归入口。</summary>
public sealed partial class SchedulerCoreRegression : Node
{
#if DEBUG
    private const int ExpectedPassCount = 26;
#else
    private const int ExpectedPassCount = 25;
#endif

    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("默认选项语义", VerifyDefaultOptions);
            Run("TimeScale 区分游戏与非缩放时钟", VerifyTimeScaleBehavior);
            Run("暂停只推进真实时钟", VerifyPauseBehavior);
            Run("Process 与 Physics 隔离", VerifyPhaseIsolation);
            Run("零延迟不在调用栈内执行", VerifyZeroDelay);
            Run("回调创建的零延迟任务延后到下一轮", VerifyReentrantZeroDelay);
            Run("同到期时间保持创建顺序", VerifyStableOrdering);
            Run("非法时间参数被拒绝", VerifyInvalidTimeRejection);
            Run("重复任务按固定间隔执行", VerifyRepeatingInterval);
            Run("重复任务支持独立初始延迟", VerifyRepeatingInitialDelay);
            Run("卡帧遗漏周期合并为一次", VerifyRepeatingCoalescesMissedIntervals);
            Run("任务支持取消与自取消", VerifyCancellation);
            Run("独立暂停保存并恢复剩余时间", VerifyPauseAndResume);
            Run("重复任务可在回调中暂停自身", VerifyRepeatingSelfPause);
            Run("回调异常被隔离并取消任务", VerifyCallbackExceptionIsolation);
            Run("单轮派发上限保留剩余任务", VerifyDispatchLimit);
            Run("大量取消触发失效队列压缩", VerifyStaleQueueCompaction);
            Run("DelayAsync 按人工时钟完成", VerifyDelayAsyncCompletion);
            Run("DelayAsync continuation 保持主线程", VerifyDelayContinuationThread);
            Run("后台 Token 取消在下一更新生效", VerifyBackgroundCancellation);
            Run("预取消 Token 不创建任务", VerifyPreCanceledDelay);
            Run("Owner 必须已经位于场景树", VerifyOwnerMustBeInsideTree);
            Run("同一 Owner 复用绑定并在任务结束后解绑", VerifyOwnerBindingCleanup);
            Run("Owner 退出场景树取消关联任务", VerifyOwnerExitCancellation);
#if DEBUG
            Run("Debug 快照汇总状态与取消原因", VerifyDebugSnapshot);
#endif
            Run("Shutdown 取消等待并拒绝新任务", VerifyShutdown);

            GD.Print($"[SchedulerCoreRegression] PASS ({_passed}/{ExpectedPassCount})");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SchedulerCoreRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[SchedulerCoreRegression] PASS: {name}");
    }

    private static void VerifyDefaultOptions()
    {
        ScheduleOptions options = default;
        Assert(options.Clock == ScheduleClock.GameTime, "默认时钟不是 GameTime");
        Assert(options.Phase == SchedulePhase.Process, "默认阶段不是 Process");
        Assert(options.Owner is null, "默认 Owner 不是 null");
        Assert(!default(ScheduleHandle).IsValid, "默认句柄不应有效");
    }

    private static void VerifyTimeScaleBehavior()
    {
        var core = new SchedulerCore();
        int gameCount = 0;
        int unscaledCount = 0;
        int realCount = 0;

        core.Schedule(1d, () => gameCount++);
        core.Schedule(
            1d,
            () => unscaledCount++,
            new ScheduleOptions(ScheduleClock.UnscaledGameTime));
        core.Schedule(1d, () => realCount++, ScheduleOptions.RealTime);

        int firstDispatch = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 0.5d,
            unscaledDeltaSeconds: 1d,
            isTreePaused: false);

        Assert(firstDispatch == 2, "非缩放时钟没有按真实 delta 到期");
        Assert(gameCount == 0, "GameTime 错误忽略了缩放 delta");
        Assert(unscaledCount == 1, "UnscaledGameTime 没有到期");
        Assert(realCount == 1, "RealTime 没有到期");

        int secondDispatch = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 0.5d,
            unscaledDeltaSeconds: 0d,
            isTreePaused: false);

        Assert(secondDispatch == 1 && gameCount == 1, "GameTime 累计缩放 delta 后没有到期");
    }

    private static void VerifyPauseBehavior()
    {
        var core = new SchedulerCore();
        int gameCount = 0;
        int unscaledCount = 0;
        int realCount = 0;

        core.Schedule(1d, () => gameCount++);
        core.Schedule(
            1d,
            () => unscaledCount++,
            new ScheduleOptions(ScheduleClock.UnscaledGameTime));
        core.Schedule(1d, () => realCount++, ScheduleOptions.RealTime);

        int pausedDispatch = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 1d,
            unscaledDeltaSeconds: 1d,
            isTreePaused: true);

        Assert(pausedDispatch == 1, "暂停期间不应派发两个游戏时钟");
        Assert(gameCount == 0 && unscaledCount == 0, "暂停期间游戏时钟错误推进");
        Assert(realCount == 1, "暂停期间 RealTime 没有推进");

        int resumedDispatch = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 1d,
            unscaledDeltaSeconds: 1d,
            isTreePaused: false);

        Assert(resumedDispatch == 2, "恢复后游戏时钟任务没有到期");
        Assert(gameCount == 1 && unscaledCount == 1, "恢复后的游戏时钟结果不正确");
    }

    private static void VerifyPhaseIsolation()
    {
        var core = new SchedulerCore();
        int processCount = 0;
        int physicsCount = 0;

        core.Schedule(1d, () => processCount++);
        core.Schedule(
            1d,
            () => physicsCount++,
            new ScheduleOptions(phase: SchedulePhase.Physics));

        int processDispatch = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 1d,
            unscaledDeltaSeconds: 1d,
            isTreePaused: false);

        Assert(processDispatch == 1 && processCount == 1, "Process 任务没有到期");
        Assert(physicsCount == 0, "Process 更新错误派发了 Physics 任务");
        Assert(core.GetCurrentTime(ScheduleClock.GameTime, SchedulePhase.Physics) == 0d,
            "Process 更新错误推进了 Physics 时钟");

        int physicsDispatch = core.Advance(
            SchedulePhase.Physics,
            scaledDeltaSeconds: 1d,
            unscaledDeltaSeconds: 1d,
            isTreePaused: false);

        Assert(physicsDispatch == 1 && physicsCount == 1, "Physics 任务没有独立到期");
    }

    private static void VerifyZeroDelay()
    {
        var core = new SchedulerCore();
        int callbackCount = 0;
        ScheduleHandle handle = core.Schedule(0d, () => callbackCount++);

        Assert(handle.IsValid, "Scheduler 返回了无效句柄");
        Assert(callbackCount == 0, "零延迟任务在 Schedule 调用栈内同步执行");

        int dispatched = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 0d,
            unscaledDeltaSeconds: 0d,
            isTreePaused: false);

        Assert(dispatched == 1 && callbackCount == 1, "零延迟任务没有在下一轮执行");
    }

    private static void VerifyReentrantZeroDelay()
    {
        var core = new SchedulerCore();
        int callbackCount = 0;

        core.Schedule(0d, () =>
        {
            callbackCount++;
            core.Schedule(0d, () => callbackCount++);
        });

        int firstDispatch = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 0d,
            unscaledDeltaSeconds: 0d,
            isTreePaused: false);

        Assert(firstDispatch == 1 && callbackCount == 1,
            "回调中新建的零延迟任务在同一轮发生重入");

        int secondDispatch = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 0d,
            unscaledDeltaSeconds: 0d,
            isTreePaused: false);

        Assert(secondDispatch == 1 && callbackCount == 2,
            "回调中新建的零延迟任务没有在下一轮执行");
    }

    private static void VerifyStableOrdering()
    {
        var core = new SchedulerCore();
        var order = new List<int>();

        core.Schedule(1d, () => order.Add(1));
        core.Schedule(1d, () => order.Add(2));
        core.Schedule(1d, () => order.Add(3));

        int dispatched = core.Advance(
            SchedulePhase.Process,
            scaledDeltaSeconds: 1d,
            unscaledDeltaSeconds: 1d,
            isTreePaused: false);

        Assert(dispatched == 3, "同到期时间任务没有全部派发");
        Assert(order.Count == 3 && order[0] == 1 && order[1] == 2 && order[2] == 3,
            "同到期时间没有保持创建顺序");
    }

    private static void VerifyInvalidTimeRejection()
    {
        var core = new SchedulerCore();
        AssertThrows<ArgumentOutOfRangeException>(
            () => core.Schedule(-0.01d, static () => { }),
            "负延迟没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => core.Schedule(double.NaN, static () => { }),
            "NaN 延迟没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => core.Advance(SchedulePhase.Process, double.PositiveInfinity, 0d, false),
            "无穷 delta 没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => core.ScheduleRepeating(0d, static () => { }),
            "零重复间隔没有被拒绝");
    }

    private static void VerifyRepeatingInterval()
    {
        var core = new SchedulerCore();
        int callbackCount = 0;
        ScheduleHandle handle = core.ScheduleRepeating(1d, () => callbackCount++);

        Assert(core.Advance(SchedulePhase.Process, 0.5d, 0.5d, false) == 0,
            "重复任务在完整间隔前执行");
        Assert(core.Advance(SchedulePhase.Process, 0.5d, 0.5d, false) == 1,
            "重复任务首次间隔到期后没有执行");
        Assert(core.Advance(SchedulePhase.Process, 0.9d, 0.9d, false) == 0,
            "重复任务第二个间隔提前执行");
        Assert(core.Advance(SchedulePhase.Process, 0.1d, 0.1d, false) == 1,
            "重复任务第二个间隔没有执行");
        Assert(callbackCount == 2 && core.IsScheduled(handle), "重复任务状态不正确");
    }

    private static void VerifyRepeatingInitialDelay()
    {
        var core = new SchedulerCore();
        int callbackCount = 0;
        core.ScheduleRepeating(0.25d, 1d, () => callbackCount++);

        Assert(core.Advance(SchedulePhase.Process, 0.25d, 0.25d, false) == 1,
            "独立初始延迟没有生效");
        Assert(core.Advance(SchedulePhase.Process, 0.99d, 0.99d, false) == 0,
            "初始回调后的重复间隔提前执行");
        Assert(core.Advance(SchedulePhase.Process, 0.01d, 0.01d, false) == 1,
            "初始回调后的重复间隔没有执行");
        Assert(callbackCount == 2, "独立初始延迟的回调次数不正确");
    }

    private static void VerifyRepeatingCoalescesMissedIntervals()
    {
        var core = new SchedulerCore();
        int callbackCount = 0;
        ScheduleHandle handle = core.ScheduleRepeating(1d, () => callbackCount++);

        int dispatched = core.Advance(SchedulePhase.Process, 3.4d, 3.4d, false);
        Assert(dispatched == 1 && callbackCount == 1, "卡帧后重复任务发生追赶式爆发");
        Assert(core.TryGetRemainingSeconds(handle, out double remaining), "无法查询重复任务剩余时间");
        AssertApproximately(remaining, 0.6d, "卡帧合并后的理论节奏不正确");

        Assert(core.Advance(SchedulePhase.Process, 0.5d, 0.5d, false) == 0,
            "卡帧合并后的任务提前执行");
        Assert(core.Advance(SchedulePhase.Process, 0.1d, 0.1d, false) == 1,
            "卡帧合并后的下一个理论周期没有执行");
    }

    private static void VerifyCancellation()
    {
        var core = new SchedulerCore();
        int oneShotCount = 0;
        ScheduleHandle oneShot = core.Schedule(1d, () => oneShotCount++);

        Assert(core.Cancel(oneShot), "活动任务取消返回 false");
        Assert(!core.Cancel(oneShot), "重复取消已结束任务不应成功");
        Assert(!core.IsScheduled(oneShot), "取消后任务仍显示为活动");
        Assert(core.Advance(SchedulePhase.Process, 1d, 1d, false) == 0 && oneShotCount == 0,
            "取消后一次性任务仍被执行");

        int repeatingCount = 0;
        ScheduleHandle repeating = default;
        repeating = core.ScheduleRepeating(0d, 1d, () =>
        {
            repeatingCount++;
            Assert(core.Cancel(repeating), "重复任务不能在回调中取消自身");
        });

        Assert(core.Advance(SchedulePhase.Process, 0d, 0d, false) == 1,
            "自取消重复任务首次没有执行");
        Assert(core.Advance(SchedulePhase.Process, 2d, 2d, false) == 0,
            "自取消重复任务被重新入队");
        Assert(repeatingCount == 1, "自取消重复任务执行次数不正确");
    }

    private static void VerifyPauseAndResume()
    {
        var core = new SchedulerCore();
        int callbackCount = 0;
        ScheduleHandle handle = core.Schedule(2d, () => callbackCount++);

        core.Advance(SchedulePhase.Process, 0.5d, 0.5d, false);
        Assert(core.Pause(handle), "活动任务暂停返回 false");
        Assert(!core.Pause(handle), "重复暂停不应成功");
        Assert(core.TryGetRemainingSeconds(handle, out double pausedRemaining),
            "无法查询暂停任务剩余时间");
        AssertApproximately(pausedRemaining, 1.5d, "暂停保存的剩余时间不正确");

        core.Advance(SchedulePhase.Process, 10d, 10d, false);
        Assert(callbackCount == 0, "独立暂停任务仍随时钟推进执行");
        Assert(core.TryGetRemainingSeconds(handle, out double unchangedRemaining),
            "推进后无法查询暂停任务");
        AssertApproximately(unchangedRemaining, 1.5d, "暂停期间剩余时间发生变化");

        Assert(core.Resume(handle), "暂停任务恢复返回 false");
        Assert(!core.Resume(handle), "重复恢复不应成功");
        Assert(core.Advance(SchedulePhase.Process, 1.49d, 1.49d, false) == 0,
            "恢复任务提前执行");
        Assert(core.Advance(SchedulePhase.Process, 0.01d, 0.01d, false) == 1,
            "恢复任务没有按保存的剩余时间执行");
        Assert(callbackCount == 1 && !core.IsScheduled(handle), "恢复后一次性任务状态不正确");
    }

    private static void VerifyRepeatingSelfPause()
    {
        var core = new SchedulerCore();
        int callbackCount = 0;
        ScheduleHandle handle = default;
        handle = core.ScheduleRepeating(0d, 1d, () =>
        {
            callbackCount++;
            if (callbackCount == 1)
                Assert(core.Pause(handle), "重复任务不能在回调中暂停自身");
        });

        Assert(core.Advance(SchedulePhase.Process, 0d, 0d, false) == 1,
            "自暂停重复任务首次没有执行");
        Assert(core.TryGetRemainingSeconds(handle, out double remaining),
            "无法查询自暂停重复任务");
        AssertApproximately(remaining, 1d, "自暂停重复任务没有保存完整下次间隔");
        Assert(core.Advance(SchedulePhase.Process, 5d, 5d, false) == 0,
            "自暂停重复任务仍继续执行");

        Assert(core.Resume(handle), "自暂停重复任务无法恢复");
        Assert(core.Advance(SchedulePhase.Process, 1d, 1d, false) == 1,
            "自暂停重复任务恢复后没有执行");
        Assert(callbackCount == 2 && core.IsScheduled(handle), "自暂停重复任务恢复状态不正确");
    }

    private static void VerifyCallbackExceptionIsolation()
    {
        var errors = new List<SchedulerCallbackError>();
        var core = new SchedulerCore(errors.Add);
        int successfulCount = 0;

        core.Schedule(0d, static () => throw new InvalidOperationException("one-shot"));
        core.Schedule(0d, () => successfulCount++);
        ScheduleHandle repeating = core.ScheduleRepeating(
            0d,
            1d,
            static () => throw new InvalidOperationException("repeating"));

        int dispatched = core.Advance(SchedulePhase.Process, 0d, 0d, false);
        Assert(dispatched == 3 && successfulCount == 1, "异常回调中断了其他到期任务");
        Assert(errors.Count == 2, "回调异常没有完整报告");
        Assert(!errors[0].IsRepeating && errors[1].IsRepeating, "异常上下文的重复标志不正确");
        Assert(!core.IsScheduled(repeating), "发生异常的重复任务没有取消");
        Assert(core.ActiveCount == 0, "异常派发后仍残留活动任务");
    }

    private static void VerifyDispatchLimit()
    {
        var core = new SchedulerCore(maxCallbacksPerAdvance: 2);
        int callbackCount = 0;
        core.Schedule(0d, () => callbackCount++);
        core.Schedule(0d, () => callbackCount++);
        core.Schedule(0d, () => callbackCount++);

        Assert(core.Advance(SchedulePhase.Process, 0d, 0d, false) == 2,
            "首轮没有遵守派发上限");
        Assert(core.LastAdvanceHitDispatchLimit, "达到上限且仍有到期任务时没有记录状态");
        Assert(callbackCount == 2 && core.ActiveCount == 1, "派发上限后的任务状态不正确");

        Assert(core.Advance(SchedulePhase.Process, 0d, 0d, false) == 1,
            "下一轮没有执行被保留的到期任务");
        Assert(!core.LastAdvanceHitDispatchLimit, "队列清空后仍报告派发上限");
        Assert(callbackCount == 3 && core.ActiveCount == 0, "派发上限最终结果不正确");
    }

    private static void VerifyStaleQueueCompaction()
    {
        var core = new SchedulerCore();
        var handles = new List<ScheduleHandle>(130);
        for (int index = 0; index < 130; index++)
            handles.Add(core.Schedule(100d, static () => { }));

        for (int index = 0; index < handles.Count; index++)
            Assert(core.Cancel(handles[index]), "批量取消活动任务失败");

        Assert(core.ActiveCount == 0, "批量取消后仍有活动任务");
        Assert(core.GetQueuedItemCount(ScheduleClock.GameTime, SchedulePhase.Process) < 64,
            "失效队列没有按阈值压缩");
        Assert(core.Advance(SchedulePhase.Process, 100d, 100d, false) == 0,
            "失效队列项被错误执行");
        Assert(core.GetQueuedItemCount(ScheduleClock.GameTime, SchedulePhase.Process) == 0,
            "推进后失效队列没有清空");
    }

    private static void VerifyDelayAsyncCompletion()
    {
        var core = new SchedulerCore();
        Task delay = core.DelayAsync(1d);

        Assert(!delay.IsCompleted, "DelayAsync 在创建时同步完成");
        Assert(core.ActiveCount == 1, "DelayAsync 没有注册活动任务");
        Assert(core.Advance(SchedulePhase.Process, 0.5d, 0.5d, false) == 0,
            "DelayAsync 提前完成");
        Assert(!delay.IsCompleted, "DelayAsync 在不足延迟时完成");
        Assert(core.Advance(SchedulePhase.Process, 0.5d, 0.5d, false) == 1,
            "DelayAsync 到期时没有计入派发");
        Assert(delay.IsCompletedSuccessfully, "DelayAsync 到期后没有成功完成");
        Assert(core.ActiveCount == 0, "DelayAsync 完成后仍保留活动任务");
    }

    private static void VerifyDelayContinuationThread()
    {
        var core = new SchedulerCore();
        int mainThreadId = System.Environment.CurrentManagedThreadId;
        int continuationThreadId = 0;
        Task delay = core.DelayAsync(0d, ScheduleOptions.RealTime);
        delay.GetAwaiter().OnCompleted(() =>
            continuationThreadId = System.Environment.CurrentManagedThreadId);

        core.Advance(SchedulePhase.Process, 0d, 0d, false);

        Assert(delay.IsCompletedSuccessfully, "零延迟异步等待没有完成");
        Assert(continuationThreadId == mainThreadId,
            "DelayAsync continuation 没有在 Scheduler 主线程调用链完成");
    }

    private static void VerifyBackgroundCancellation()
    {
        var core = new SchedulerCore();
        using var cancellation = new CancellationTokenSource();
        Task delay = core.DelayAsync(10d, cancellationToken: cancellation.Token);
        var thread = new Thread(cancellation.Cancel);
        thread.Start();
        thread.Join();

        Assert(!delay.IsCompleted, "后台 Token 在 Scheduler 更新前直接完成了 Task");
        Assert(core.PendingCancellationCount == 1, "后台 Token 没有进入取消请求队列");

        core.Advance(SchedulePhase.Physics, 0d, 0d, false);

        Assert(delay.IsCanceled, "下一次 Scheduler 更新没有完成取消");
        Assert(core.PendingCancellationCount == 0, "取消请求队列没有排空");
        Assert(core.ActiveCount == 0, "Token 取消后仍保留活动任务");
        try
        {
            delay.GetAwaiter().GetResult();
            throw new InvalidOperationException("取消后的 DelayAsync 没有抛出取消异常");
        }
        catch (TaskCanceledException exception)
        {
            Assert(exception.CancellationToken == cancellation.Token,
                "DelayAsync 取消没有保留原 CancellationToken");
        }
    }

    private static void VerifyPreCanceledDelay()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var core = new SchedulerCore();

        Task delay = core.DelayAsync(1d, cancellationToken: cancellation.Token);

        Assert(delay.IsCanceled, "预取消 Token 没有立即返回取消 Task");
        Assert(core.ActiveCount == 0, "预取消 DelayAsync 仍创建了调度任务");
        Assert(core.PendingCancellationCount == 0, "预取消 Token 错误进入请求队列");
    }

    private static void VerifyOwnerMustBeInsideTree()
    {
        var core = new SchedulerCore();
        var owner = new Node();

        AssertThrows<InvalidOperationException>(
            () => core.Schedule(1d, static () => { }, new ScheduleOptions(owner: owner)),
            "不在场景树中的 Owner 仍可创建任务");
        Assert(core.ActiveCount == 0, "非法 Owner 创建失败后仍残留任务");
        Assert(core.OwnerCount == 0, "非法 Owner 创建失败后仍残留生命周期绑定");
        owner.Free();
    }

    private void VerifyOwnerBindingCleanup()
    {
        var core = new SchedulerCore();
        var owner = new Node();
        AddChild(owner);
        ScheduleOptions options = new(owner: owner);

        ScheduleHandle canceled = core.Schedule(10d, static () => { }, options);
        core.Schedule(1d, static () => { }, options);

        Assert(core.OwnerCount == 1, "同一 Owner 被重复建立生命周期绑定");
        Assert(core.OwnedHandleCount == 2, "Owner 没有登记全部关联任务");
        Assert(core.Cancel(canceled), "Owner 关联任务取消失败");
        Assert(core.OwnerCount == 1 && core.OwnedHandleCount == 1,
            "部分任务结束时错误移除 Owner 绑定");

        core.Advance(SchedulePhase.Process, 1d, 1d, false);

        Assert(core.OwnerCount == 0 && core.OwnedHandleCount == 0,
            "最后一个任务自然结束后没有移除 Owner 绑定");
        RemoveChild(owner);
        owner.Free();
    }

    private void VerifyOwnerExitCancellation()
    {
        var core = new SchedulerCore();
        var owner = new Node();
        AddChild(owner);
        ScheduleOptions options = new(owner: owner);
        int callbackCount = 0;

        core.Schedule(1d, () => callbackCount++, options);
        core.ScheduleRepeating(1d, () => callbackCount++, options);
        Task delay = core.DelayAsync(1d, options);

        RemoveChild(owner);

        Assert(core.ActiveCount == 0, "Owner 退出后仍有活动任务");
        Assert(core.OwnerCount == 0 && core.OwnedHandleCount == 0,
            "Owner 退出后仍残留生命周期登记");
        Assert(delay.IsCanceled, "Owner 退出没有取消关联 DelayAsync");
        core.Advance(SchedulePhase.Process, 1d, 1d, false);
        Assert(callbackCount == 0, "Owner 退出后关联回调仍被执行");
        owner.Free();
    }

#if DEBUG
    private void VerifyDebugSnapshot()
    {
        var core = new SchedulerCore(static _ => { });
        ScheduleHandle game = core.Schedule(5d, static () => { });
        core.ScheduleRepeating(
            4d,
            static () => { },
            ScheduleOptions.UnscaledGameTime);
        ScheduleHandle pausedPhysics = core.Schedule(
            3d,
            static () => { },
            new ScheduleOptions(
                ScheduleClock.RealTime,
                SchedulePhase.Physics));
        Assert(core.Pause(pausedPhysics), "Debug 快照测试任务无法暂停");

        var owner = new Node();
        AddChild(owner);
        core.Schedule(
            10d,
            static () => { },
            new ScheduleOptions(owner: owner));
        RemoveChild(owner);
        owner.Free();

        core.Schedule(0d, static () => throw new InvalidOperationException("expected"));
        core.Advance(SchedulePhase.Process, 0d, 0d, false);
        Assert(core.Cancel(game), "Debug 快照测试任务无法取消");

        SchedulerDebugSnapshot snapshot = core.GetDebugSnapshot();
        Assert(snapshot.ActiveCount == 2, "Debug 快照活动任务数不正确");
        Assert(snapshot.PausedCount == 1, "Debug 快照暂停任务数不正确");
        Assert(snapshot.RepeatingCount == 1, "Debug 快照重复任务数不正确");
        Assert(snapshot.UnscaledProcessCount == 1 && snapshot.RealPhysicsCount == 1,
            "Debug 快照六队列分布不正确");
        Assert(snapshot.GameProcessCount == 0 &&
               snapshot.RealProcessCount == 0 &&
               snapshot.GamePhysicsCount == 0 &&
               snapshot.UnscaledPhysicsCount == 0,
            "Debug 快照包含不存在的队列任务");
        Assert(snapshot.LastProcessDispatchCount == 1 && snapshot.LastPhysicsDispatchCount == 0,
            "Debug 快照最近派发数不正确");
        Assert(snapshot.CanceledCount == 3,
            "Debug 快照累计取消数不正确");
        Assert(snapshot.OwnerCanceledCount == 1,
            "Debug 快照 Owner 自动取消数不正确");
        Assert(snapshot.CallbackFailedCount == 1,
            "Debug 快照 callback 异常取消数不正确");
        Assert(snapshot.NextRemainingSeconds.HasValue,
            "Debug 快照缺少下一任务剩余时间");
        AssertApproximately(snapshot.NextRemainingSeconds!.Value, 3d,
            "Debug 快照下一任务剩余时间不正确");
        core.Shutdown();
    }
#endif

    private static void VerifyShutdown()
    {
        var core = new SchedulerCore();
        Task processDelay = core.DelayAsync(10d);
        Task physicsDelay = core.DelayAsync(
            10d,
            new ScheduleOptions(phase: SchedulePhase.Physics));
        core.ScheduleRepeating(1d, static () => { });

        core.Shutdown();
        core.Shutdown();

        Assert(processDelay.IsCanceled && physicsDelay.IsCanceled,
            "Shutdown 没有取消全部 DelayAsync");
        Assert(core.ActiveCount == 0, "Shutdown 后仍有活动任务");
        Assert(core.PendingCancellationCount == 0, "Shutdown 后仍有待处理取消请求");
        Assert(core.GetQueuedItemCount(ScheduleClock.GameTime, SchedulePhase.Process) == 0,
            "Shutdown 后 Process 队列未清空");
        Assert(core.GetQueuedItemCount(ScheduleClock.GameTime, SchedulePhase.Physics) == 0,
            "Shutdown 后 Physics 队列未清空");
        AssertThrows<ObjectDisposedException>(
            () => core.Schedule(0d, static () => { }),
            "Shutdown 后仍可创建任务");
        AssertThrows<ObjectDisposedException>(
            () => core.Advance(SchedulePhase.Process, 0d, 0d, false),
            "Shutdown 后仍可推进时钟");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertApproximately(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) > 0.000001d)
            throw new InvalidOperationException($"{message}；expected={expected}, actual={actual}");
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
