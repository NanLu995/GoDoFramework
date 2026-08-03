using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>分离 UI 资源加载、实例化、首次入树与缓存重开的性能基准。</summary>
public sealed partial class UiFirstOpenBenchmark : Node
{
    private const int InstantiateSampleCount = 10;
    private const int ResidencySampleCount = 20;
    private const int ReopenSampleCount = 20;

    private static readonly ResourceKey FixtureKey =
        ResourceKey.Create("res://Verification/Performance/UiPerformanceFixture.tscn");
    private static readonly ResourceKey ConfigKey =
        ResourceKey.Create("res://Verification/Performance/UiPerformanceConfig.tres");
    private static readonly UiId PerformanceId = UiId.Create("performance");

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            WarmUpMeasurementApi();
            PackedScene scene = BenchmarkResourceLoad();
            BenchmarkInstantiation(scene);
            await BenchmarkFirstTreeEntry(scene);
            await BenchmarkResidencyStrategies(scene);
            await BenchmarkUiService();
            GD.Print(
                $"[UiFirstOpenBenchmark] PASS; Build={BuildConfiguration}; " +
                $"Rows={UiPerformanceFixture.RowCount}; Nodes={ExpectedNodeCount}; " +
                $"Processors={System.Environment.ProcessorCount}; OS={OS.GetName()}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[UiFirstOpenBenchmark] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static int ExpectedNodeCount => 2 + UiPerformanceFixture.RowCount * 3;

    private static void WarmUpMeasurementApi()
    {
        for (int index = 0; index < 10; index++)
            _ = Stopwatch.GetTimestamp();
    }

    private static PackedScene BenchmarkResourceLoad()
    {
        long started = Stopwatch.GetTimestamp();
        PackedScene scene = ResourceHub.Load<PackedScene>(FixtureKey);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        Assert(scene.CanInstantiate(), "性能 fixture 无法实例化");
        GD.Print(
            $"[UiFirstOpenBenchmark] ResourceLoad: ElapsedMs={elapsed.TotalMilliseconds:F3}");
        return scene;
    }

    private static void BenchmarkInstantiation(PackedScene scene)
    {
        UiPerformanceFixture first = Instantiate(scene);
        first.Free();

        long totalTicks = 0;
        for (int index = 0; index < InstantiateSampleCount; index++)
        {
            long started = Stopwatch.GetTimestamp();
            UiPerformanceFixture instance = Instantiate(scene);
            long finished = Stopwatch.GetTimestamp();
            totalTicks += finished - started;
            instance.Free();
        }

        double averageMilliseconds =
            totalTicks * 1000d / Stopwatch.Frequency / InstantiateSampleCount;
        GD.Print(
            $"[UiFirstOpenBenchmark] Instantiate: Samples={InstantiateSampleCount}; " +
            $"AverageMs={averageMilliseconds:F3}");
    }

    private async Task BenchmarkFirstTreeEntry(PackedScene scene)
    {
        UiPerformanceFixture instance = Instantiate(scene);
        long started = Stopwatch.GetTimestamp();
        AddChild(instance);
        TimeSpan synchronousElapsed = Stopwatch.GetElapsedTime(started);

        started = Stopwatch.GetTimestamp();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        TimeSpan firstFrameElapsed = Stopwatch.GetElapsedTime(started);

        Assert(instance.ReadyCount == 1, "首次入树没有执行一次 _Ready");
        Assert(instance.EnterTreeCount == 1, "首次入树没有执行一次 _EnterTree");
        GD.Print(
            $"[UiFirstOpenBenchmark] FirstTreeEntry: " +
            $"SynchronousMs={synchronousElapsed.TotalMilliseconds:F3}; " +
            $"FirstFrameWaitMs={firstFrameElapsed.TotalMilliseconds:F3}");

        instance.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task BenchmarkUiService()
    {
        IUiService ui = Services.Get<IUiService>();
        ui.LoadUiConfig(ConfigKey);

        long started = Stopwatch.GetTimestamp();
        UiPerformanceFixture first = ui.Open<UiPerformanceFixture>(PerformanceId);
        TimeSpan firstOpenElapsed = Stopwatch.GetElapsedTime(started);
        Assert(first.ReadyCount == 1 && first.AcquireCount == 1,
            "UiService 首次打开生命周期错误");

        ui.Close(first);
        Assert(ui.HasCachedInstance(PerformanceId), "关闭后没有生成复用缓存");

        started = Stopwatch.GetTimestamp();
        UiPerformanceFixture reopened = ui.Open<UiPerformanceFixture>(PerformanceId);
        TimeSpan firstReopenElapsed = Stopwatch.GetElapsedTime(started);
        Assert(ReferenceEquals(first, reopened), "缓存重开没有复用原实例");
        Assert(reopened.ReadyCount == 1 && reopened.EnterTreeCount == 2,
            "缓存重开错误执行 _Ready 或没有重新入树");
        ui.Close(reopened);

        long totalTicks = 0;
        for (int index = 0; index < ReopenSampleCount; index++)
        {
            started = Stopwatch.GetTimestamp();
            UiPerformanceFixture instance = ui.Open<UiPerformanceFixture>(PerformanceId);
            long finished = Stopwatch.GetTimestamp();
            totalTicks += finished - started;
            ui.Close(instance);
        }

        double averageReopenMilliseconds =
            totalTicks * 1000d / Stopwatch.Frequency / ReopenSampleCount;
        GD.Print(
            $"[UiFirstOpenBenchmark] UiService: " +
            $"FirstOpenMs={firstOpenElapsed.TotalMilliseconds:F3}; " +
            $"FirstCachedReopenMs={firstReopenElapsed.TotalMilliseconds:F3}; " +
            $"CachedReopenSamples={ReopenSampleCount}; " +
            $"CachedReopenAverageMs={averageReopenMilliseconds:F3}");

        Assert(ui.ClearCachedInstance(PerformanceId), "基准结束时没有清理缓存实例");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Assert(!GodotObject.IsInstanceValid(first), "基准缓存实例没有释放");
    }

    private async Task BenchmarkResidencyStrategies(PackedScene scene)
    {
        var activeRoot = new Control { Name = "ActiveRoot" };
        var cacheRoot = new Control
        {
            Name = "CacheRoot",
            Visible = false,
            ProcessMode = ProcessModeEnum.Disabled
        };
        AddChild(activeRoot);
        AddChild(cacheRoot);

        UiPerformanceFixture detached = Instantiate(scene);
        activeRoot.AddChild(detached);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        long detachedTicks = 0;
        for (int index = 0; index < ResidencySampleCount; index++)
        {
            activeRoot.RemoveChild(detached);
            long started = Stopwatch.GetTimestamp();
            activeRoot.AddChild(detached);
            detachedTicks += Stopwatch.GetTimestamp() - started;
        }

        Assert(detached.ReadyCount == 1, "脱树重挂错误地重复执行 _Ready");
        Assert(
            detached.EnterTreeCount == ResidencySampleCount + 1 &&
            detached.ExitTreeCount == ResidencySampleCount,
            "脱树重挂没有产生预期的出入树生命周期");

        UiPerformanceFixture hidden = Instantiate(scene);
        activeRoot.AddChild(hidden);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        int hiddenProcessCount = hidden.ProcessCount;
        hidden.Hide();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Assert(hidden.ProcessCount > hiddenProcessCount,
            "隐藏驻留 UI 意外停止了 _Process，实验无法反映后台处理风险");

        long showTicks = 0;
        for (int index = 0; index < ResidencySampleCount; index++)
        {
            hidden.Hide();
            long started = Stopwatch.GetTimestamp();
            hidden.Show();
            showTicks += Stopwatch.GetTimestamp() - started;
        }

        Assert(
            hidden.ReadyCount == 1 &&
            hidden.EnterTreeCount == 1 &&
            hidden.ExitTreeCount == 0,
            "原父节点隐藏显示错误改变了场景树生命周期");

        UiPerformanceFixture reparented = Instantiate(scene);
        activeRoot.AddChild(reparented);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        int reparentEnterCount = reparented.EnterTreeCount;
        int reparentExitCount = reparented.ExitTreeCount;

        long reparentTicks = 0;
        for (int index = 0; index < ResidencySampleCount; index++)
        {
            reparented.Reparent(cacheRoot, keepGlobalTransform: false);
            Assert(
                reparented.IsInsideTree() &&
                !reparented.IsVisibleInTree() &&
                !reparented.CanProcess(),
                "隐藏缓存根没有同时保持入树、隐藏并停止处理");

            long started = Stopwatch.GetTimestamp();
            reparented.Reparent(activeRoot, keepGlobalTransform: false);
            reparentTicks += Stopwatch.GetTimestamp() - started;
        }

        Assert(
            reparented.ReadyCount == 1,
            "同一 SceneTree 内 Reparent 错误地重复执行 _Ready");
        Assert(
            reparented.EnterTreeCount ==
                reparentEnterCount + ResidencySampleCount * 2 &&
            reparented.ExitTreeCount ==
                reparentExitCount + ResidencySampleCount * 2,
            "同一 SceneTree 内 Reparent 没有产生预期的出入树生命周期");
        Assert(reparented.CanProcess() && reparented.IsVisibleInTree(),
            "从隐藏缓存根恢复后没有恢复处理或可见状态");

        GD.Print(
            $"[UiFirstOpenBenchmark] Residency: Samples={ResidencySampleCount}; " +
            $"DetachedReattachAverageMs={ToAverageMilliseconds(detachedTicks, ResidencySampleCount):F3}; " +
            $"SameParentShowAverageMs={ToAverageMilliseconds(showTicks, ResidencySampleCount):F3}; " +
            $"CacheRootReparentAverageMs={ToAverageMilliseconds(reparentTicks, ResidencySampleCount):F3}; " +
            $"HiddenContinuedProcessing=true; " +
            $"ReparentPreservedReady=true; ReparentTriggeredTreeLifecycle=true");

        activeRoot.QueueFree();
        cacheRoot.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static UiPerformanceFixture Instantiate(PackedScene scene)
    {
        Node node = scene.Instantiate();
        if (node is UiPerformanceFixture fixture)
        {
            Assert(CountNodes(fixture) == ExpectedNodeCount,
                "性能 fixture 节点数量不符合预期");
            return fixture;
        }

        node.Free();
        throw new InvalidOperationException("性能 fixture 根节点类型错误");
    }

    private static int CountNodes(Node node)
    {
        int count = 1;
        for (int index = 0; index < node.GetChildCount(); index++)
            count += CountNodes(node.GetChild(index));
        return count;
    }

    private static double ToAverageMilliseconds(long ticks, int sampleCount) =>
        ticks * 1000d / Stopwatch.Frequency / sampleCount;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
