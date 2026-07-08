using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>ResourceHub 的无交互回归验证入口。</summary>
public sealed partial class ResourceHubRegression : Node
{
    private static readonly ResourceKey ValidKey =
        ResourceKey.Create("res://resources/ConfigTestValid.tres");
    private static readonly ResourceKey MissingKey =
        ResourceKey.Create("res://resources/ResourceHubMissing.tres");

    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        RunAsync();
    }

    private async void RunAsync()
    {
        try
        {
            Run("同步加载与类型检查", VerifySynchronousLoad);
            Run("无效与缺失资源失败语义", VerifyInvalidAndMissingKeys);
            Run("同步类型不匹配", VerifySynchronousTypeMismatch);
            await RunAsync("异步合并、冲突与完成", VerifyAsyncLoading);
            await RunAsync("完成后可创建新操作", VerifyOperationCleanup);

            GD.Print($"[ResourceHubRegression] PASS ({_passed}/5)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[ResourceHubRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[ResourceHubRegression] PASS: {name}");
    }

    private async Task RunAsync(string name, Func<Task> verification)
    {
        await verification();
        _passed++;
        GD.Print($"[ResourceHubRegression] PASS: {name}");
    }

    private static void VerifySynchronousLoad()
    {
        ConfigTestResource resource = ResourceHub.Load<ConfigTestResource>(ValidKey);
        AssertEqual("valid", resource.Id, "同步加载资源 Id 错误");
        AssertEqual(42, resource.Value, "同步加载资源 Value 错误");
    }

    private static void VerifyInvalidAndMissingKeys()
    {
        AssertThrows<ArgumentException>(
            static () => ResourceHub.Load<Resource>(default),
            "默认 ResourceKey 没有被拒绝");

        ResourceLoadException missing = AssertThrows<ResourceLoadException>(
            static () => ResourceHub.Load<Resource>(MissingKey),
            "缺失资源没有抛出 ResourceLoadException");
        AssertEqual(MissingKey, missing.Key, "缺失资源异常 Key 错误");
        AssertEqual(typeof(Resource), missing.RequestedType, "缺失资源异常请求类型错误");
    }

    private static void VerifySynchronousTypeMismatch()
    {
        ResourceLoadException mismatch = AssertThrows<ResourceLoadException>(
            static () => ResourceHub.Load<PackedScene>(ValidKey),
            "同步类型不匹配没有抛出 ResourceLoadException");
        AssertEqual(ValidKey, mismatch.Key, "类型不匹配异常 Key 错误");
        AssertEqual(typeof(PackedScene), mismatch.RequestedType, "类型不匹配异常请求类型错误");
    }

    private async Task VerifyAsyncLoading()
    {
        float lastProgress = 0f;
        void OnProgress(float progress) => lastProgress = progress;

        ResourceLoadOperation<ConfigTestResource> first =
            ResourceHub.LoadAsync<ConfigTestResource>(ValidKey);
        first.ProgressChanged += OnProgress;
        try
        {
            ResourceLoadOperation<ConfigTestResource> second =
                ResourceHub.LoadAsync<ConfigTestResource>(ValidKey);
            Assert(ReferenceEquals(first, second), "相同异步请求没有合并为同一操作");
            AssertEqual(1, ResourceHub.ActiveOperationCount, "合并后活动操作数量错误");

            AssertThrows<ResourceLoadException>(
                static () => ResourceHub.LoadAsync<PackedScene>(ValidKey),
                "同路径不同类型异步请求没有失败");
            AssertThrows<InvalidOperationException>(
                static () => ResourceHub.Load<ConfigTestResource>(ValidKey),
                "异步期间同步加载没有失败");

            ConfigTestResource resource = await first.Completion;
            AssertEqual(ResourceLoadStatus.Completed, first.Status, "异步操作完成状态错误");
            AssertEqual(1f, first.Progress, "异步操作最终进度不为 1");
            AssertEqual(1f, lastProgress, "进度监听者没有收到最终进度");
            AssertEqual(42, resource.Value, "异步加载资源内容错误");
        }
        finally
        {
            first.ProgressChanged -= OnProgress;
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        AssertEqual(0, ResourceHub.ActiveOperationCount, "完成操作没有从活动表移除");
    }

    private async Task VerifyOperationCleanup()
    {
        ResourceLoadOperation<ConfigTestResource> operation =
            ResourceHub.LoadAsync<ConfigTestResource>(ValidKey);
        ConfigTestResource resource = await operation.Completion;

        AssertEqual(42, resource.Value, "后续异步加载资源内容错误");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        AssertEqual(0, ResourceHub.ActiveOperationCount, "后续操作完成后没有清理");
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

    private static TException AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }
}
