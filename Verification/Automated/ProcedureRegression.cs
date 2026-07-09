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
            await RunAsync("切换顺序", VerifyChangeOrderAsync);
            await RunAsync("并发切换拒绝", VerifyConcurrentChangeRejectionAsync);
            await RunAsync("Exit 失败保留旧流程", VerifyExitFailureAsync);
            await RunAsync("Enter 失败后当前流程为空", VerifyEnterFailureAsync);
            await RunAsync("Context 获取服务", VerifyContextServiceAccessAsync);
            await RunAsync("Enter 内请求后续流程", VerifyEnterRequestedChangeAsync);
            await RunAsync("当前流程方法请求切换", VerifyCurrentProcedureRequestedChangeAsync);

            GD.Print($"[ProcedureRegression] PASS ({_passed}/9)");
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
        AssertThrows<ProcedureChangeException>(
            () => service.ChangeAsync(new RecordingProcedure("Other")).GetAwaiter().GetResult(),
            "并发切换没有抛出 ProcedureChangeException");

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

        AssertThrows<ProcedureChangeException>(
            () => service.ChangeAsync(nextProcedure).GetAwaiter().GetResult(),
            "Exit 失败没有抛出 ProcedureChangeException");
        Assert(ReferenceEquals(oldProcedure, service.Current), "Exit 失败后没有保留旧流程");
        Assert(nextProcedure.EnterCount == 0, "Exit 失败后不应进入新流程");
        Assert(!service.IsChanging, "Exit 失败后 IsChanging 未复位");
    }

    private static async Task VerifyEnterFailureAsync()
    {
        var service = new ProcedureService();
        var oldProcedure = new RecordingProcedure("Old");
        var failingProcedure = new FailingEnterProcedure("Broken");
        await service.ChangeAsync(oldProcedure);

        AssertThrows<ProcedureChangeException>(
            () => service.ChangeAsync(failingProcedure).GetAwaiter().GetResult(),
            "Enter 失败没有抛出 ProcedureChangeException");
        Assert(service.Current is null, "Enter 失败后 Current 应为空");
        Assert(oldProcedure.ExitCount == 1, "Enter 失败前应已退出旧流程");
        Assert(!service.IsChanging, "Enter 失败后 IsChanging 未复位");
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
    private sealed class BlockingProcedure : RecordingProcedure
    {
        private readonly TaskCompletionSource _releaseEnter = new();
        public TaskCompletionSource EnterStarted { get; } = new();

        public BlockingProcedure(string name) : base(name)
        {
        }

        public override async Task EnterAsync(ProcedureContext context)
        {
            EnterStarted.SetResult();
            await _releaseEnter.Task;
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
