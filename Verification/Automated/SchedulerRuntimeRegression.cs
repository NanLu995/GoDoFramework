using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>SchedulerService 真实帧采样、暂停语义与 Runtime 注册的无交互回归入口。</summary>
public sealed partial class SchedulerRuntimeRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            ProcessMode = ProcessModeEnum.Always;
            ISchedulerService service = Services.Get<ISchedulerService>();

            await RunAsync("Runtime 注册 Scheduler 服务", () => VerifyRuntimeRegistrationAsync(service));
            await RunAsync("Process 与 Physics 真实阶段派发", () => VerifyRuntimePhasesAsync(service));
            await RunAsync("TimeScale 只影响 GameTime", () => VerifyRuntimeTimeScaleAsync(service));
            await RunAsync("场景树暂停只推进 RealTime", () => VerifyRuntimePauseAsync(service));
            await RunAsync("Owner 退出取消运行时任务", () => VerifyRuntimeOwnerAsync(service));
            await RunAsync("SchedulerService 退出取消等待", VerifyServiceExitAsync);

            GD.Print($"[SchedulerRuntimeRegression] PASS ({_passed}/6)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            Engine.TimeScale = 1d;
            GetTree().Paused = false;
            GD.PushError($"[SchedulerRuntimeRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync(string name, Func<Task> verification)
    {
        await verification();
        _passed++;
        GD.Print($"[SchedulerRuntimeRegression] PASS: {name}");
    }

    private static Task VerifyRuntimeRegistrationAsync(ISchedulerService service)
    {
        Assert(service is SchedulerService scheduler && scheduler.IsInsideTree(),
            "Services 返回的不是已进入场景树的 SchedulerService");
#if DEBUG
        SchedulerDebugSnapshot snapshot = ((SchedulerService)service).GetDebugSnapshot();
        Assert(snapshot.ActiveCount == 0, "新注册 Scheduler 的 Debug 快照不是空状态");
#endif
        return Task.CompletedTask;
    }

    private async Task VerifyRuntimePhasesAsync(ISchedulerService service)
    {
        int processCount = 0;
        int physicsCount = 0;
        service.Schedule(0d, () => processCount++);
        service.Schedule(
            0d,
            () => physicsCount++,
            new ScheduleOptions(phase: SchedulePhase.Physics));

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Assert(processCount == 1, "Process 任务没有在真实 Process 阶段执行一次");
        Assert(physicsCount == 1, "Physics 任务没有在真实 Physics 阶段执行一次");
    }

    private static async Task VerifyRuntimeTimeScaleAsync(ISchedulerService service)
    {
        double previousTimeScale = Engine.TimeScale;
        bool gameTriggered = false;
        bool unscaledTriggered = false;
        bool realTriggered = false;

        try
        {
            Engine.TimeScale = 0.1d;
            service.Schedule(0.03d, () => gameTriggered = true);
            service.Schedule(
                0.03d,
                () => unscaledTriggered = true,
                ScheduleOptions.UnscaledGameTime);
            service.Schedule(0.03d, () => realTriggered = true, ScheduleOptions.RealTime);

            await service.DelayAsync(0.1d, ScheduleOptions.RealTime);

            Assert(!gameTriggered, "GameTime 没有受较低 TimeScale 影响");
            Assert(unscaledTriggered, "UnscaledGameTime 错误受 TimeScale 影响");
            Assert(realTriggered, "RealTime 没有按真实时间执行");

            Engine.TimeScale = 1d;
            await service.DelayAsync(0.05d, ScheduleOptions.RealTime);
            Assert(gameTriggered, "恢复正常 TimeScale 后 GameTime 任务没有完成");
        }
        finally
        {
            Engine.TimeScale = previousTimeScale;
        }
    }

    private async Task VerifyRuntimePauseAsync(ISchedulerService service)
    {
        bool previousPaused = GetTree().Paused;
        bool gameTriggered = false;
        bool unscaledTriggered = false;
        bool realTriggered = false;

        try
        {
            service.Schedule(0.03d, () => gameTriggered = true);
            service.Schedule(
                0.03d,
                () => unscaledTriggered = true,
                ScheduleOptions.UnscaledGameTime);
            service.Schedule(0.03d, () => realTriggered = true, ScheduleOptions.RealTime);
            GetTree().Paused = true;

            await service.DelayAsync(0.08d, ScheduleOptions.RealTime);

            Assert(!gameTriggered, "暂停期间 GameTime 仍在推进");
            Assert(!unscaledTriggered, "暂停期间 UnscaledGameTime 仍在推进");
            Assert(realTriggered, "暂停期间 RealTime 没有推进");

            GetTree().Paused = false;
            await service.DelayAsync(0.06d, ScheduleOptions.RealTime);
            Assert(gameTriggered && unscaledTriggered, "恢复后游戏时钟任务没有继续完成");
        }
        finally
        {
            GetTree().Paused = previousPaused;
        }
    }

    private async Task VerifyRuntimeOwnerAsync(ISchedulerService service)
    {
        var owner = new Node();
        AddChild(owner);
        bool callbackTriggered = false;
        ScheduleOptions options = new(owner: owner);
        service.Schedule(0.03d, () => callbackTriggered = true, options);
        Task delay = service.DelayAsync(0.03d, options);

        RemoveChild(owner);

        Assert(delay.IsCanceled, "Owner 退出没有取消运行时 DelayAsync");
        await service.DelayAsync(0.05d, ScheduleOptions.RealTime);
        Assert(!callbackTriggered, "Owner 退出后运行时回调仍被执行");
        owner.Free();
    }

    private async Task VerifyServiceExitAsync()
    {
        var service = new SchedulerService();
        AddChild(service);
        Task delay = service.DelayAsync(1d, ScheduleOptions.RealTime);

        RemoveChild(service);

        Assert(delay.IsCanceled, "SchedulerService 退出场景树没有取消等待");
        service.Free();
        await Task.CompletedTask;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
