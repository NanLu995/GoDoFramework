using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>AudioService 首次播放、池扩容与缓存 SFX 批量播放的可重复性能基准。</summary>
public sealed partial class AudioSfxBenchmark : Node
{
    private const int SampleCount = 25;
    private const int MaximumVoiceCount = 32;
    private const int FollowUpdateIterations = 10_000;

    private static readonly int[] BatchSizes = [1, 8, 32];
    private static readonly ResourceKey AudioKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/Audio/LoopSilence.tres");
    private static readonly ResourceKey AlternateAudioKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/Audio/AlternateLoopSilence.tres");
    private static readonly ResourceKey SfxVoiceKey =
        ResourceKey.Create("res://addons/godo_framework/Runtime/Audio/SfxVoice.tscn");
    private static readonly ResourceKey Sfx3DVoiceKey =
        ResourceKey.Create("res://addons/godo_framework/Runtime/Audio/Sfx3DVoice.tscn");

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
            var listenerCamera = new Camera3D
            {
                Name = "BenchmarkAudioListenerCamera",
                Current = true,
            };
            AddChild(listenerCamera);

            IAudioService audio = Services.Get<IAudioService>();
            Assert(audio.MaxSfxVoices >= MaximumVoiceCount,
                $"AudioService 最大 Voice 数不足 {MaximumVoiceCount}: {audio.MaxSfxVoices}");
            Assert(audio.MaxSfx3DVoices >= MaximumVoiceCount,
                $"AudioService 最大 3D Voice 数不足 {MaximumVoiceCount}: {audio.MaxSfx3DVoices}");

            WarmUpMeasurementApis();
            await BenchmarkFirstPlay(audio);
            await BenchmarkPreparedFirstPlay(audio);
            await BenchmarkPoolGrowth(audio);
            foreach (int batchSize in BatchSizes)
                await BenchmarkCachedBatch(audio, batchSize);
            await BenchmarkPriorityPreemption(audio);
            await BenchmarkVoicePrewarm();
            await BenchmarkSfx3DPrewarmAndBurst(audio);
            await BenchmarkSfx3DFollowUpdates();

            Assert(audio.ActiveSfxCount == 0, "基准结束后仍有活动 SFX Voice");
            Assert(audio.ActiveSfx3DCount == 0, "基准结束后仍有活动 3D SFX Voice");
            GD.Print(
                $"[AudioSfxBenchmark] PASS; Build={BuildConfiguration}; " +
                $"Samples={SampleCount}; Processors={System.Environment.ProcessorCount}; OS={OS.GetName()}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[AudioSfxBenchmark] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void WarmUpMeasurementApis()
    {
        for (int index = 0; index < 10; index++)
        {
            _ = Stopwatch.GetTimestamp();
            _ = GC.GetAllocatedBytesForCurrentThread();
        }
    }

    private static async Task BenchmarkFirstPlay(IAudioService audio)
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        bool played = await audio.PlaySfxAsync(AudioKey);
        long finished = Stopwatch.GetTimestamp();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert(played, "首次 SFX 播放被容量限制拒绝");
        Assert(audio.ActiveSfxCount == 1, "首次 SFX 播放没有占用一个 Voice");
        audio.StopAllSfx();
        GD.Print(
            $"[AudioSfxBenchmark] FirstPlay: " +
            $"ReadyMs={ToMilliseconds(finished - started):F3}; " +
            $"AllocatedBytes={allocated}");
    }

    private static async Task BenchmarkPoolGrowth(IAudioService audio)
    {
        var tasks = new Task<bool>[MaximumVoiceCount];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int index = 0; index < tasks.Length; index++)
            tasks[index] = audio.PlaySfxAsync(AudioKey);
        long submitted = Stopwatch.GetTimestamp();
        bool[] results = await Task.WhenAll(tasks);
        long ready = Stopwatch.GetTimestamp();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        AssertAllPlayed(results, "池扩容");
        Assert(audio.ActiveSfxCount == MaximumVoiceCount,
            $"池扩容后活动 Voice 数错误: {audio.ActiveSfxCount}");
        audio.StopAllSfx();
        Assert(audio.ActiveSfxCount == 0, "池扩容样本停止后仍有活动 Voice");
        GD.Print(
            $"[AudioSfxBenchmark] PoolGrowth: Voices={MaximumVoiceCount}; " +
            $"SubmitMs={ToMilliseconds(submitted - started):F3}; " +
            $"ReadyMs={ToMilliseconds(ready - started):F3}; " +
            $"AllocatedBytes={allocated}");
    }

    private static async Task BenchmarkPreparedFirstPlay(IAudioService audio)
    {
        int activeBefore = audio.ActiveSfxCount;
        int pendingBefore = audio.PendingSfxCount;
        long prepareAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long prepareStarted = Stopwatch.GetTimestamp();
        await audio.PrepareSfxAsync(AlternateAudioKey);
        long prepareFinished = Stopwatch.GetTimestamp();
        long prepareAllocated =
            GC.GetAllocatedBytesForCurrentThread() - prepareAllocatedBefore;

        Assert(audio.ActiveSfxCount == activeBefore,
            "SFX 资源准备改变了活动 Voice 数量");
        Assert(audio.PendingSfxCount == pendingBefore,
            "SFX 资源准备占用了播放准入容量");

        long playAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long playStarted = Stopwatch.GetTimestamp();
        bool played = await audio.PlaySfxAsync(AlternateAudioKey);
        long playFinished = Stopwatch.GetTimestamp();
        long playAllocated =
            GC.GetAllocatedBytesForCurrentThread() - playAllocatedBefore;

        Assert(played, "资源准备后的首次 SFX 没有开始播放");
        audio.StopAllSfx();
        GD.Print(
            $"[AudioSfxBenchmark] PreparedFirstPlay: " +
            $"PrepareMs={ToMilliseconds(prepareFinished - prepareStarted):F3}; " +
            $"PrepareAllocatedBytes={prepareAllocated}; " +
            $"ReadyMs={ToMilliseconds(playFinished - playStarted):F3}; " +
            $"AllocatedBytes={playAllocated}");
    }

    private static async Task BenchmarkCachedBatch(IAudioService audio, int batchSize)
    {
        var tasks = new Task<bool>[batchSize];
        var submitMilliseconds = new double[SampleCount];
        var readyMilliseconds = new double[SampleCount];
        var allocatedBytes = new long[SampleCount];

        for (int sample = 0; sample < SampleCount; sample++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int index = 0; index < tasks.Length; index++)
                tasks[index] = audio.PlaySfxAsync(AudioKey);
            long submitted = Stopwatch.GetTimestamp();
            bool[] results = await Task.WhenAll(tasks);
            long ready = Stopwatch.GetTimestamp();

            submitMilliseconds[sample] = ToMilliseconds(submitted - started);
            readyMilliseconds[sample] = ToMilliseconds(ready - started);
            allocatedBytes[sample] =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            AssertAllPlayed(results, $"缓存批次 {batchSize}");
            Assert(audio.ActiveSfxCount == batchSize,
                $"缓存批次 {batchSize} 活动 Voice 数错误: {audio.ActiveSfxCount}");
            audio.StopAllSfx();
            Assert(audio.ActiveSfxCount == 0,
                $"缓存批次 {batchSize} 停止后仍有活动 Voice");
        }

        Array.Sort(submitMilliseconds);
        Array.Sort(readyMilliseconds);
        Array.Sort(allocatedBytes);
        GD.Print(
            $"[AudioSfxBenchmark] CachedBatch: Voices={batchSize}; Samples={SampleCount}; " +
            $"SubmitAvgMs={Average(submitMilliseconds):F3}; " +
            $"SubmitP50Ms={Percentile(submitMilliseconds, 0.50d):F3}; " +
            $"SubmitP95Ms={Percentile(submitMilliseconds, 0.95d):F3}; " +
            $"ReadyAvgMs={Average(readyMilliseconds):F3}; " +
            $"ReadyP50Ms={Percentile(readyMilliseconds, 0.50d):F3}; " +
            $"ReadyP95Ms={Percentile(readyMilliseconds, 0.95d):F3}; " +
            $"AllocatedAvgBytes={Average(allocatedBytes):F0}; " +
            $"AllocatedP95Bytes={Percentile(allocatedBytes, 0.95d):F0}");
    }

    private static async Task BenchmarkPriorityPreemption(IAudioService audio)
    {
        var lowTasks = new Task<SfxPlaybackResult>[MaximumVoiceCount];
        var lowOptions = new SfxPlaybackOptions(
            1f,
            priority: SfxPriority.Low);
        var criticalOptions = new SfxPlaybackOptions(
            1f,
            priority: SfxPriority.Critical,
            allowStealLowerPriority: true);

        for (int index = 0; index < lowTasks.Length; index++)
            lowTasks[index] = audio.PlaySfxAsync(AudioKey, lowOptions);
        SfxPlaybackResult[] lowResults = await Task.WhenAll(lowTasks);
        AssertAllStarted(lowResults, "优先级抢占预填充");
        Assert(audio.ActiveSfxCount == MaximumVoiceCount,
            "优先级抢占预填充没有占满 Voice");

        long preemptedBefore = audio.PreemptedSfxCount;
        var submitMilliseconds = new double[SampleCount];
        var readyMilliseconds = new double[SampleCount];
        var allocatedBytes = new long[SampleCount];

        for (int sample = 0; sample < SampleCount; sample++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            Task<SfxPlaybackResult> request = audio.PlaySfxAsync(AudioKey, criticalOptions);
            long submitted = Stopwatch.GetTimestamp();
            SfxPlaybackResult result = await request;
            long ready = Stopwatch.GetTimestamp();

            Assert(result.Started, $"优先级抢占样本 {sample} 没有开始播放");
            Assert(audio.ActiveSfxCount == MaximumVoiceCount,
                $"优先级抢占样本 {sample} 改变了活动 Voice 数量");
            submitMilliseconds[sample] = ToMilliseconds(submitted - started);
            readyMilliseconds[sample] = ToMilliseconds(ready - started);
            allocatedBytes[sample] =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        }

        Assert(
            audio.PreemptedSfxCount - preemptedBefore == SampleCount,
            "优先级抢占累计数量与样本数不一致");
        audio.StopAllSfx();

        Array.Sort(submitMilliseconds);
        Array.Sort(readyMilliseconds);
        Array.Sort(allocatedBytes);
        GD.Print(
            $"[AudioSfxBenchmark] PriorityPreemption: Voices={MaximumVoiceCount}; " +
            $"Samples={SampleCount}; " +
            $"SubmitAvgMs={Average(submitMilliseconds):F3}; " +
            $"SubmitP50Ms={Percentile(submitMilliseconds, 0.50d):F3}; " +
            $"SubmitP95Ms={Percentile(submitMilliseconds, 0.95d):F3}; " +
            $"ReadyAvgMs={Average(readyMilliseconds):F3}; " +
            $"ReadyP50Ms={Percentile(readyMilliseconds, 0.50d):F3}; " +
            $"ReadyP95Ms={Percentile(readyMilliseconds, 0.95d):F3}; " +
            $"AllocatedAvgBytes={Average(allocatedBytes):F0}; " +
            $"AllocatedP95Bytes={Percentile(allocatedBytes, 0.95d):F0}");
    }

    private async Task BenchmarkVoicePrewarm()
    {
        AudioService service = CreateIsolatedAudioService();
        try
        {
            long prewarmAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long prewarmStarted = Stopwatch.GetTimestamp();
            int created = service.PrewarmSfxVoices(MaximumVoiceCount);
            long prewarmFinished = Stopwatch.GetTimestamp();
            long prewarmAllocated =
                GC.GetAllocatedBytesForCurrentThread() - prewarmAllocatedBefore;

            Assert(created == MaximumVoiceCount - 8,
                $"显式 Voice 预热创建数量错误: {created}");
            Assert(service.PreparedSfxVoiceCount == MaximumVoiceCount,
                "显式 Voice 预热没有达到 32 路");

            var tasks = new Task<bool>[MaximumVoiceCount];
            long batchAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long batchStarted = Stopwatch.GetTimestamp();
            for (int index = 0; index < tasks.Length; index++)
                tasks[index] = service.PlaySfxAsync(AudioKey);
            long batchSubmitted = Stopwatch.GetTimestamp();
            bool[] results = await Task.WhenAll(tasks);
            long batchReady = Stopwatch.GetTimestamp();
            long batchAllocated =
                GC.GetAllocatedBytesForCurrentThread() - batchAllocatedBefore;

            AssertAllPlayed(results, "显式预热后的 32 路批次");
            Assert(service.ActiveSfxCount == MaximumVoiceCount,
                "显式预热后的批次没有占满 32 路");
            service.StopAllSfx();
            GD.Print(
                $"[AudioSfxBenchmark] VoicePrewarm: " +
                $"Created={created}; " +
                $"PrewarmMs={ToMilliseconds(prewarmFinished - prewarmStarted):F3}; " +
                $"PrewarmAllocatedBytes={prewarmAllocated}; " +
                $"PreparedSubmitMs={ToMilliseconds(batchSubmitted - batchStarted):F3}; " +
                $"PreparedReadyMs={ToMilliseconds(batchReady - batchStarted):F3}; " +
                $"PreparedAllocatedBytes={batchAllocated}");
        }
        finally
        {
            if (service.IsInsideTree())
                service.GetParent()?.RemoveChild(service);
            if (!service.IsQueuedForDeletion())
                service.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task BenchmarkSfx3DPrewarmAndBurst(IAudioService audio)
    {
        audio.StopAllSfx3D();
        long prewarmAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long prewarmStarted = Stopwatch.GetTimestamp();
        int created = audio.PrewarmSfx3DVoices(MaximumVoiceCount);
        long prewarmFinished = Stopwatch.GetTimestamp();
        long prewarmAllocated =
            GC.GetAllocatedBytesForCurrentThread() - prewarmAllocatedBefore;

        Assert(audio.PreparedSfx3DVoiceCount == MaximumVoiceCount,
            "3D Voice 预热没有达到 32 路");

        var options = new Sfx3DPlaybackOptions(
            maxDistance: 100f,
            unitSize: 8f,
            priority: SfxPriority.Normal);
        Sfx3DBurstMeasurement coldBurst = await MeasureSfx3DBurst(audio, options, "首次");
        Sfx3DBurstMeasurement stableBurst = await MeasureSfx3DBurst(audio, options, "稳定");

        GD.Print(
            $"[AudioSfxBenchmark] Sfx3DPrewarmBurst: " +
            $"Created={created}; " +
            $"PrewarmMs={ToMilliseconds(prewarmFinished - prewarmStarted):F3}; " +
            $"PrewarmAllocatedBytes={prewarmAllocated}; " +
            $"ColdSubmitMs={coldBurst.SubmitMilliseconds:F3}; " +
            $"ColdReadyMs={coldBurst.ReadyMilliseconds:F3}; " +
            $"ColdAllocatedBytes={coldBurst.AllocatedBytes}; " +
            $"StableSubmitMs={stableBurst.SubmitMilliseconds:F3}; " +
            $"StableReadyMs={stableBurst.ReadyMilliseconds:F3}; " +
            $"StableAllocatedBytes={stableBurst.AllocatedBytes}");
    }

    private static async Task<Sfx3DBurstMeasurement> MeasureSfx3DBurst(
        IAudioService audio,
        Sfx3DPlaybackOptions options,
        string context)
    {
        var tasks = new Task<Sfx3DPlaybackResult>[MaximumVoiceCount];
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int index = 0; index < tasks.Length; index++)
        {
            tasks[index] = audio.PlaySfx3DAsync(
                AudioKey,
                new Vector3(index % 8, 0f, index / 8),
                options);
        }

        long submitted = Stopwatch.GetTimestamp();
        Sfx3DPlaybackResult[] results = await Task.WhenAll(tasks);
        long ready = Stopwatch.GetTimestamp();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        for (int index = 0; index < results.Length; index++)
            Assert(results[index].Started, $"{context} 3D 突发的第 {index} 个 Voice 没有开始播放");
        Assert(audio.ActiveSfx3DCount == MaximumVoiceCount,
            $"{context} 3D 突发没有占满 32 路");
        audio.StopAllSfx3D();

        return new Sfx3DBurstMeasurement(
            ToMilliseconds(submitted - started),
            ToMilliseconds(ready - started),
            allocated);
    }

    private async Task BenchmarkSfx3DFollowUpdates()
    {
        AudioService service = CreateFollowAudioService();
        var targets = new Node3D[MaximumVoiceCount];
        try
        {
            service.PrewarmSfx3DVoices(MaximumVoiceCount);
            for (int index = 0; index < targets.Length; index++)
            {
                targets[index] = new Node3D
                {
                    Name = $"FollowBenchmarkTarget{index}",
                    Position = new Vector3(index % 8, 0f, index / 8),
                };
                AddChild(targets[index]);
            }

            foreach (int voiceCount in new[] { 8, 16, 32 })
                await MeasureSfx3DFollowUpdates(service, targets, voiceCount);
        }
        finally
        {
            service.StopAllSfx3D();
            for (int index = 0; index < targets.Length; index++)
            {
                Node3D? target = targets[index];
                if (target == null)
                    continue;
                if (target.IsInsideTree())
                    RemoveChild(target);
                if (!target.IsQueuedForDeletion())
                    target.QueueFree();
            }

            if (service.IsInsideTree())
                service.GetParent()?.RemoveChild(service);
            if (!service.IsQueuedForDeletion())
                service.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task MeasureSfx3DFollowUpdates(
        AudioService service,
        Node3D[] targets,
        int voiceCount)
    {
        var tasks = new Task<Sfx3DPlaybackResult>[voiceCount];
        var options = new Sfx3DPlaybackOptions(maxDistance: 100f, unitSize: 8f);
        for (int index = 0; index < tasks.Length; index++)
        {
            tasks[index] = service.PlaySfx3DFollowAsync(
                AudioKey,
                targets[index],
                new Vector3(0.25f, 0.5f, -0.25f),
                options);
        }

        Sfx3DPlaybackResult[] results = await Task.WhenAll(tasks);
        for (int index = 0; index < results.Length; index++)
            Assert(results[index].Started, $"{voiceCount} 路跟随的第 {index} 个 Voice 没有开始播放");
        Assert(service.FollowingSfx3DCount == voiceCount,
            $"跟随数量错误: 期望 {voiceCount}，实际 {service.FollowingSfx3DCount}");
        Assert(service.IsPhysicsProcessing(), $"{voiceCount} 路跟随没有启用物理帧更新");

        for (int index = 0; index < 100; index++)
            service._PhysicsProcess(1d / 60d);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        for (int index = 0; index < FollowUpdateIterations; index++)
            service._PhysicsProcess(1d / 60d);
        long finished = Stopwatch.GetTimestamp();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        double totalMilliseconds = ToMilliseconds(finished - started);

        Assert(allocated == 0,
            $"{voiceCount} 路跟随热路径产生了托管分配: {allocated} bytes");
        service.StopAllSfx3D();
        Assert(!service.IsPhysicsProcessing(), $"{voiceCount} 路跟随停止后仍启用物理帧更新");

        GD.Print(
            $"[AudioSfxBenchmark] Sfx3DFollowUpdate: Voices={voiceCount}; " +
            $"Iterations={FollowUpdateIterations}; " +
            $"TotalMs={totalMilliseconds:F3}; " +
            $"AvgUsPerUpdate={totalMilliseconds * 1000d / FollowUpdateIterations:F3}; " +
            $"AvgUsPerVoice={totalMilliseconds * 1000d / FollowUpdateIterations / voiceCount:F3}; " +
            $"AllocatedBytes={allocated}");
    }

    private AudioService CreateIsolatedAudioService()
    {
        var bgmPlayer = new AudioStreamPlayer { Name = "BgmPlayer" };
        var secondaryBgmPlayer = new AudioStreamPlayer { Name = "BgmSecondaryPlayer" };
        var sfxRoot = new Node { Name = "SfxRoot" };
        var sfx3DRoot = new Node3D { Name = "Sfx3DRoot" };
        var service = new AudioService
        {
            Name = "PrewarmAudioServiceUnderTest",
            BgmPlayerPath = new NodePath("BgmPlayer"),
            SecondaryBgmPlayerPath = new NodePath("BgmSecondaryPlayer"),
            SfxRootPath = new NodePath("SfxRoot"),
            SfxVoiceScene = ResourceHub.Load<PackedScene>(SfxVoiceKey),
            MaxSfxVoices = MaximumVoiceCount,
            InitialSfxVoices = 8,
            Sfx3DRootPath = new NodePath("Sfx3DRoot"),
            Sfx3DVoiceScene = ResourceHub.Load<PackedScene>(Sfx3DVoiceKey),
            MaxSfx3DVoices = 1,
            MaxFollowingSfx3DVoices = 1,
            InitialSfx3DVoices = 0,
        };
        service.AddChild(bgmPlayer);
        service.AddChild(secondaryBgmPlayer);
        service.AddChild(sfxRoot);
        service.AddChild(sfx3DRoot);
        AddChild(service);
        return service;
    }

    private AudioService CreateFollowAudioService()
    {
        var bgmPlayer = new AudioStreamPlayer { Name = "BgmPlayer" };
        var secondaryBgmPlayer = new AudioStreamPlayer { Name = "BgmSecondaryPlayer" };
        var sfxRoot = new Node { Name = "SfxRoot" };
        var sfx3DRoot = new Node3D { Name = "Sfx3DRoot" };
        var service = new AudioService
        {
            Name = "FollowBenchmarkAudioService",
            BgmPlayerPath = new NodePath("BgmPlayer"),
            SecondaryBgmPlayerPath = new NodePath("BgmSecondaryPlayer"),
            SfxRootPath = new NodePath("SfxRoot"),
            SfxVoiceScene = ResourceHub.Load<PackedScene>(SfxVoiceKey),
            MaxSfxVoices = 1,
            InitialSfxVoices = 0,
            Sfx3DRootPath = new NodePath("Sfx3DRoot"),
            Sfx3DVoiceScene = ResourceHub.Load<PackedScene>(Sfx3DVoiceKey),
            MaxSfx3DVoices = MaximumVoiceCount,
            MaxFollowingSfx3DVoices = MaximumVoiceCount,
            InitialSfx3DVoices = 0,
        };
        service.AddChild(bgmPlayer);
        service.AddChild(secondaryBgmPlayer);
        service.AddChild(sfxRoot);
        service.AddChild(sfx3DRoot);
        AddChild(service);
        return service;
    }

    private static void AssertAllPlayed(bool[] results, string context)
    {
        for (int index = 0; index < results.Length; index++)
            Assert(results[index], $"{context} 的第 {index} 个 Voice 没有开始播放");
    }

    private static void AssertAllStarted(
        SfxPlaybackResult[] results,
        string context)
    {
        for (int index = 0; index < results.Length; index++)
        {
            Assert(
                results[index].Started,
                $"{context} 的第 {index} 个 Voice 没有开始播放");
        }
    }

    private static double ToMilliseconds(long ticks) =>
        ticks * 1000d / Stopwatch.Frequency;

    private static double Average(double[] values)
    {
        double total = 0d;
        for (int index = 0; index < values.Length; index++)
            total += values[index];
        return total / values.Length;
    }

    private static double Average(long[] values)
    {
        long total = 0L;
        for (int index = 0; index < values.Length; index++)
            total += values[index];
        return (double)total / values.Length;
    }

    private readonly record struct Sfx3DBurstMeasurement(
        double SubmitMilliseconds,
        double ReadyMilliseconds,
        long AllocatedBytes);

    private static double Percentile(double[] sortedValues, double percentile)
    {
        int index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }

    private static double Percentile(long[] sortedValues, double percentile)
    {
        int index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
