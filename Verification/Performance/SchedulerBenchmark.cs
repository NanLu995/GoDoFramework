using System;
using System.Diagnostics;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>Scheduler 核心空闲、创建取消与批量到期的可重复性能基准。</summary>
public sealed partial class SchedulerBenchmark : Node
{
    private const int WaitingTaskCount = 1_000;
    private const int IdleAdvanceCount = 10_000;
    private const int CreateCancelCount = 10_000;
    private const int DueTaskCount = 1_000;

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            WarmUpMeasurementApis();
            WarmUpSchedulerPaths();
            BenchmarkIdleAdvance();
            BenchmarkCreateAndCancel();
            BenchmarkBatchDispatch();
            GD.Print(
                $"[SchedulerBenchmark] PASS; Build={BuildConfiguration}; " +
                $"Processors={System.Environment.ProcessorCount}; OS={OS.GetName()}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SchedulerBenchmark] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void WarmUpMeasurementApis()
    {
        for (int index = 0; index < 10; index++)
        {
            _ = GC.GetAllocatedBytesForCurrentThread();
            _ = Stopwatch.GetTimestamp();
        }
    }

    private static void WarmUpSchedulerPaths()
    {
        var idleCore = new SchedulerCore();
        for (int index = 0; index < 100; index++)
            idleCore.Schedule(3_600d, static () => { });
        for (int index = 0; index < 20_000; index++)
            idleCore.Advance(SchedulePhase.Process, 0d, 0d, false);
        idleCore.Shutdown();

        var cancellationCore = new SchedulerCore();
        for (int index = 0; index < 20_000; index++)
        {
            ScheduleHandle handle = cancellationCore.Schedule(3_600d, static () => { });
            cancellationCore.Cancel(handle);
        }
        cancellationCore.Shutdown();

        for (int iteration = 0; iteration < 20; iteration++)
        {
            var dispatchCore = new SchedulerCore();
            for (int index = 0; index < 100; index++)
                dispatchCore.Schedule(1d, static () => { });
            dispatchCore.Advance(SchedulePhase.Process, 1d, 1d, false);
            dispatchCore.Shutdown();
        }
    }

    private static void BenchmarkIdleAdvance()
    {
        var core = new SchedulerCore();
        for (int index = 0; index < WaitingTaskCount; index++)
            core.Schedule(3_600d, static () => { });

        for (int index = 0; index < 100; index++)
            core.Advance(SchedulePhase.Process, 0d, 0d, false);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < IdleAdvanceCount; index++)
            core.Advance(SchedulePhase.Process, 0d, 0d, false);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        long started = Stopwatch.GetTimestamp();
        for (int index = 0; index < IdleAdvanceCount; index++)
            core.Advance(SchedulePhase.Process, 0d, 0d, false);
        long finished = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started, finished);

        Assert(allocated == 0,
            $"1,000 个等待任务的空闲推进产生托管分配: {allocated} bytes");
        Assert(core.ActiveCount == WaitingTaskCount, "空闲推进错误改变活动任务数量");
        GD.Print(
            $"[SchedulerBenchmark] Idle: Tasks={WaitingTaskCount}; Advances={IdleAdvanceCount}; " +
            $"ElapsedMs={elapsed.TotalMilliseconds:F3}; AllocatedBytes={allocated}");
        core.Shutdown();
    }

    private static void BenchmarkCreateAndCancel()
    {
        var allocationCore = new SchedulerCore();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < CreateCancelCount; index++)
        {
            ScheduleHandle handle = allocationCore.Schedule(3_600d, static () => { });
            Assert(allocationCore.Cancel(handle), "创建后的任务无法立即取消");
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        var timingCore = new SchedulerCore();
        long started = Stopwatch.GetTimestamp();
        for (int index = 0; index < CreateCancelCount; index++)
        {
            ScheduleHandle handle = timingCore.Schedule(3_600d, static () => { });
            Assert(timingCore.Cancel(handle), "创建后的任务无法立即取消");
        }
        long finished = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started, finished);

        int queuedItems = timingCore.GetQueuedItemCount(
            ScheduleClock.GameTime,
            SchedulePhase.Process);
        Assert(allocationCore.ActiveCount == 0 && timingCore.ActiveCount == 0,
            "批量取消后仍有活动任务");
        Assert(queuedItems < 64, $"批量取消后失效队列未压缩: {queuedItems}");
        GD.Print(
            $"[SchedulerBenchmark] CreateCancel: Operations={CreateCancelCount}; " +
            $"ElapsedMs={elapsed.TotalMilliseconds:F3}; AllocatedBytes={allocated}; " +
            $"QueuedItems={queuedItems}");
        allocationCore.Shutdown();
        timingCore.Shutdown();
    }

    private static void BenchmarkBatchDispatch()
    {
        var allocationCore = new SchedulerCore();
        int allocationCallbackCount = 0;
        Action allocationCallback = () => allocationCallbackCount++;
        for (int index = 0; index < DueTaskCount; index++)
            allocationCore.Schedule(1d, allocationCallback);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        int allocationDispatched = allocationCore.Advance(
            SchedulePhase.Process,
            1d,
            1d,
            false);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        var timingCore = new SchedulerCore();
        int timingCallbackCount = 0;
        Action timingCallback = () => timingCallbackCount++;
        for (int index = 0; index < DueTaskCount; index++)
            timingCore.Schedule(1d, timingCallback);

        long started = Stopwatch.GetTimestamp();
        int timingDispatched = timingCore.Advance(SchedulePhase.Process, 1d, 1d, false);
        long finished = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started, finished);

        Assert(allocationDispatched == DueTaskCount && timingDispatched == DueTaskCount,
            "批量到期任务没有全部派发");
        Assert(allocationCallbackCount == DueTaskCount && timingCallbackCount == DueTaskCount,
            "批量到期 callback 数量不正确");
        Assert(allocationCore.ActiveCount == 0 && timingCore.ActiveCount == 0,
            "批量派发后仍有活动任务");
        Assert(allocated == 0, $"批量派发已有任务时产生托管分配: {allocated} bytes");
        GD.Print(
            $"[SchedulerBenchmark] BatchDispatch: Tasks={DueTaskCount}; " +
            $"ElapsedMs={elapsed.TotalMilliseconds:F3}; AllocatedBytes={allocated}");
        allocationCore.Shutdown();
        timingCore.Shutdown();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
