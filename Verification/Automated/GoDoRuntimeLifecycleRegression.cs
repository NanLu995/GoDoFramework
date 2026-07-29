using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>GoDoRuntime 初始化、失败清理与重新启动的无交互回归入口。</summary>
public sealed partial class GoDoRuntimeLifecycleRegression : Node
{
    private int _passed;
    private GoDoRuntime? _replacement;
    private GoDoRuntime? _broken;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            PackedScene runtimeScene = GD.Load<PackedScene>(
                "res://addons/godo_framework/Core/GoDoRuntime.tscn");
            GoDoRuntime original = GetNode<GoDoRuntime>("/root/GoDoRuntime");

            await RunAsync("正常启动注册全部服务", VerifyInitialRuntimeAsync);
            await RunAsync("关闭清理全部全局状态", () => VerifyShutdownAsync(original));
            await RunAsync("初始化失败后可重新启动", () => VerifyFailureRecoveryAsync(runtimeScene));

            await ShutdownRuntimeAsync(_replacement);
            GD.Print($"[GoDoRuntimeLifecycleRegression] PASS ({_passed}/3)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            await ShutdownRuntimeAsync(_replacement);
            await ShutdownRuntimeAsync(_broken);
            GD.PushError($"[GoDoRuntimeLifecycleRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunAsync(string name, Func<Task> verification)
    {
        await verification();
        _passed++;
        GD.Print($"[GoDoRuntimeLifecycleRegression] PASS: {name}");
    }

    private async Task VerifyInitialRuntimeAsync()
    {
        await NextFrameAsync();
        VerifyAllServices();
        Assert(MainThreadGuard.IsInitialized, "正常启动后未记录 Godot 主线程");
        Assert(GetTree().Root.HasNode("GoDoUI"), "UiRoot 未移动到 SceneTree 根节点");
    }

    private async Task VerifyShutdownAsync(GoDoRuntime original)
    {
        original.QueueFree();
        await NextFrameAsync();
        await NextFrameAsync();

        Assert(!GodotObject.IsInstanceValid(original), "GoDoRuntime 关闭后实例仍有效");
        Assert(!MainThreadGuard.IsInitialized, "GoDoRuntime 关闭后主线程记录未清理");
        Assert(!GetTree().Root.HasNode("GoDoRuntime"), "GoDoRuntime 关闭后仍留在根节点");
        Assert(!GetTree().Root.HasNode("GoDoUI"), "GoDoRuntime 关闭后 UiRoot 未清理");
        AssertThrows<InvalidOperationException>(
            () => Services.Get<IProcedureService>(),
            "GoDoRuntime 关闭后 Services 仍可访问");
    }

    private async Task VerifyFailureRecoveryAsync(PackedScene runtimeScene)
    {
        _broken = new GoDoRuntime { Name = "BrokenGoDoRuntime" };
        GetTree().Root.AddChild(_broken);
        await NextFrameAsync();
        await NextFrameAsync();

        Assert(
            !GodotObject.IsInstanceValid(_broken) || !_broken.IsInsideTree(),
            "配置错误的 GoDoRuntime 留下了半初始化实例");
        Assert(!MainThreadGuard.IsInitialized, "初始化失败后主线程记录未清理");

        _replacement = runtimeScene.Instantiate<GoDoRuntime>();
        GetTree().Root.AddChild(_replacement);
        await NextFrameAsync();
        await NextFrameAsync();

        Assert(GodotObject.IsInstanceValid(_replacement), "失败清理后无法创建新 GoDoRuntime");
        VerifyAllServices();
    }

    private static void VerifyAllServices()
    {
        _ = Services.Get<ISchedulerService>();
        _ = Services.Get<ISceneService>();
        _ = Services.Get<ICameraService>();
        _ = Services.Get<IInputService>();
        _ = Services.Get<IAudioService>();
        _ = Services.Get<ILocalizationService>();
        _ = Services.Get<IDataTableService>();
        _ = Services.Get<IUiService>();
        _ = Services.Get<ISaveService>();
        _ = Services.Get<ISettingsService>();
        _ = Services.Get<IProcedureService>();
    }

    private async Task ShutdownRuntimeAsync(GoDoRuntime? runtime)
    {
        if (!GodotObject.IsInstanceValid(runtime) || !runtime.IsInsideTree())
            return;

        runtime.QueueFree();
        await NextFrameAsync();
        await NextFrameAsync();
    }

    private async Task NextFrameAsync() =>
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

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
}
