using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate.Verification;

/// <summary>Measures real Starter UI cold phases, cached reopen behavior, and a Multiple Toast burst.</summary>
public sealed partial class StarterUiPerformanceBenchmark : Node
{
    private const int InstantiateSampleCount = 15;
    private const int ReopenSampleCount = 25;
    private const int ToastBurstCount = 32;

    private static readonly ResourceKey BenchmarkConfigKey =
        ResourceKey.Create("res://Verification/Performance/StarterUiPerformanceConfig.tres");
    private static readonly UiId SettingsReuseId = UiId.Create("benchmark/settings_reuse");
    private static readonly UiId ToastBurstId = UiId.Create("benchmark/toast_burst");

    private static readonly SceneCase[] SceneCases =
    {
        new("MainMenu", ResourceKey.Create("res://MainMenu/MainMenuView.tscn"), null),
        new("Settings", ResourceKey.Create("res://Settings/SettingsView.tscn"), ConfigureSettings),
        new("GameplayHud", ResourceKey.Create("res://Gameplay/GameplayHud.tscn"), null),
        new("Pause", ResourceKey.Create("res://Ui/PauseModal.tscn"), null),
        new("Confirm", ResourceKey.Create("res://Ui/ConfirmDialogModal.tscn"), ConfigureConfirm),
        new("Loading", ResourceKey.Create("res://Ui/LoadingOverlay.tscn"), ConfigureLoading),
        new("Toast", ResourceKey.Create("res://Ui/ToastOverlay.tscn"), ConfigureToast),
    };

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
            PrintEnvironment();
            await BenchmarkRealScenePhases();

            IUiService ui = Services.Get<IUiService>();
            ui.LoadUiConfig(BenchmarkConfigKey);
            await BenchmarkCachedSettings(ui);
            await BenchmarkToastBurst(ui);

            GD.Print($"[StarterUiPerformance] PASS; Build={BuildConfiguration}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[StarterUiPerformance] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task BenchmarkRealScenePhases()
    {
        foreach (SceneCase sceneCase in SceneCases)
        {
            Measurement<PackedScene> load = Measure(() => ResourceHub.Load<PackedScene>(sceneCase.Key));
            Assert(load.Value.CanInstantiate(), $"{sceneCase.Name} PackedScene cannot instantiate");

            Measurement<Control> instantiate = Measure(() => InstantiateControl(load.Value, sceneCase.Name));
            Control instance = instantiate.Value;
            Measurement configure = sceneCase.Configure is null
                ? default
                : Measure(() => sceneCase.Configure(instance));
            int nodeCount = CountNodes(instance);
            Measurement mount = Measure(() => AddChild(instance));

            long frameStarted = Stopwatch.GetTimestamp();
            long frameAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            TimeSpan firstFrame = Stopwatch.GetElapsedTime(frameStarted);
            long frameAllocated = GC.GetAllocatedBytesForCurrentThread() - frameAllocatedBefore;

            GD.Print(
                $"[StarterUiPerformance] ColdScene; Name={sceneCase.Name}; Nodes={nodeCount}; " +
                $"ResourceLoadMs={load.ElapsedMilliseconds:F3}; ResourceLoadBytes={load.AllocatedBytes}; " +
                $"InstantiateMs={instantiate.ElapsedMilliseconds:F3}; InstantiateBytes={instantiate.AllocatedBytes}; " +
                $"ConfigureMs={configure.ElapsedMilliseconds:F3}; ConfigureBytes={configure.AllocatedBytes}; " +
                $"MountReadyMs={mount.ElapsedMilliseconds:F3}; MountReadyBytes={mount.AllocatedBytes}; " +
                $"FirstFrameWaitMs={firstFrame.TotalMilliseconds:F3}; FirstFrameThreadBytes={frameAllocated}");

            instance.Free();
            BenchmarkWarmInstantiation(sceneCase.Name, load.Value);
        }
    }

    private static void BenchmarkWarmInstantiation(string name, PackedScene scene)
    {
        var samples = new double[InstantiateSampleCount];
        var allocations = new long[InstantiateSampleCount];
        for (int index = 0; index < InstantiateSampleCount; index++)
        {
            Measurement<Control> measurement = Measure(() => InstantiateControl(scene, name));
            samples[index] = measurement.ElapsedMilliseconds;
            allocations[index] = measurement.AllocatedBytes;
            measurement.Value.Free();
        }

        PrintDistribution("WarmInstantiate", name, samples, allocations);
    }

    private async Task BenchmarkCachedSettings(IUiService ui)
    {
        MemorySnapshot before = await CaptureMemorySnapshot();
        Measurement<SettingsView> firstOpen = Measure(() =>
            ui.Open<SettingsView>(SettingsReuseId, ConfigureSettings));
        SettingsView cachedView = firstOpen.Value;
        int cachedNodeCount = CountNodes(cachedView);
        Measurement firstClose = Measure(() => ui.Close(cachedView));
        Assert(ui.HasCachedInstance(SettingsReuseId), "Settings close did not create a reuse cache");
        MemorySnapshot cached = await CaptureMemorySnapshot();

        var samples = new double[ReopenSampleCount];
        var allocations = new long[ReopenSampleCount];
        for (int index = 0; index < ReopenSampleCount; index++)
        {
            Measurement<SettingsView> reopen = Measure(() =>
                ui.Open<SettingsView>(SettingsReuseId, ConfigureSettings));
            samples[index] = reopen.ElapsedMilliseconds;
            allocations[index] = reopen.AllocatedBytes;
            Assert(ReferenceEquals(cachedView, reopen.Value), "Cached settings reopen created another instance");
            if (index == 0)
                VerifyReopenedSettingsSignals(reopen.Value);
            ui.Close(reopen.Value);
        }

        PrintDistribution("CachedReopen", "Settings", samples, allocations);
        GD.Print(
            $"[StarterUiPerformance] CacheResidency; Name=Settings; Nodes={cachedNodeCount}; " +
            $"FirstOpenMs={firstOpen.ElapsedMilliseconds:F3}; FirstOpenBytes={firstOpen.AllocatedBytes}; " +
            $"FirstCloseMs={firstClose.ElapsedMilliseconds:F3}; FirstCloseBytes={firstClose.AllocatedBytes}; " +
            $"ManagedDeltaBytes={cached.ManagedBytes - before.ManagedBytes}; " +
            $"GodotStaticDeltaBytes={cached.GodotStaticBytes - before.GodotStaticBytes}");

        Assert(ui.ClearCachedInstance(SettingsReuseId), "Settings cache could not be cleared");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Assert(!GodotObject.IsInstanceValid(cachedView), "Cleared settings cache instance remains valid");
        MemorySnapshot cleared = await CaptureMemorySnapshot();
        GD.Print(
            $"[StarterUiPerformance] CacheCleared; Name=Settings; " +
            $"ManagedDeltaFromBaselineBytes={cleared.ManagedBytes - before.ManagedBytes}; " +
            $"GodotStaticDeltaFromBaselineBytes={cleared.GodotStaticBytes - before.GodotStaticBytes}");
    }

    private async Task BenchmarkToastBurst(IUiService ui)
    {
        string[] messages = new string[ToastBurstCount];
        for (int index = 0; index < messages.Length; index++)
            messages[index] = $"Benchmark toast {index:D2}";

        MemorySnapshot before = await CaptureMemorySnapshot();
        long started = Stopwatch.GetTimestamp();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < ToastBurstCount; index++)
        {
            int stackIndex = index;
            ui.Open<ToastOverlay>(
                ToastBurstId,
                toast =>
                {
                    toast.GetNode<Timer>(toast.DismissTimerPath).WaitTime = 60.0;
                    toast.Show(messages[stackIndex], stackIndex);
                });
        }

        TimeSpan openElapsed = Stopwatch.GetElapsedTime(started);
        long openAllocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert(ui.GetOpenCount(ToastBurstId) == ToastBurstCount, "Toast burst open count is incorrect");

        started = Stopwatch.GetTimestamp();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        TimeSpan firstFrame = Stopwatch.GetElapsedTime(started);
        MemorySnapshot peak = await CaptureMemorySnapshot();

        Measurement close = Measure(() =>
        {
            int closed = ui.CloseAll(ToastBurstId);
            Assert(closed == ToastBurstCount, "Toast burst close count is incorrect");
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Assert(ui.GetOpenCount(ToastBurstId) == 0, "Toast burst left open instances");
        MemorySnapshot cleared = await CaptureMemorySnapshot();

        GD.Print(
            $"[StarterUiPerformance] ToastBurst; Count={ToastBurstCount}; " +
            $"OpenTotalMs={openElapsed.TotalMilliseconds:F3}; OpenThreadBytes={openAllocated}; " +
            $"FirstFrameWaitMs={firstFrame.TotalMilliseconds:F3}; " +
            $"CloseTotalMs={close.ElapsedMilliseconds:F3}; CloseThreadBytes={close.AllocatedBytes}; " +
            $"PeakManagedDeltaBytes={peak.ManagedBytes - before.ManagedBytes}; " +
            $"PeakGodotStaticDeltaBytes={peak.GodotStaticBytes - before.GodotStaticBytes}; " +
            $"ClearedManagedDeltaBytes={cleared.ManagedBytes - before.ManagedBytes}; " +
            $"ClearedGodotStaticDeltaBytes={cleared.GodotStaticBytes - before.GodotStaticBytes}");
    }

    private async Task<MemorySnapshot> CaptureMemorySnapshot()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return new MemorySnapshot(
            GC.GetTotalMemory(forceFullCollection: false),
            (long)OS.GetStaticMemoryUsage());
    }

    private static void PrintDistribution(
        string category,
        string name,
        double[] elapsedSamples,
        long[] allocationSamples)
    {
        Array.Sort(elapsedSamples);
        Array.Sort(allocationSamples);
        GD.Print(
            $"[StarterUiPerformance] {category}; Name={name}; Samples={elapsedSamples.Length}; " +
            $"AverageMs={Average(elapsedSamples):F3}; P50Ms={Percentile(elapsedSamples, 0.50):F3}; " +
            $"P95Ms={Percentile(elapsedSamples, 0.95):F3}; " +
            $"AverageThreadBytes={Average(allocationSamples):F0}; " +
            $"P95ThreadBytes={Percentile(allocationSamples, 0.95):F0}");
    }

    private static void PrintEnvironment()
    {
        GD.Print(
            $"[StarterUiPerformance] Environment; Build={BuildConfiguration}; Engine={Engine.GetVersionInfo()}; " +
            $"OS={OS.GetName()}; Model={OS.GetModelName()}; Processor={OS.GetProcessorName()}; " +
            $"LogicalProcessors={System.Environment.ProcessorCount}; DisplayServer={DisplayServer.GetName()}");
    }

    private static void WarmUpMeasurementApi()
    {
        for (int index = 0; index < 16; index++)
        {
            _ = Stopwatch.GetTimestamp();
            _ = GC.GetAllocatedBytesForCurrentThread();
        }
    }

    private static Measurement Measure(Action action)
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        action();
        long finished = Stopwatch.GetTimestamp();
        return new Measurement(
            (finished - started) * 1000d / Stopwatch.Frequency,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    }

    private static Measurement<T> Measure<T>(Func<T> action)
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        T value = action();
        long finished = Stopwatch.GetTimestamp();
        return new Measurement<T>(
            value,
            (finished - started) * 1000d / Stopwatch.Frequency,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    }

    private static Control InstantiateControl(PackedScene scene, string name)
    {
        Node node = scene.Instantiate();
        if (node is Control control)
            return control;

        node.Free();
        throw new InvalidOperationException($"{name} root must inherit Control");
    }

    private static int CountNodes(Node node)
    {
        int count = 1;
        for (int index = 0; index < node.GetChildCount(); index++)
            count += CountNodes(node.GetChild(index));
        return count;
    }

    private static void ConfigureSettings(Control control) => ((SettingsView)control).Refresh();

    private static void VerifyReopenedSettingsSignals(SettingsView settings)
    {
        ISettingsService service = Services.Get<ISettingsService>();
        float expected = service.Current.MasterVolume > 0.5f ? 0.25f : 0.75f;
        settings.GetNode<HSlider>(settings.MasterSliderPath).Value = expected;
        Assert(Mathf.IsEqualApprox(service.Current.MasterVolume, expected),
            "Cached settings reopen did not restore slider signal subscriptions");
    }

    private static void ConfigureConfirm(Control control) =>
        ((ConfirmDialogModal)control).SetMessage("Return to the main menu?");

    private static void ConfigureLoading(Control control) => ((LoadingOverlay)control).SetProgress(0.42f);

    private static void ConfigureToast(Control control)
    {
        var toast = (ToastOverlay)control;
        toast.GetNode<Timer>(toast.DismissTimerPath).WaitTime = 60.0;
        toast.Show("Benchmark toast", 0);
    }

    private static double Average(double[] values)
    {
        double total = 0.0;
        for (int index = 0; index < values.Length; index++)
            total += values[index];
        return total / values.Length;
    }

    private static double Average(long[] values)
    {
        long total = 0;
        for (int index = 0; index < values.Length; index++)
            total += values[index];
        return (double)total / values.Length;
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        int index = Math.Clamp((int)Math.Ceiling(sortedValues.Length * percentile) - 1, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }

    private static double Percentile(long[] sortedValues, double percentile)
    {
        int index = Math.Clamp((int)Math.Ceiling(sortedValues.Length * percentile) - 1, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly record struct SceneCase(string Name, ResourceKey Key, Action<Control>? Configure);

    private readonly record struct Measurement(double ElapsedMilliseconds, long AllocatedBytes);

    private readonly record struct Measurement<T>(T Value, double ElapsedMilliseconds, long AllocatedBytes);

    private readonly record struct MemorySnapshot(long ManagedBytes, long GodotStaticBytes);
}
