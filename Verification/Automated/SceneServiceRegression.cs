using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>SceneService 切换、失败与生命周期取消的无交互回归入口。</summary>
public sealed partial class SceneServiceRegression : Node
{
    private static readonly ResourceKey TargetKey =
        ResourceKey.Create("res://Verification/Automated/SceneRegressionTarget.tscn");
    private static readonly ResourceKey MissingKey =
        ResourceKey.Create("res://Verification/Automated/SceneRegressionMissing.tscn");

    private SceneService _service = null!;
    private int _changedEventCount;
    private int _passed;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            SceneTree tree = GetTree();
            Assert(GodotObject.IsInstanceValid(tree.CurrentScene), "回归启动时没有 CurrentScene");
            Reparent(tree.Root);

            _service = new SceneService { Name = "SceneServiceUnderTest" };
            tree.Root.AddChild(_service);
            EventChannel.On<FrameworkMainSceneChangedEvent>(OnMainSceneChanged);

            await RunAsync("资源失败保留旧场景", VerifyMissingSceneFailure);
            await RunAsync("并发拒绝与成功提交", VerifyConcurrentRequestAndSuccess);
            await RunAsync("离树立即取消并可重新进入", VerifyExitCancellation);
            await RunAsync("挂载期取消保留旧场景", VerifyMountCancellation);
            await RunAsync("取消后恢复切换", VerifyRecoveryAfterCancellation);

            GD.Print($"[SceneServiceRegression] PASS ({_passed}/5)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SceneServiceRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            SceneRegressionTarget.ReadyAction = null;
            EventChannel.Off<FrameworkMainSceneChangedEvent>(OnMainSceneChanged);

            if (GodotObject.IsInstanceValid(_service))
            {
                if (_service.IsInsideTree())
                    _service.GetParent()?.RemoveChild(_service);

                _service.QueueFree();
            }
        }
    }

    private async Task RunAsync(string name, Func<Task> verification)
    {
        await verification();
        _passed++;
        GD.Print($"[SceneServiceRegression] PASS: {name}");
    }

    private async Task VerifyMissingSceneFailure()
    {
        Node? oldScene = GetTree().CurrentScene;
        SceneChangeException exception = await AssertThrowsAsync<SceneChangeException>(
            () => _service.ChangeAsync(MissingKey),
            "缺失场景没有抛出 SceneChangeException");

        AssertEqual(MissingKey, exception.Key, "缺失场景异常 Key 错误");
        Assert(ReferenceEquals(oldScene, GetTree().CurrentScene), "资源失败后 CurrentScene 发生变化");
        Assert(!_service.IsChanging, "资源失败后仍处于切换状态");
        AssertEqual(0f, _service.Progress, "资源失败后进度没有复位");
    }

    private async Task VerifyConcurrentRequestAndSuccess()
    {
        Node? oldScene = GetTree().CurrentScene;
        Task<Node> firstChange = _service.ChangeAsync(TargetKey);

        Assert(_service.IsChanging, "首个请求启动后未进入切换状态");
        await AssertThrowsAsync<InvalidOperationException>(
            () => _service.ChangeAsync(TargetKey),
            "并发场景请求没有被拒绝");

        Node newScene = await firstChange;
        Assert(ReferenceEquals(newScene, GetTree().CurrentScene), "成功后 CurrentScene 不是新场景");
        Assert(
            GodotObject.IsInstanceValid(oldScene) && oldScene!.IsQueuedForDeletion(),
            "成功后旧场景没有进入删除队列");
        Assert(!_service.IsChanging, "成功后仍处于切换状态");
        AssertEqual(1f, _service.Progress, "成功后进度不是 100%");
        AssertEqual(1, _changedEventCount, "成功切换事件次数错误");
    }

    private async Task VerifyExitCancellation()
    {
        SceneTree tree = GetTree();
        Node? oldScene = tree.CurrentScene;
        Node runtime = tree.Root.GetNode<Node>("GoDoRuntime");
        bool runtimeWasProcessing = runtime.IsProcessing();

        runtime.SetProcess(false);
        try
        {
            Task<Node> change = _service.ChangeAsync(TargetKey);
            tree.Root.RemoveChild(_service);
            tree.Root.AddChild(_service);

            await Task.Yield();
            Assert(change.IsCompleted, "SceneService 离树后没有立即结束等待");

            SceneChangeException exception = await AssertThrowsAsync<SceneChangeException>(
                () => change,
                "SceneService 离树后切换没有取消");
            Assert(
                exception.InnerException is OperationCanceledException,
                "离树取消没有保留直接的 OperationCanceledException");
        }
        finally
        {
            runtime.SetProcess(runtimeWasProcessing);
        }

        Assert(ReferenceEquals(oldScene, tree.CurrentScene), "离树取消后 CurrentScene 发生变化");
        Assert(!_service.IsChanging, "离树取消后仍处于切换状态");
        AssertEqual(0f, _service.Progress, "离树取消后进度没有复位");
    }

    private async Task VerifyMountCancellation()
    {
        SceneTree tree = GetTree();
        Node? oldScene = tree.CurrentScene;
        SceneRegressionTarget.ReadyAction = () => tree.Root.RemoveChild(_service);

        SceneChangeException exception;
        try
        {
            exception = await AssertThrowsAsync<SceneChangeException>(
                () => _service.ChangeAsync(TargetKey),
                "挂载期生命周期变化没有取消切换");
        }
        finally
        {
            SceneRegressionTarget.ReadyAction = null;
            if (!_service.IsInsideTree())
                tree.Root.AddChild(_service);
        }

        Assert(
            exception.InnerException is OperationCanceledException,
            "挂载期取消没有保留直接的 OperationCanceledException");
        Assert(ReferenceEquals(oldScene, tree.CurrentScene), "挂载期取消后 CurrentScene 发生变化");
        AssertEqual(1, _changedEventCount, "挂载期取消错误发送了切换事件");
        Assert(!_service.IsChanging, "挂载期取消后仍处于切换状态");
        AssertEqual(0f, _service.Progress, "挂载期取消后进度没有复位");
    }

    private async Task VerifyRecoveryAfterCancellation()
    {
        Node scene = await _service.ChangeAsync(TargetKey);

        Assert(ReferenceEquals(scene, GetTree().CurrentScene), "取消后恢复切换没有提交新场景");
        Assert(!_service.IsChanging, "恢复切换后仍处于切换状态");
        AssertEqual(1f, _service.Progress, "恢复切换后进度不是 100%");
        AssertEqual(2, _changedEventCount, "恢复切换事件次数错误");
    }

    private void OnMainSceneChanged(FrameworkMainSceneChangedEvent _)
    {
        _changedEventCount++;
    }

    private static async Task<TException> AssertThrowsAsync<TException>(
        Func<Task> action,
        string message)
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}；期望 {expected}，实际 {actual}");
        }
    }
}
