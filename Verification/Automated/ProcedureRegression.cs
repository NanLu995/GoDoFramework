using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>Procedure 的无交互回归验证入口。</summary>
public sealed partial class ProcedureRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            await RunAsync("初始状态为空", VerifyInitialStateAsync);
            await RunAsync("首次进入流程", VerifyFirstEnterAsync);
            await RunAsync("泛型进入无参流程", VerifyGenericChangeAsync);
            await RunAsync("切换顺序", VerifyChangeOrderAsync);
            await RunAsync("并发切换拒绝", VerifyConcurrentChangeRejectionAsync);
            await RunAsync("Exit 失败保留旧流程", VerifyExitFailureAsync);
            await RunAsync("Enter 失败后当前流程为空", VerifyEnterFailureAsync);
            await RunAsync("Context 获取服务", VerifyContextServiceAccessAsync);
            await RunAsync("Enter 内请求后续流程", VerifyEnterRequestedChangeAsync);
            await RunAsync("当前流程方法请求切换", VerifyCurrentProcedureRequestedChangeAsync);
            await RunAsync("首次流程请求获胜", VerifyFirstRequestedChangeWinsAsync);
            await RunAsync("请求切换失败通知", VerifyRequestedChangeFailureNotificationAsync);
            await RunAsync("Context 泛型请求无参流程", VerifyGenericRequestAsync);
            await RunAsync("失败后清理待处理请求", VerifyFailedChangeClearsRequestedProcedureAsync);
            await RunAsync("正常退出按逆序清理激活资源", VerifyActivationCleanupAsync);
            await RunAsync("Enter 失败清理激活资源", VerifyEnterFailureCleanupAsync);
            await RunAsync("Exit 失败保留激活 Context", VerifyExitFailureKeepsContextActiveAsync);
            await RunAsync("旧 Context 拒绝流程请求", VerifyInactiveContextRejectsRequestAsync);
            await RunAsync("清理异常可见且继续清理", VerifyCleanupFailureAsync);
            await RunAsync("LifetimeToken 回调异常不阻断清理", VerifyLifetimeCancellationFailureAsync);
            await RunAsync("Shutdown 失效但不执行业务清理", VerifyShutdownInvalidatesContextAsync);
            await RunAsync("Shutdown 取消异常后仍复位服务", VerifyShutdownCancellationFailureAsync);
            await RunAsync("Shutdown 取消进行中的切换", VerifyShutdownCancelsInFlightChangeAsync);
            await RunAsync("泛型入口先验证主线程", VerifyGenericEntryPointsValidateThreadFirstAsync);
            await RunAsync("Name 读取失败不遮盖生命周期异常", VerifyFailingNameDoesNotMaskLifecycleFailureAsync);

            GD.Print($"[ProcedureRegression] PASS ({_passed}/25)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[ProcedureRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync(string name, Func<Task> verification)
    {
        await verification();
        _passed++;
        GD.Print($"[ProcedureRegression] PASS: {name}");
    }

    private static Task VerifyInitialStateAsync()
    {
        var service = new ProcedureService();
        Assert(service.Current is null, "初始 Current 不是 null");
        Assert(!service.IsChanging, "初始 IsChanging 不是 false");
#if DEBUG
        ProcedureDebugSnapshot snapshot = service.GetDebugSnapshot();
        Assert(snapshot.Phase == ProcedureDebugPhase.Idle, "初始 Debug 阶段不是 Idle");
        Assert(snapshot.LastResult == ProcedureDebugResult.None, "初始 Debug 结果不是 None");
        Assert(snapshot.LastDurationMilliseconds == 0, "初始 Debug 耗时不是 0");
#endif
        return Task.CompletedTask;
    }

    private static async Task VerifyFirstEnterAsync()
    {
        var service = new ProcedureService();
        var procedure = new RecordingProcedure("Boot");

        await service.ChangeAsync(procedure);

        Assert(ReferenceEquals(procedure, service.Current), "首次进入后 Current 不正确");
        Assert(!service.IsChanging, "首次进入完成后 IsChanging 未复位");
        Assert(procedure.EnterCount == 1, "首次进入没有调用 EnterAsync");
        Assert(procedure.ExitCount == 0, "首次进入不应调用 ExitAsync");
#if DEBUG
        ProcedureDebugSnapshot snapshot = service.GetDebugSnapshot();
        Assert(snapshot.LastPhase == ProcedureDebugPhase.Entering, "首次进入的最近阶段不正确");
        Assert(snapshot.LastResult == ProcedureDebugResult.Succeeded, "首次进入的最近结果不是成功");
        Assert(snapshot.LastFailure is null, "首次进入成功后仍保留失败详情");
#endif
    }

    private static async Task VerifyGenericChangeAsync()
    {
        var service = new ProcedureService();

        await service.ChangeAsync<ParameterlessProcedure>();

        Assert(service.Current is ParameterlessProcedure, "泛型切换没有创建目标流程");
        var procedure = (ParameterlessProcedure)service.Current!;
        Assert(procedure.EnterCount == 1, "泛型切换没有调用 EnterAsync");
    }

    private static async Task VerifyChangeOrderAsync()
    {
        var service = new ProcedureService();
        var log = new ProcedureLog();
        var first = new RecordingProcedure("Menu", log);
        var second = new RecordingProcedure("Game", log);

        await service.ChangeAsync(first);
        await service.ChangeAsync(second);

        Assert(ReferenceEquals(second, service.Current), "切换后 Current 不正确");
        Assert(log.Text == "Enter:Menu;Exit:Menu;Enter:Game;", $"切换顺序不正确: {log.Text}");
    }

    private static async Task VerifyConcurrentChangeRejectionAsync()
    {
        var service = new ProcedureService();
        var blocker = new BlockingProcedure("Blocker");
        Task changeTask = service.ChangeAsync(blocker);
        await blocker.EnterStarted.Task;

        Assert(service.IsChanging, "阻塞 Enter 期间 IsChanging 不是 true");
        ProcedureChangeException rejection = await AssertThrowsAsync<ProcedureChangeException>(
            () => service.ChangeAsync(new RecordingProcedure("Other")),
            "并发切换没有抛出 ProcedureChangeException");
        Assert(rejection.Phase == ProcedureChangePhase.Requesting,
            "并发切换拒绝没有标记 Requesting 阶段");
#if DEBUG
        ProcedureDebugSnapshot rejectedSnapshot = service.GetDebugSnapshot();
        Assert(rejectedSnapshot.Phase == ProcedureDebugPhase.Entering, "并发拒绝改变了当前切换阶段");
        Assert(rejectedSnapshot.LastPhase == ProcedureDebugPhase.Entering, "并发拒绝没有记录发生阶段");
        Assert(rejectedSnapshot.LastResult == ProcedureDebugResult.Rejected, "并发拒绝没有分类为 Rejected");
#endif

        blocker.ReleaseEnter();
        await changeTask;
        Assert(ReferenceEquals(blocker, service.Current), "阻塞流程完成后 Current 不正确");
    }

    private static async Task VerifyExitFailureAsync()
    {
        var service = new ProcedureService();
        var oldProcedure = new FailingExitProcedure("Old");
        var nextProcedure = new RecordingProcedure("Next");
        await service.ChangeAsync(oldProcedure);

        ProcedureChangeException exception = await AssertThrowsAsync<ProcedureChangeException>(
            () => service.ChangeAsync(nextProcedure),
            "Exit 失败没有抛出 ProcedureChangeException");
        Assert(exception.Phase == ProcedureChangePhase.Exiting,
            "Exit 失败没有标记 Exiting 阶段");
        Assert(ReferenceEquals(oldProcedure, service.Current), "Exit 失败后没有保留旧流程");
        Assert(nextProcedure.EnterCount == 0, "Exit 失败后不应进入新流程");
        Assert(!service.IsChanging, "Exit 失败后 IsChanging 未复位");
#if DEBUG
        ProcedureDebugSnapshot snapshot = service.GetDebugSnapshot();
        Assert(snapshot.LastPhase == ProcedureDebugPhase.Exiting, "Exit 失败的最近阶段不正确");
        Assert(snapshot.LastResult == ProcedureDebugResult.Failed, "Exit 失败没有分类为 Failed");
        Assert(snapshot.LastFailure?.Contains("Old", StringComparison.Ordinal) == true,
            "Exit 失败详情没有包含流程名称");
#endif
    }

    private static async Task VerifyEnterFailureAsync()
    {
        var service = new ProcedureService();
        var oldProcedure = new RecordingProcedure("Old");
        var failingProcedure = new FailingEnterProcedure("Broken");
        await service.ChangeAsync(oldProcedure);

        ProcedureChangeException exception = await AssertThrowsAsync<ProcedureChangeException>(
            () => service.ChangeAsync(failingProcedure),
            "Enter 失败没有抛出 ProcedureChangeException");
        Assert(exception.Phase == ProcedureChangePhase.Entering,
            "Enter 失败没有标记 Entering 阶段");
        Assert(service.Current is null, "Enter 失败后 Current 应为空");
        Assert(oldProcedure.ExitCount == 1, "Enter 失败前应已退出旧流程");
        Assert(!service.IsChanging, "Enter 失败后 IsChanging 未复位");
#if DEBUG
        ProcedureDebugSnapshot snapshot = service.GetDebugSnapshot();
        Assert(snapshot.LastPhase == ProcedureDebugPhase.Entering, "Enter 失败的最近阶段不正确");
        Assert(snapshot.LastResult == ProcedureDebugResult.Failed, "Enter 失败没有分类为 Failed");
        Assert(snapshot.LastFailure?.Contains("Broken", StringComparison.Ordinal) == true,
            "Enter 失败详情没有包含流程名称");
#endif
    }

    private static async Task VerifyContextServiceAccessAsync()
    {
        var service = new ProcedureService();
        var registered = new ContextTestService();
        try
        {
            Services.Register<IContextTestService>(registered);
            var procedure = new ContextProcedure("Context");
            await service.ChangeAsync(procedure);

            Assert(ReferenceEquals(registered, procedure.RequiredService), "Context.GetService 未返回注册服务");
            Assert(ReferenceEquals(registered, procedure.OptionalService), "Context.TryGetService 未返回注册服务");
        }
        finally
        {
            Services.Unregister<IContextTestService>(registered);
        }
    }

    private static async Task VerifyEnterRequestedChangeAsync()
    {
        var service = new ProcedureService();
        var log = new ProcedureLog();
        var next = new RecordingProcedure("Menu", log);
        var boot = new RequestOnEnterProcedure("Boot", next, log);

        await service.ChangeAsync(boot);

        Assert(ReferenceEquals(next, service.Current), "Enter 内 RequestChange 后 Current 不是请求的后续流程");
        Assert(log.Text == "Enter:Boot;Exit:Boot;Enter:Menu;", $"Enter 请求切换顺序不正确: {log.Text}");
        Assert(!service.IsChanging, "Enter 请求切换完成后 IsChanging 未复位");
    }

    private static async Task VerifyCurrentProcedureRequestedChangeAsync()
    {
        var service = new ProcedureService();
        var log = new ProcedureLog();
        var current = new CommandProcedure("Menu", log);
        var next = new RecordingProcedure("Game", log);

        await service.ChangeAsync(current);
        current.RequestNext(next);
        await Task.Delay(1);

        Assert(ReferenceEquals(next, service.Current), "当前流程方法 RequestChange 后 Current 不正确");
        Assert(log.Text == "Enter:Menu;Exit:Menu;Enter:Game;", $"当前流程方法请求切换顺序不正确: {log.Text}");
    }

    private static async Task VerifyFirstRequestedChangeWinsAsync()
    {
        var service = new ProcedureService();
        var current = new BlockingExitRequestProcedure();
        var first = new RecordingProcedure("First");
        var second = new RecordingProcedure("Second");
        await service.ChangeAsync(current);

        Assert(current.TryRequestNext(first), "第一次 TryRequestChange 没有成功登记");
        await current.ExitStarted.Task;
        Assert(!current.TryRequestNext(second), "第二次 TryRequestChange 覆盖了首个请求");
#if DEBUG
        ProcedureDebugSnapshot snapshot = service.GetDebugSnapshot();
        Assert(snapshot.LastRejectedRequestName == "Second",
            "Debug 快照没有记录被拒绝的流程请求");
        Assert(snapshot.LastRequestRejection?.Length > 0,
            "Debug 快照没有记录流程请求拒绝原因");
#endif

        current.ReleaseExit();
        await Task.Yield();
        Assert(ReferenceEquals(first, service.Current), "首个流程请求没有获胜");
        Assert(second.EnterCount == 0, "被拒绝的流程请求仍然进入了生命周期");
    }

    private static async Task VerifyRequestedChangeFailureNotificationAsync()
    {
        var service = new ProcedureService();
        var current = new CommandProcedure("Current", new ProcedureLog());
        var failureSource = new FailingEnterProcedure("RequestedFailure");
        var failureCompletion = new TaskCompletionSource<ProcedureChangeException>();
        bool observedStableState = false;

        void OnRequestedChangeFailed(ProcedureChangeException exception)
        {
            observedStableState = !service.IsChanging;
            failureCompletion.TrySetResult(exception);
        }

        service.RequestedChangeFailed += OnRequestedChangeFailed;
        try
        {
            await service.ChangeAsync(current);
            current.RequestNext(failureSource);
            ProcedureChangeException exception = await failureCompletion.Task;
            Assert(exception.Phase == ProcedureChangePhase.Entering,
                "请求切换失败通知没有保留 Entering 阶段");
            Assert(exception.ProcedureName == "RequestedFailure",
                "请求切换失败通知没有保留目标流程名称");
            Assert(observedStableState, "请求切换失败通知发生在切换状态复位前");
            Assert(service.Current is null, "请求目标 Enter 失败后 Current 应为空");
        }
        finally
        {
            service.RequestedChangeFailed -= OnRequestedChangeFailed;
        }
    }

    private static async Task VerifyGenericRequestAsync()
    {
        var service = new ProcedureService();

        await service.ChangeAsync(new GenericRequestProcedure());

        Assert(service.Current is ParameterlessProcedure, "Context 泛型请求没有创建目标流程");
        var procedure = (ParameterlessProcedure)service.Current!;
        Assert(procedure.EnterCount == 1, "Context 泛型请求没有调用 EnterAsync");
    }

    private static async Task VerifyFailedChangeClearsRequestedProcedureAsync()
    {
        var enterService = new ProcedureService();
        var staleAfterEnter = new RecordingProcedure("StaleAfterEnter");
        var failingEnter = new RequestThenFailEnterProcedure("BrokenEnter", staleAfterEnter);

        await AssertThrowsAsync<ProcedureChangeException>(
            () => enterService.ChangeAsync(failingEnter),
            "Enter 失败没有抛出 ProcedureChangeException");

        var enterRecovery = new RecordingProcedure("EnterRecovery");
        await enterService.ChangeAsync(enterRecovery);
        Assert(ReferenceEquals(enterRecovery, enterService.Current), "Enter 失败残留请求覆盖了恢复流程");
        Assert(staleAfterEnter.EnterCount == 0, "Enter 失败后仍执行了残留请求");

        var exitService = new ProcedureService();
        var staleAfterExit = new RecordingProcedure("StaleAfterExit");
        var failingExit = new RequestThenFailExitProcedure("BrokenExit", staleAfterExit);
        await exitService.ChangeAsync(failingExit);

        await AssertThrowsAsync<ProcedureChangeException>(
            () => exitService.ChangeAsync(new RecordingProcedure("Rejected")),
            "Exit 失败没有抛出 ProcedureChangeException");

        failingExit.FailExit = false;
        var exitRecovery = new RecordingProcedure("ExitRecovery");
        await exitService.ChangeAsync(exitRecovery);
        Assert(ReferenceEquals(exitRecovery, exitService.Current), "Exit 失败残留请求覆盖了恢复流程");
        Assert(staleAfterExit.EnterCount == 0, "Exit 失败后仍执行了残留请求");
    }

    private static async Task VerifyActivationCleanupAsync()
    {
        var service = new ProcedureService();
        var procedure = new CleanupProcedure("Owned");

        await service.ChangeAsync(procedure);
        Assert(!procedure.Context!.LifetimeToken.IsCancellationRequested, "激活 Context 的 LifetimeToken 错误地已取消");
#if DEBUG
        ProcedureDebugSnapshot activeSnapshot = service.GetDebugSnapshot();
        Assert(activeSnapshot.HasActiveContext, "进入成功后 Debug 快照没有激活 Context");
        Assert(activeSnapshot.CleanupCount == 3, "Debug 快照的待清理数量错误");
#endif
        await service.ChangeAsync(new RecordingProcedure("Next"));

        Assert(procedure.CleanupLog == "second;disposable;first;", $"清理顺序错误: {procedure.CleanupLog}");
        Assert(procedure.Context is { IsActive: false }, "退出后旧 Context 仍处于激活状态");
        Assert(procedure.Context.LifetimeToken.IsCancellationRequested, "正常退出没有取消 LifetimeToken");
    }

    private static async Task VerifyEnterFailureCleanupAsync()
    {
        var service = new ProcedureService();
        var procedure = new FailingOwnedEnterProcedure();

        await AssertThrowsAsync<ProcedureChangeException>(
            () => service.ChangeAsync(procedure),
            "Enter 失败没有抛出 ProcedureChangeException");

        EventChannel.Emit<ActivationTestEvent>();
        Assert(procedure.CleanupCount == 1, "Enter 失败没有执行已登记清理");
        Assert(procedure.EventCount == 0, "Enter 失败后 Context Events 仍接收事件");
        Assert(procedure.Context is { IsActive: false }, "Enter 失败后 Context 仍处于激活状态");
        Assert(procedure.Context!.LifetimeToken.IsCancellationRequested, "Enter 失败没有取消 LifetimeToken");
    }

    private static async Task VerifyExitFailureKeepsContextActiveAsync()
    {
        var service = new ProcedureService();
        var procedure = new FailingOwnedExitProcedure();
        await service.ChangeAsync(procedure);

        await AssertThrowsAsync<ProcedureChangeException>(
            () => service.ChangeAsync(new RecordingProcedure("Rejected")),
            "Exit 失败没有抛出 ProcedureChangeException");

        Assert(ReferenceEquals(procedure, service.Current), "Exit 失败后没有保留旧流程");
        Assert(procedure.Context is { IsActive: true }, "Exit 失败错误地使 Context 失效");
        Assert(!procedure.Context!.LifetimeToken.IsCancellationRequested, "Exit 失败错误地取消 LifetimeToken");
        Assert(procedure.CleanupCount == 0, "Exit 失败错误地执行了激活清理");
    }

    private static async Task VerifyInactiveContextRejectsRequestAsync()
    {
        var service = new ProcedureService();
        var first = new ContextCapturingProcedure();
        await service.ChangeAsync(first);
        await service.ChangeAsync(new RecordingProcedure("Next"));

        AssertThrows<InvalidOperationException>(
            () => first.Context.RequestChange(new RecordingProcedure("Stale")),
            "已退出流程的 Context 仍能请求切换");
        Assert(service.Current?.Name == "Next", "旧 Context 请求改变了当前流程");
    }

    private static async Task VerifyCleanupFailureAsync()
    {
        var service = new ProcedureService();
        var procedure = new FailingCleanupProcedure();
        var next = new RecordingProcedure("Next");
        await service.ChangeAsync(procedure);

        ProcedureChangeException exception = await AssertThrowsAsync<ProcedureChangeException>(
            () => service.ChangeAsync(next),
            "激活清理失败没有抛出 ProcedureChangeException");
        Assert(exception.Phase == ProcedureChangePhase.Cleanup,
            "激活清理失败没有标记 Cleanup 阶段");

        Assert(exception.InnerException?.Message == "cleanup failed", "清理失败没有保留原始异常");
        Assert(procedure.SuccessfulCleanupCount == 1, "一个清理失败后没有继续执行其余清理");
        Assert(procedure.Context is { IsActive: false }, "清理失败后 Context 仍处于激活状态");
        Assert(service.Current is null, "业务 Exit 成功但激活清理失败后 Current 应为空");
        Assert(next.EnterCount == 0, "激活清理失败后仍进入了目标流程");
    }

    private static async Task VerifyShutdownInvalidatesContextAsync()
    {
        var service = new ProcedureService();
        var procedure = new ShutdownOwnedProcedure();
        await service.ChangeAsync(procedure);

        service.Shutdown();
        EventChannel.Emit<ActivationTestEvent>();

        Assert(procedure.Context is { IsActive: false }, "Shutdown 后 Context 仍处于激活状态");
        Assert(procedure.Context!.LifetimeToken.IsCancellationRequested, "Shutdown 没有取消 LifetimeToken");
        Assert(procedure.BusinessCleanupCount == 0, "Shutdown 错误地执行业务清理");
        Assert(procedure.EventCount == 0, "Shutdown 后 Context Events 仍接收事件");
    }

    private static async Task VerifyLifetimeCancellationFailureAsync()
    {
        var service = new ProcedureService();
        var procedure = new CancellationFailureProcedure();
        var next = new RecordingProcedure("Next");
        await service.ChangeAsync(procedure);

        ProcedureChangeException exception = await AssertThrowsAsync<ProcedureChangeException>(
            () => service.ChangeAsync(next),
            "LifetimeToken 回调异常没有作为清理失败暴露");

        Assert(exception.InnerException is AggregateException,
            "LifetimeToken 回调异常没有保留 AggregateException");
        Assert(procedure.CleanupCount == 1, "LifetimeToken 回调异常阻断了业务清理");
        Assert(procedure.Context!.LifetimeToken.IsCancellationRequested,
            "LifetimeToken 回调异常后令牌未保持取消状态");
        Assert(service.Current is null, "LifetimeToken 回调异常后 Current 应为空");
        Assert(next.EnterCount == 0, "LifetimeToken 回调异常后仍进入了目标流程");
    }

    private static async Task VerifyShutdownCancellationFailureAsync()
    {
        var service = new ProcedureService();
        var procedure = new CancellationFailureProcedure();
        await service.ChangeAsync(procedure);

        AssertThrows<AggregateException>(
            service.Shutdown,
            "Shutdown 没有暴露 LifetimeToken 回调异常");

        Assert(service.Current is null, "Shutdown 取消异常后 Current 没有复位");
        Assert(!service.IsChanging, "Shutdown 取消异常后 IsChanging 没有复位");
        Assert(procedure.Context is { IsActive: false }, "Shutdown 取消异常后 Context 仍激活");
        Assert(procedure.Context!.LifetimeToken.IsCancellationRequested,
            "Shutdown 取消异常后 LifetimeToken 未保持取消状态");
        Assert(procedure.CleanupCount == 0, "Shutdown 取消异常错误地执行业务清理");
    }

    private static async Task VerifyShutdownCancelsInFlightChangeAsync()
    {
        var service = new ProcedureService();
        var blocker = new BlockingProcedure("Blocker");
        Task changeTask = service.ChangeAsync(blocker);
        await blocker.EnterStarted.Task;

        service.Shutdown();

        ProcedureChangeException exception = await AssertThrowsAsync<ProcedureChangeException>(
            () => changeTask,
            "Shutdown 后进行中的切换没有失败");
        Assert(exception.InnerException is OperationCanceledException, "Shutdown 失败未保留取消原因");
        Assert(exception.Phase == ProcedureChangePhase.Entering,
            "Shutdown 进行中的 Enter 没有标记 Entering 阶段");
        Assert(blocker.Context!.LifetimeToken.IsCancellationRequested,
            "Shutdown 没有通过 LifetimeToken 取消进行中的 Enter");
        Assert(service.Current is null, "Shutdown 后旧切换重新写回了 Current");
        Assert(!service.IsChanging, "Shutdown 后 IsChanging 未复位");
#if DEBUG
        ProcedureDebugSnapshot snapshot = service.GetDebugSnapshot();
        Assert(snapshot.LastPhase == ProcedureDebugPhase.Entering, "Shutdown 取消的最近阶段不正确");
        Assert(snapshot.LastResult == ProcedureDebugResult.LifecycleCanceled,
            "Shutdown 取消没有分类为 LifecycleCanceled");
#endif
    }

    private static async Task VerifyGenericEntryPointsValidateThreadFirstAsync()
    {
        var service = new ProcedureService();
        ConstructorTrackingProcedure.Reset();

        await AssertThrowsAsync<InvalidOperationException>(
            () => Task.Run(() => service.ChangeAsync<ConstructorTrackingProcedure>()),
            "泛型 ChangeAsync 在非主线程没有失败");
        Assert(ConstructorTrackingProcedure.ConstructorCount == 0, "泛型 ChangeAsync 在线程校验前创建了流程");

        var contextProcedure = new ContextCapturingProcedure();
        await service.ChangeAsync(contextProcedure);
        ConstructorTrackingProcedure.Reset();

        await AssertThrowsAsync<InvalidOperationException>(
            () => Task.Run(() => contextProcedure.Context.RequestChange<ConstructorTrackingProcedure>()),
            "泛型 RequestChange 在非主线程没有失败");
        Assert(ConstructorTrackingProcedure.ConstructorCount == 0, "泛型 RequestChange 在线程校验前创建了流程");
    }

    private static async Task VerifyFailingNameDoesNotMaskLifecycleFailureAsync()
    {
        var service = new ProcedureService();
        var procedure = new FailingNameProcedure();

        ProcedureChangeException exception = await AssertThrowsAsync<ProcedureChangeException>(
            () => service.ChangeAsync(procedure),
            "Name 读取失败遮盖了 ProcedureChangeException");

        Assert(exception.InnerException?.Message == "Enter failure", "Name 读取失败遮盖了原始 Enter 异常");
        Assert(exception.ProcedureName == nameof(FailingNameProcedure), "Name 读取失败没有使用类型名回退");
        Assert(exception.Phase == ProcedureChangePhase.Entering,
            "Name 读取失败后的异常没有标记 Entering 阶段");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class ProcedureLog
    {
        public string Text { get; private set; } = string.Empty;
        public void Append(string value) => Text += value;
    }

    private class RecordingProcedure : IProcedure
    {
        private readonly ProcedureLog? _log;

        public string Name { get; }
        public int EnterCount { get; private set; }
        public int ExitCount { get; private set; }

        public RecordingProcedure(string name, ProcedureLog? log = null)
        {
            Name = name;
            _log = log;
        }

        public virtual Task EnterAsync(ProcedureContext context)
        {
            EnterCount++;
            _log?.Append($"Enter:{Name};");
            return Task.CompletedTask;
        }

        public virtual Task ExitAsync(ProcedureContext context)
        {
            ExitCount++;
            _log?.Append($"Exit:{Name};");
            return Task.CompletedTask;
        }
    }

    private sealed class ParameterlessProcedure : IProcedure
    {
        public ParameterlessProcedure()
        {
        }

        public string Name => "Parameterless";

        public int EnterCount { get; private set; }

        public Task EnterAsync(ProcedureContext context)
        {
            EnterCount++;
            return Task.CompletedTask;
        }

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class GenericRequestProcedure : IProcedure
    {
        public string Name => "GenericRequest";

        public Task EnterAsync(ProcedureContext context)
        {
            context.RequestChange<ParameterlessProcedure>();
            return Task.CompletedTask;
        }

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class RequestOnEnterProcedure : RecordingProcedure
    {
        private readonly IProcedure _next;

        public RequestOnEnterProcedure(string name, IProcedure next, ProcedureLog log) : base(name, log)
        {
            _next = next;
        }

        public override Task EnterAsync(ProcedureContext context)
        {
            Task result = base.EnterAsync(context);
            context.RequestChange(_next);
            return result;
        }
    }

    private sealed class CommandProcedure : RecordingProcedure
    {
        private ProcedureContext? _context;

        public CommandProcedure(string name, ProcedureLog log) : base(name, log)
        {
        }

        public override Task EnterAsync(ProcedureContext context)
        {
            _context = context;
            return base.EnterAsync(context);
        }

        public override Task ExitAsync(ProcedureContext context)
        {
            _context = null;
            return base.ExitAsync(context);
        }

        public void RequestNext(IProcedure next)
        {
            if (_context == null)
                throw new InvalidOperationException("流程尚未进入，不能请求切换。");

            _context.RequestChange(next);
        }
    }

    private sealed class BlockingExitRequestProcedure : IProcedure
    {
        private readonly TaskCompletionSource _releaseExit = new();
        private ProcedureContext? _context;

        public string Name => "BlockingExitRequest";

        public TaskCompletionSource ExitStarted { get; } = new();

        public Task EnterAsync(ProcedureContext context)
        {
            _context = context;
            return Task.CompletedTask;
        }

        public Task ExitAsync(ProcedureContext context)
        {
            ExitStarted.TrySetResult();
            return _releaseExit.Task;
        }

        public bool TryRequestNext(IProcedure next)
        {
            if (_context is null)
                throw new InvalidOperationException("流程尚未进入，不能请求切换。");

            return _context.TryRequestChange(next);
        }

        public void ReleaseExit() => _releaseExit.TrySetResult();
    }

    private sealed class BlockingProcedure : RecordingProcedure
    {
        private readonly TaskCompletionSource _releaseEnter = new();
        public TaskCompletionSource EnterStarted { get; } = new();
        public ProcedureContext? Context { get; private set; }

        public BlockingProcedure(string name) : base(name)
        {
        }

        public override async Task EnterAsync(ProcedureContext context)
        {
            Context = context;
            EnterStarted.SetResult();
            await _releaseEnter.Task.WaitAsync(context.LifetimeToken);
            await base.EnterAsync(context);
        }

        public void ReleaseEnter() => _releaseEnter.SetResult();
    }

    private sealed class FailingExitProcedure : RecordingProcedure
    {
        public FailingExitProcedure(string name) : base(name)
        {
        }

        public override Task ExitAsync(ProcedureContext context) =>
            throw new InvalidOperationException("Exit failure");
    }

    private sealed class FailingEnterProcedure : RecordingProcedure
    {
        public FailingEnterProcedure(string name) : base(name)
        {
        }

        public override Task EnterAsync(ProcedureContext context) =>
            throw new InvalidOperationException("Enter failure");
    }

    private sealed class RequestThenFailEnterProcedure : RecordingProcedure
    {
        private readonly IProcedure _requested;

        public RequestThenFailEnterProcedure(string name, IProcedure requested) : base(name)
        {
            _requested = requested;
        }

        public override Task EnterAsync(ProcedureContext context)
        {
            context.RequestChange(_requested);
            throw new InvalidOperationException("Enter failure");
        }
    }

    private sealed class RequestThenFailExitProcedure : RecordingProcedure
    {
        private readonly IProcedure _requested;

        public bool FailExit { get; set; } = true;

        public RequestThenFailExitProcedure(string name, IProcedure requested) : base(name)
        {
            _requested = requested;
        }

        public override Task ExitAsync(ProcedureContext context)
        {
            if (!FailExit)
                return base.ExitAsync(context);

            context.RequestChange(_requested);
            throw new InvalidOperationException("Exit failure");
        }
    }

    private sealed class ContextCapturingProcedure : IProcedure
    {
        public string Name => "ContextCapturing";
        public ProcedureContext Context { get; private set; } = null!;

        public Task EnterAsync(ProcedureContext context)
        {
            Context = context;
            return Task.CompletedTask;
        }

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class ConstructorTrackingProcedure : IProcedure
    {
        public static int ConstructorCount { get; private set; }

        public ConstructorTrackingProcedure()
        {
            ConstructorCount++;
        }

        public string Name => "ConstructorTracking";

        public static void Reset() => ConstructorCount = 0;

        public Task EnterAsync(ProcedureContext context) => Task.CompletedTask;

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class FailingNameProcedure : IProcedure
    {
        public string Name => throw new InvalidOperationException("Name failure");

        public Task EnterAsync(ProcedureContext context) =>
            throw new InvalidOperationException("Enter failure");

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class CleanupProcedure : RecordingProcedure
    {
        public ProcedureContext? Context { get; private set; }
        public string CleanupLog { get; private set; } = string.Empty;

        public CleanupProcedure(string name) : base(name)
        {
        }

        public override Task EnterAsync(ProcedureContext context)
        {
            Context = context;
            context.RegisterCleanup(() => CleanupLog += "first;");
            context.RegisterCleanup(new CallbackDisposable(() => CleanupLog += "disposable;"));
            context.RegisterCleanup(() => CleanupLog += "second;");
            return base.EnterAsync(context);
        }
    }

    private sealed class CallbackDisposable : IDisposable
    {
        private readonly Action _dispose;

        public CallbackDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose() => _dispose();
    }

    private sealed class FailingOwnedEnterProcedure : IProcedure
    {
        public string Name => "FailingOwnedEnter";
        public ProcedureContext? Context { get; private set; }
        public int CleanupCount { get; private set; }
        public int EventCount { get; private set; }

        public Task EnterAsync(ProcedureContext context)
        {
            Context = context;
            context.RegisterCleanup(() => CleanupCount++);
            context.Events.On<ActivationTestEvent>(_ => EventCount++);
            throw new InvalidOperationException("enter failed");
        }

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class FailingOwnedExitProcedure : IProcedure
    {
        public string Name => "FailingOwnedExit";
        public ProcedureContext? Context { get; private set; }
        public int CleanupCount { get; private set; }

        public Task EnterAsync(ProcedureContext context)
        {
            Context = context;
            context.RegisterCleanup(() => CleanupCount++);
            return Task.CompletedTask;
        }

        public Task ExitAsync(ProcedureContext context) =>
            throw new InvalidOperationException("exit failed");
    }

    private sealed class FailingCleanupProcedure : IProcedure
    {
        public string Name => "FailingCleanup";
        public ProcedureContext? Context { get; private set; }
        public int SuccessfulCleanupCount { get; private set; }

        public Task EnterAsync(ProcedureContext context)
        {
            Context = context;
            context.RegisterCleanup(() => SuccessfulCleanupCount++);
            context.RegisterCleanup(() => throw new InvalidOperationException("cleanup failed"));
            return Task.CompletedTask;
        }

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class ShutdownOwnedProcedure : IProcedure
    {
        public string Name => "ShutdownOwned";
        public ProcedureContext? Context { get; private set; }
        public int BusinessCleanupCount { get; private set; }
        public int EventCount { get; private set; }

        public Task EnterAsync(ProcedureContext context)
        {
            Context = context;
            context.RegisterCleanup(() => BusinessCleanupCount++);
            context.Events.On<ActivationTestEvent>(_ => EventCount++);
            return Task.CompletedTask;
        }

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class CancellationFailureProcedure : IProcedure
    {
        public string Name => "CancellationFailure";
        public ProcedureContext? Context { get; private set; }
        public int CleanupCount { get; private set; }

        public Task EnterAsync(ProcedureContext context)
        {
            Context = context;
            context.LifetimeToken.Register(
                static () => throw new InvalidOperationException("lifetime cancellation failed"));
            context.RegisterCleanup(() => CleanupCount++);
            return Task.CompletedTask;
        }

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private readonly struct ActivationTestEvent : IEventMessage;

    private sealed class ContextProcedure : RecordingProcedure
    {
        public IContextTestService? RequiredService { get; private set; }
        public IContextTestService? OptionalService { get; private set; }

        public ContextProcedure(string name) : base(name)
        {
        }

        public override Task EnterAsync(ProcedureContext context)
        {
            RequiredService = context.GetService<IContextTestService>();
            Assert(context.TryGetService(out IContextTestService? optional), "TryGetService 没有找到注册服务");
            OptionalService = optional;
            return base.EnterAsync(context);
        }
    }

    private interface IContextTestService;
    private sealed class ContextTestService : IContextTestService;
}
