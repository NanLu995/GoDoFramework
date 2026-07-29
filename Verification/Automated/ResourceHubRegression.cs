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
        ResourceKey.Create("res://Verification/Automated/Fixtures/ConfigTestValid.tres");
    private static readonly ResourceKey MissingKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/ResourceHubMissing.tres");

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
            await RunAsync("异步失败发布前清理", VerifyAsyncFailureCleanup);
#if DEBUG
            await RunAsync("错误线程拒绝不污染诊断", VerifyWrongThreadDiagnostics);
            Run("Debug 资源诊断快照", VerifyDebugDiagnostics);
#endif
            await RunAsync("Shutdown 取消与重新初始化", VerifyShutdownCancellation);

            GD.Print($"[ResourceHubRegression] PASS ({_passed}/{_passed})");
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
        int survivingProgressNotifications = 0;
        void OnProgress(float progress) => lastProgress = progress;
        void OnThrowingProgress(float _) =>
            throw new InvalidOperationException("ResourceHub progress listener test");
        void OnSurvivingProgress(float _) => survivingProgressNotifications++;

        ResourceLoadOperation<ConfigTestResource> first =
            ResourceHub.LoadAsync<ConfigTestResource>(ValidKey);
        first.ProgressChanged += OnProgress;
        first.ProgressChanged += OnThrowingProgress;
        first.ProgressChanged += OnSurvivingProgress;
        try
        {
            ResourceLoadOperation<ConfigTestResource> second =
                ResourceHub.LoadAsync<ConfigTestResource>(ValidKey);
            Assert(ReferenceEquals(first, second), "相同异步请求没有合并为同一操作");
            AssertEqual(1, ResourceHub.ActiveOperationCount, "合并后活动操作数量错误");
#if DEBUG
            ResourceDebugSnapshot activeSnapshot = ResourceHub.GetDebugSnapshot();
            AssertEqual(1, activeSnapshot.ActiveOperations.Length, "活动请求快照数量错误");
            AssertEqual(2, activeSnapshot.ActiveOperations[0].MergedRequestCount,
                "同键合并请求数没有写入快照");
#endif

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
            Assert(survivingProgressNotifications > 0,
                "进度监听者异常阻断了后续监听者");
            AssertEqual(42, resource.Value, "异步加载资源内容错误");
        }
        finally
        {
            first.ProgressChanged -= OnProgress;
            first.ProgressChanged -= OnThrowingProgress;
            first.ProgressChanged -= OnSurvivingProgress;
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
        AssertEqual(0, ResourceHub.ActiveOperationCount,
            "await 恢复前异步操作尚未从活动表移除");

        ResourceLoadOperation<ConfigTestResource> nextOperation =
            ResourceHub.LoadAsync<ConfigTestResource>(ValidKey);
        Assert(!ReferenceEquals(operation, nextOperation),
            "await 恢复后立即重载仍返回已完成的旧操作");
        await nextOperation.Completion;
        AssertEqual(0, ResourceHub.ActiveOperationCount,
            "立即重载完成后异步操作尚未从活动表移除");
    }

    private static async Task VerifyAsyncFailureCleanup()
    {
        ResourceLoadOperation<PackedScene> operation =
            ResourceHub.LoadAsync<PackedScene>(ValidKey);
        try
        {
            await operation.Completion;
            throw new InvalidOperationException("异步类型不匹配没有抛出 ResourceLoadException");
        }
        catch (ResourceLoadException exception)
        {
            AssertEqual(ValidKey, exception.Key, "异步类型不匹配异常 Key 错误");
            AssertEqual(typeof(PackedScene), exception.RequestedType,
                "异步类型不匹配异常请求类型错误");
        }

        AssertEqual(ResourceLoadStatus.Failed, operation.Status,
            "异步类型不匹配后的操作状态错误");
        AssertEqual(0, ResourceHub.ActiveOperationCount,
            "异步失败恢复 await 前操作尚未从活动表移除");
    }

#if DEBUG
    private static async Task VerifyWrongThreadDiagnostics()
    {
        ResourceDebugSnapshot before = ResourceHub.GetDebugSnapshot();
        Exception? failure = await Task.Run(() =>
        {
            try
            {
                ResourceHub.Load<ConfigTestResource>(ValidKey);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });
        ResourceDebugSnapshot after = ResourceHub.GetDebugSnapshot();

        Assert(failure is InvalidOperationException, "错误线程调用没有被主线程守卫拒绝");
        AssertEqual(before.SynchronousRequestCount, after.SynchronousRequestCount,
            "错误线程调用污染了同步请求统计");
        AssertEqual(before.FailedRequestCount, after.FailedRequestCount,
            "错误线程调用污染了失败请求统计");
        AssertEqual(before.History.Length, after.History.Length,
            "错误线程调用污染了资源诊断历史");
    }

    private static void VerifyDebugDiagnostics()
    {
        ResourceDebugSnapshot snapshot = ResourceHub.GetDebugSnapshot();
        AssertEqual(0, snapshot.ActiveOperations.Length, "完成后诊断快照仍保留活动请求");
        Assert(snapshot.SynchronousRequestCount >= 4 &&
            snapshot.AsynchronousRequestCount >= 4 &&
            snapshot.MergedRequestCount >= 1 &&
            snapshot.SucceededRequestCount >= 4 &&
            snapshot.FailedRequestCount >= 4 &&
            snapshot.History.Length > 0 &&
            snapshot.History.Length <= 32,
            "资源诊断统计或固定容量历史错误");
        Assert(Array.Exists(snapshot.History, entry =>
                entry.Mode == ResourceDebugLoadMode.Synchronous &&
                entry.Status == ResourceLoadStatus.Completed) &&
            Array.Exists(snapshot.History, entry =>
                entry.Mode == ResourceDebugLoadMode.Asynchronous &&
                entry.MergedRequestCount >= 2),
            "资源诊断历史没有保留同步成功或异步合并条目");

        for (int index = 0; index < 33; index++)
        {
            ResourceKey missingKey = ResourceKey.Create(
                $"res://Verification/Automated/Fixtures/ResourceHubMissing{index}.tres");
            AssertThrows<ResourceLoadException>(
                () => ResourceHub.Load<Resource>(missingKey),
                $"第 {index} 个容量测试资源没有失败");
        }

        snapshot = ResourceHub.GetDebugSnapshot();
        AssertEqual(32, snapshot.History.Length, "资源诊断历史没有限制为 32 条");
        AssertEqual(
            "res://Verification/Automated/Fixtures/ResourceHubMissing1.tres",
            snapshot.History[0].Key.Value,
            "资源诊断历史没有淘汰最早记录");
        AssertEqual(
            "res://Verification/Automated/Fixtures/ResourceHubMissing32.tres",
            snapshot.History[^1].Key.Value,
            "资源诊断历史顺序或最新记录错误");
    }
#endif

    private async Task VerifyShutdownCancellation()
    {
        ResourceLoadOperation<ConfigTestResource> operation =
            ResourceHub.LoadAsync<ConfigTestResource>(ValidKey);
        ResourceHub.Shutdown();
        try
        {
            try
            {
                await operation.Completion;
                throw new InvalidOperationException("Shutdown 后等待中的资源操作没有被取消");
            }
            catch (OperationCanceledException)
            {
            }

            // Shutdown 只取消框架等待；测试需取走自己启动的 Godot 底层请求，避免退出残留。
            ResourceLoader.ThreadLoadStatus underlyingStatus;
            do
            {
                underlyingStatus = ResourceLoader.LoadThreadedGetStatus(ValidKey.Value);
                if (underlyingStatus == ResourceLoader.ThreadLoadStatus.InProgress)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            while (underlyingStatus == ResourceLoader.ThreadLoadStatus.InProgress);

            if (underlyingStatus == ResourceLoader.ThreadLoadStatus.Loaded)
                ResourceLoader.LoadThreadedGet(ValidKey.Value);
        }
        finally
        {
            ResourceHub.Initialize();
        }

        AssertEqual(ResourceLoadStatus.Failed, operation.Status,
            "Shutdown 取消后的操作状态错误");
        AssertEqual(0, ResourceHub.ActiveOperationCount,
            "ResourceHub 重新初始化后仍保留旧活动操作");

        ResourceLoadOperation<ConfigTestResource> recoveredOperation =
            ResourceHub.LoadAsync<ConfigTestResource>(ValidKey);
        ConfigTestResource recoveredResource = await recoveredOperation.Completion;
        AssertEqual(42, recoveredResource.Value,
            "ResourceHub 重新初始化后无法继续加载资源");
        AssertEqual(0, ResourceHub.ActiveOperationCount,
            "ResourceHub 恢复加载完成后仍保留活动操作");
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
