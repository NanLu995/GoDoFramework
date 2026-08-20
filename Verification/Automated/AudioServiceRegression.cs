using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>AudioService 播放、停止、容量与退出清理的无交互回归入口。</summary>
public sealed partial class AudioServiceRegression : Node
{
    private const int MaxVoices = 128;
    private const int BurstVoiceCount = 100;

    private static readonly ResourceKey AudioKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/Audio/LoopSilence.tres");
    private static readonly ResourceKey OneShotAudioKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/Audio/OneShotSilence.tres");
    private static readonly ResourceKey AlternateAudioKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/Audio/AlternateLoopSilence.tres");
    private static readonly ResourceKey MissingKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/Audio/Missing.tres");
    private static readonly ResourceKey SfxVoiceKey =
        ResourceKey.Create("res://addons/godo_framework/Runtime/Audio/SfxVoice.tscn");
    private static readonly ResourceKey Sfx3DVoiceKey =
        ResourceKey.Create("res://addons/godo_framework/Runtime/Audio/Sfx3DVoice.tscn");

    private AudioService _service = null!;
    private Node _sfxRoot = null!;
    private Node3D _sfx3DRoot = null!;
    private Camera3D _audioListenerCamera = null!;
    private int _passed;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            CreateService();

            Run("Bus 音量与参数校验", VerifyBusVolume);
            await RunAsync("资源失败语义", VerifyMissingResourceFailures);
            await RunAsync("BGM 播放暂停与停止", VerifyBgmPlayback);
            await RunAsync("BGM Stop 释放加载状态", VerifyBgmStopDuringLoading);
            await RunAsync("BGM Crossfade 与状态", VerifyBgmCrossfade);
            await RunAsync("BGM Crossfade 最新请求优先", VerifyBgmCrossfadeLatestWins);
            await RunAsync("BGM Crossfade 暂停与取消", VerifyBgmCrossfadePauseAndCancellation);
            await RunAsync("BGM FadeOut 完成与取消", VerifyBgmFadeOut);
            await RunAsync("SFX 容量与 StopAll", VerifySfxCapacityAndStopAll);
            await RunAsync("SFX 逐次参数与池重置", VerifySfxPlaybackParameters);
            await RunAsync("SFX 句柄与突发准入", VerifyControlledSfxAdmission);
            await RunAsync("SFX 资源准备与 Voice 预热", VerifySfxPreparationAndPrewarm);
            await RunAsync("3D SFX 位置参数与池复用", VerifySfx3DPlaybackAndPrewarm);
            await RunAsync("3D SFX 句柄与突发准入", VerifyControlledSfx3DAdmission);
            await RunAsync("3D SFX 跟随更新与空闲停更", VerifySfx3DFollowUpdate);
            await RunAsync("3D SFX 跟随容量与目标生命周期", VerifySfx3DFollowLifecycle);
            await RunAsync("SFX 自然结束回收", VerifySfxNaturalCompletion);
            await RunAsync("100 路缓存突发", VerifyCachedBurst);
            await RunAsync("服务退出取消等待", VerifyExitCancellation);

            CleanupService();
            await ToSignal(
                GetTree().CreateTimer(0.2),
                SceneTreeTimer.SignalName.Timeout);
            GD.Print($"[AudioServiceRegression] PASS ({_passed}/19)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[AudioServiceRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            CleanupService();
        }
    }

    private void CreateService()
    {
        _audioListenerCamera = new Camera3D
        {
            Name = "AudioListenerCamera",
            Current = true,
        };
        AddChild(_audioListenerCamera);

        var bgmPlayer = new AudioStreamPlayer { Name = "BgmPlayer" };
        var secondaryBgmPlayer = new AudioStreamPlayer { Name = "BgmSecondaryPlayer" };
        _sfxRoot = new Node { Name = "SfxRoot" };
        _sfx3DRoot = new Node3D { Name = "Sfx3DRoot" };
        _service = new AudioService
        {
            Name = "AudioServiceUnderTest",
            BgmPlayerPath = new NodePath("BgmPlayer"),
            SecondaryBgmPlayerPath = new NodePath("BgmSecondaryPlayer"),
            SfxRootPath = new NodePath("SfxRoot"),
            SfxVoiceScene = ResourceHub.Load<PackedScene>(SfxVoiceKey),
            MaxSfxVoices = MaxVoices,
            InitialSfxVoices = 8,
            Sfx3DRootPath = new NodePath("Sfx3DRoot"),
            Sfx3DVoiceScene = ResourceHub.Load<PackedScene>(Sfx3DVoiceKey),
            MaxSfx3DVoices = 8,
            MaxFollowingSfx3DVoices = 8,
            InitialSfx3DVoices = 0,
        };
        _service.AddChild(bgmPlayer);
        _service.AddChild(secondaryBgmPlayer);
        _service.AddChild(_sfxRoot);
        _service.AddChild(_sfx3DRoot);
        AddChild(_service);

        Assert(_service.IsInitialized, "AudioService 没有完成初始化");
        AssertEqual(MaxVoices, _service.MaxSfxVoices, "SFX 最大并发配置错误");
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[AudioServiceRegression] PASS: {name}");
    }

    private async Task RunAsync(string name, Func<Task> verification)
    {
        await verification();
        _passed++;
        GD.Print($"[AudioServiceRegression] PASS: {name}");
    }

    private void VerifyBusVolume()
    {
        float master = _service.GetVolume(AudioGroup.Master);
        float bgm = _service.GetVolume(AudioGroup.Bgm);
        float sfx = _service.GetVolume(AudioGroup.Sfx);

        try
        {
            _service.SetVolume(AudioGroup.Master, 0.75f);
            _service.SetVolume(AudioGroup.Bgm, 0.5f);
            _service.SetVolume(AudioGroup.Sfx, 0.25f);

            AssertNear(0.75f, _service.GetVolume(AudioGroup.Master), "Master 音量错误");
            AssertNear(0.5f, _service.GetVolume(AudioGroup.Bgm), "BGM 音量错误");
            AssertNear(0.25f, _service.GetVolume(AudioGroup.Sfx), "SFX 音量错误");
            AssertThrows<ArgumentOutOfRangeException>(
                () => _service.SetVolume(AudioGroup.Sfx, float.NaN),
                "NaN 音量没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => _service.SetVolume(AudioGroup.Sfx, 1.1f),
                "越界音量没有被拒绝");
        }
        finally
        {
            _service.SetVolume(AudioGroup.Master, master);
            _service.SetVolume(AudioGroup.Bgm, bgm);
            _service.SetVolume(AudioGroup.Sfx, sfx);
        }
    }

    private async Task VerifyMissingResourceFailures()
    {
        AudioPlaybackException bgmException =
            await AssertThrowsAsync<AudioPlaybackException>(
                () => _service.PlayBgmAsync(MissingKey),
                "缺失 BGM 没有抛出 AudioPlaybackException");
        AssertEqual(MissingKey, bgmException.Key, "缺失 BGM 异常 Key 错误");
        AssertEqual(AudioGroup.Bgm, bgmException.Group, "缺失 BGM 异常分组错误");
        Assert(!_service.IsBgmLoading, "缺失 BGM 后仍处于加载状态");

        AudioPlaybackException sfxException =
            await AssertThrowsAsync<AudioPlaybackException>(
                () => _service.PlaySfxAsync(MissingKey),
                "缺失 SFX 没有抛出 AudioPlaybackException");
        AssertEqual(MissingKey, sfxException.Key, "缺失 SFX 异常 Key 错误");
        AssertEqual(AudioGroup.Sfx, sfxException.Group, "缺失 SFX 异常分组错误");
        AssertEqual(0, _service.ActiveSfxCount, "缺失 SFX 后存在活动 Voice");

        AudioPlaybackException sfx3DException =
            await AssertThrowsAsync<AudioPlaybackException>(
                () => _service.PlaySfx3DAsync(
                    MissingKey,
                    Vector3.Zero,
                    new Sfx3DPlaybackOptions(50f)),
                "缺失 3D SFX 没有抛出 AudioPlaybackException");
        AssertEqual(MissingKey, sfx3DException.Key, "缺失 3D SFX 异常 Key 错误");
        AssertEqual(AudioGroup.Sfx, sfx3DException.Group, "缺失 3D SFX 异常分组错误");
        AssertEqual(0, _service.ActiveSfx3DCount, "缺失 3D SFX 后存在活动 Voice");
    }

    private async Task VerifyBgmPlayback()
    {
        await _service.PlayBgmAsync(AudioKey);

        AssertEqual(AudioKey, _service.CurrentBgm, "BGM 资源键错误");
        Assert(_service.IsBgmPlaying, "BGM 没有开始播放");
        Assert(!_service.IsBgmLoading, "BGM 成功后仍处于加载状态");

        await _service.PlayBgmAsync(AudioKey);
        _service.PauseBgm();
        _service.ResumeBgm();
        _service.StopBgm();

        Assert(!_service.CurrentBgm.HasValue, "StopBgm 后仍保留资源键");
        Assert(!_service.IsBgmPlaying, "StopBgm 后仍在播放");

        await _service.PlayBgmAsync(OneShotAudioKey);
        await WaitUntilAsync(
            () => _service.BgmState == BgmPlaybackState.Ended,
            "非循环 BGM 没有在超时前自然结束");
        AssertEqual(BgmPlaybackState.Ended, _service.BgmState, "非循环 BGM 自然结束状态错误");
        AssertEqual(OneShotAudioKey, _service.CurrentBgm, "非循环 BGM 结束后资源键丢失");
        _service.StopBgm();
    }

    private async Task VerifyBgmStopDuringLoading()
    {
        Node runtime = GetNode<Node>("/root/GoDoRuntime");
        bool runtimeWasProcessing = runtime.IsProcessing();
        Task firstRequest;
        Task replacementRequest;

        runtime.SetProcess(false);
        try
        {
            firstRequest = _service.PlayBgmAsync(AudioKey);
            Assert(_service.IsBgmLoading, "BGM 请求没有进入加载状态");
#if DEBUG
            Thread.Sleep(5);
            ulong? firstRequestAge = _service.DebugBgmRequestAgeMilliseconds;
            Assert(firstRequestAge >= 1, "BGM 加载请求存活时间没有增长");
#endif

            await AssertThrowsAsync<InvalidOperationException>(
                () => _service.PlayBgmAsync(AlternateAudioKey),
                "加载期间的第二个立即播放请求没有被拒绝");

            _service.StopBgm();
            Assert(!_service.IsBgmLoading, "StopBgm 没有立即释放加载状态");
#if DEBUG
            Assert(!_service.DebugBgmRequestAgeMilliseconds.HasValue,
                "StopBgm 后仍保留活动请求存活时间");
#endif

            replacementRequest = _service.PlayBgmAsync(AudioKey);
            Assert(_service.IsBgmLoading, "StopBgm 后无法立即开始新请求");
#if DEBUG
            Assert(_service.DebugBgmRequestAgeMilliseconds < firstRequestAge,
                "替代 BGM 请求没有从新的起点计时");
#endif
        }
        finally
        {
            runtime.SetProcess(runtimeWasProcessing);
        }

        await AssertThrowsAsync<OperationCanceledException>(
            () => firstRequest,
            "StopBgm 前的请求没有取消");
        await replacementRequest;

        AssertEqual(AudioKey, _service.CurrentBgm, "替代 BGM 请求没有提交");
        Assert(!_service.IsBgmLoading, "替代 BGM 请求完成后仍处于加载状态");
#if DEBUG
        Assert(!_service.DebugBgmRequestAgeMilliseconds.HasValue,
            "BGM 请求完成后仍保留活动请求存活时间");
#endif
        _service.StopBgm();
    }

    private async Task VerifySfxCapacityAndStopAll()
    {
        Node runtime = GetNode<Node>("/root/GoDoRuntime");
        bool runtimeWasProcessing = runtime.IsProcessing();
        var canceledRequests = new Task<bool>[MaxVoices];
        Task<bool> replacementA;
        Task<bool> replacementB;

        runtime.SetProcess(false);
        try
        {
            for (int index = 0; index < canceledRequests.Length; index++)
                canceledRequests[index] = _service.PlaySfxAsync(AudioKey);

            bool overCapacity = await _service.PlaySfxAsync(AudioKey);
            Assert(!overCapacity, "SFX 待加载请求突破并发上限");

            _service.StopAllSfx();
            replacementA = _service.PlaySfxAsync(AudioKey);
            replacementB = _service.PlaySfxAsync(AudioKey);
        }
        finally
        {
            runtime.SetProcess(runtimeWasProcessing);
        }

        for (int index = 0; index < canceledRequests.Length; index++)
        {
            await AssertThrowsAsync<OperationCanceledException>(
                () => canceledRequests[index],
                $"StopAllSfx 没有取消旧请求 {index}");
        }

        Assert(await replacementA, "StopAllSfx 后第一个新请求没有播放");
        Assert(await replacementB, "StopAllSfx 后第二个新请求没有播放");
        AssertEqual(2, _service.ActiveSfxCount, "StopAllSfx 后活动 Voice 数量错误");

        _service.StopAllSfx();
        AssertEqual(0, _service.ActiveSfxCount, "StopAllSfx 后仍有活动 Voice");
    }

    private async Task VerifyBgmCrossfade()
    {
        await _service.PlayBgmAsync(AudioKey);
        AssertEqual(BgmPlaybackState.Playing, _service.BgmState, "立即播放状态错误");

        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.CrossfadeBgmAsync(AlternateAudioKey, 0d),
            "零时长 Crossfade 没有被拒绝");

        await _service.CrossfadeBgmAsync(AlternateAudioKey, 0.05d);
        AssertEqual(AlternateAudioKey, _service.CurrentBgm, "Crossfade 目标资源键错误");
        AssertEqual(BgmPlaybackState.Playing, _service.BgmState, "Crossfade 完成状态错误");
        Assert(!_service.IsBgmLoading, "Crossfade 完成后仍处于加载状态");
    }

    private async Task VerifyBgmCrossfadeLatestWins()
    {
        Task outdated = _service.CrossfadeBgmAsync(AudioKey, 0.2d);
        await ToSignal(GetTree().CreateTimer(0.03d), SceneTreeTimer.SignalName.Timeout);
        AssertEqual(BgmPlaybackState.Transitioning, _service.BgmState, "首个 Crossfade 没有进入过渡状态");

        Task latest = _service.CrossfadeBgmAsync(AlternateAudioKey, 0.05d);
        await AssertThrowsAsync<OperationCanceledException>(
            () => outdated,
            "被更新请求取代的 Crossfade 没有取消");
        await latest;

        AssertEqual(AlternateAudioKey, _service.CurrentBgm, "最新 Crossfade 请求没有成为当前 BGM");
        AssertEqual(BgmPlaybackState.Playing, _service.BgmState, "最新 Crossfade 完成状态错误");
    }

    private async Task VerifyBgmCrossfadePauseAndCancellation()
    {
        Task pausedTransition = _service.CrossfadeBgmAsync(AudioKey, 0.12d);
        await ToSignal(GetTree().CreateTimer(0.03d), SceneTreeTimer.SignalName.Timeout);
        _service.PauseBgm();
        AssertEqual(BgmPlaybackState.Paused, _service.BgmState, "Crossfade 暂停状态错误");

        await ToSignal(GetTree().CreateTimer(0.06d), SceneTreeTimer.SignalName.Timeout);
        Assert(!pausedTransition.IsCompleted, "暂停期间 Crossfade 仍然完成");
        _service.ResumeBgm();
        await pausedTransition;
        AssertEqual(AudioKey, _service.CurrentBgm, "恢复后的 Crossfade 目标错误");

        using var cancellation = new System.Threading.CancellationTokenSource();
        Task canceledTransition = _service.CrossfadeBgmAsync(AlternateAudioKey, 0.2d, cancellation.Token);
        await ToSignal(GetTree().CreateTimer(0.03d), SceneTreeTimer.SignalName.Timeout);
        cancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => canceledTransition,
            "调用方取消 Crossfade 后任务没有取消");

        Assert(_service.CurrentBgm.HasValue, "取消 Crossfade 后没有保留可播放 BGM");
        AssertEqual(BgmPlaybackState.Playing, _service.BgmState, "取消 Crossfade 后状态没有稳定到播放中");

        Task stoppedTransition = _service.CrossfadeBgmAsync(AlternateAudioKey, 0.2d);
        await ToSignal(GetTree().CreateTimer(0.03d), SceneTreeTimer.SignalName.Timeout);
        _service.StopBgm();
        await AssertThrowsAsync<OperationCanceledException>(
            () => stoppedTransition,
            "StopBgm 没有取消正在执行的 Crossfade");
        AssertEqual(BgmPlaybackState.Stopped, _service.BgmState, "StopBgm 后状态不是 Stopped");
    }

    private async Task VerifyBgmFadeOut()
    {
        await AssertThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.FadeOutBgmAsync(double.NaN),
            "非法 FadeOut 时长没有被拒绝");

        await _service.PlayBgmAsync(AudioKey);
        await _service.FadeOutBgmAsync(0.05d);
        Assert(!_service.CurrentBgm.HasValue, "FadeOut 完成后仍保留 BGM 资源键");
        AssertEqual(BgmPlaybackState.Stopped, _service.BgmState, "FadeOut 完成后状态不是 Stopped");
        await _service.FadeOutBgmAsync(0.05d);

        await _service.PlayBgmAsync(AudioKey);
        using var cancellation = new System.Threading.CancellationTokenSource();
        Task canceledFadeOut = _service.FadeOutBgmAsync(0.2d, cancellation.Token);
        await ToSignal(GetTree().CreateTimer(0.03d), SceneTreeTimer.SignalName.Timeout);
        cancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => canceledFadeOut,
            "调用方取消 FadeOut 后任务没有取消");
        AssertEqual(AudioKey, _service.CurrentBgm, "取消 FadeOut 后没有保留当前 BGM");
        AssertEqual(BgmPlaybackState.Playing, _service.BgmState, "取消 FadeOut 后没有恢复播放状态");

        Task outdatedFadeOut = _service.FadeOutBgmAsync(0.2d);
        await ToSignal(GetTree().CreateTimer(0.03d), SceneTreeTimer.SignalName.Timeout);
        Task replacement = _service.CrossfadeBgmAsync(AlternateAudioKey, 0.05d);
        await AssertThrowsAsync<OperationCanceledException>(
            () => outdatedFadeOut,
            "被 Crossfade 取代的 FadeOut 没有取消");
        await replacement;
        AssertEqual(AlternateAudioKey, _service.CurrentBgm, "取代 FadeOut 的 Crossfade 没有提交");
        _service.StopBgm();
    }

    private async Task VerifyCachedBurst()
    {
        bool warmed = await _service.PlaySfxAsync(AudioKey);
        Assert(warmed, "性能测量预热播放失败");
        _service.StopAllSfx();

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long timestamp = Stopwatch.GetTimestamp();
        var requests = new Task<bool>[BurstVoiceCount];
        for (int index = 0; index < requests.Length; index++)
            requests[index] = _service.PlaySfxAsync(AudioKey);

        bool[] results = await Task.WhenAll(requests);
        double elapsedMilliseconds = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        for (int index = 0; index < results.Length; index++)
            Assert(results[index], $"缓存突发请求 {index} 没有播放");

        AssertEqual(BurstVoiceCount, _service.ActiveSfxCount, "缓存突发活动 Voice 数量错误");
        _service.StopAllSfx();
        AssertEqual(0, _service.ActiveSfxCount, "缓存突发停止后仍有活动 Voice");

        GD.Print(
            $"[AudioServiceRegression] PERF: voices={BurstVoiceCount}, " +
            $"elapsed={elapsedMilliseconds:F2} ms, allocated={allocatedBytes} bytes");
    }

    private async Task VerifySfxPlaybackParameters()
    {
        AssertThrows<ArgumentOutOfRangeException>(
            () => _service.PlaySfxAsync(AudioKey, float.NaN),
            "SFX NaN 逐次音量没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _service.PlaySfxAsync(AudioKey, -0.01f),
            "SFX 负逐次音量没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _service.PlaySfxAsync(AudioKey, 1.01f),
            "SFX 超范围逐次音量没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _service.PlaySfxAsync(AudioKey, 1f, 0f),
            "SFX 零音高倍率没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _service.PlaySfxAsync(AudioKey, 1f, float.PositiveInfinity),
            "SFX 无限音高倍率没有被拒绝");
        AssertEqual(0, _service.ActiveSfxCount, "无效逐次参数占用了 SFX Voice");

        bool customized = await _service.PlaySfxAsync(AudioKey, 0f, 2f);
        Assert(customized, "带逐次参数的 SFX 没有开始播放");
        SfxVoice customizedVoice = GetActiveSfxVoice();
        AssertNear(0f, customizedVoice.VolumeLinear, "SFX 逐次音量没有应用");
        AssertNear(2f, customizedVoice.PitchScale, "SFX 逐次音高没有应用");

        _service.StopAllSfx();
        bool defaulted = await _service.PlaySfxAsync(AudioKey);
        Assert(defaulted, "默认参数 SFX 没有开始播放");
        SfxVoice defaultVoice = GetActiveSfxVoice();
        AssertNear(1f, defaultVoice.VolumeLinear, "池复用后 SFX 音量没有恢复默认值");
        AssertNear(1f, defaultVoice.PitchScale, "池复用后 SFX 音高没有恢复默认值");
        _service.StopAllSfx();
    }

    private async Task VerifyControlledSfxAdmission()
    {
        AudioService service = CreateIsolatedAudioService(2);
        Node runtime = GetNode<Node>("/root/GoDoRuntime");
        bool runtimeWasProcessing = runtime.IsProcessing();

        try
        {
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PlaySfxAsync(
                    AudioKey,
                    new SfxPlaybackOptions(
                        1f,
                        priority: (SfxPriority)99)),
                "未定义 SFX 优先级没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PlaySfxAsync(
                    AudioKey,
                    new SfxPlaybackOptions(
                        1f,
                        maxConcurrentPerKey: -1)),
                "负数同资源并发上限没有被拒绝");

            Task<bool> oldestLegacy;
            Task<bool> newerLegacy;
            Task<SfxPlaybackResult> criticalRequest;
            runtime.SetProcess(false);
            try
            {
                oldestLegacy = service.PlaySfxAsync(AudioKey);
                newerLegacy = service.PlaySfxAsync(AlternateAudioKey);
                criticalRequest = service.PlaySfxAsync(
                    AudioKey,
                    new SfxPlaybackOptions(
                        1f,
                        priority: SfxPriority.Critical,
                        allowStealLowerPriority: true));

                AssertEqual(2, service.PendingSfxCount, "抢占后待加载 SFX 数量错误");
                AssertEqual(1L, service.PreemptedSfxCount, "待加载 SFX 没有被抢占");
            }
            finally
            {
                runtime.SetProcess(runtimeWasProcessing);
            }

            Assert(!await oldestLegacy, "最早的普通待加载请求没有返回 false");
            Assert(await newerLegacy, "未被抢占的普通待加载请求没有播放");
            SfxPlaybackResult critical = await criticalRequest;
            AssertEqual(SfxPlaybackStatus.Started, critical.Status, "Critical SFX 没有开始播放");
            Assert(critical.Handle.IsValid, "Critical SFX 没有返回有效句柄");
            Assert(service.IsSfxPlaying(critical.Handle), "Critical SFX 句柄没有处于活动状态");
            AssertEqual(2, service.ActiveSfxCount, "抢占完成后活动 SFX 数量错误");
            AssertEqual(0, service.PendingSfxCount, "抢占完成后仍有待加载 SFX");

            SfxPlaybackResult high = await service.PlaySfxAsync(
                AlternateAudioKey,
                new SfxPlaybackOptions(
                    1f,
                    priority: SfxPriority.High,
                    allowStealLowerPriority: true));
            Assert(high.Started, "High SFX 没有抢占活动的普通 Voice");
            Assert(service.IsSfxPlaying(high.Handle), "High SFX 句柄没有处于活动状态");
            Assert(service.IsSfxPlaying(critical.Handle), "High SFX 错误抢占了 Critical Voice");
            AssertEqual(2L, service.PreemptedSfxCount, "活动 SFX 抢占累计数量错误");

            SfxPlaybackResult perKeyRejected = await service.PlaySfxAsync(
                AudioKey,
                new SfxPlaybackOptions(
                    1f,
                    priority: SfxPriority.Critical,
                    maxConcurrentPerKey: 1,
                    allowStealLowerPriority: true));
            AssertEqual(
                SfxPlaybackStatus.PerKeyLimitReached,
                perKeyRejected.Status,
                "相同资源限制没有拒绝 SFX");

            SfxPlaybackResult capacityRejected = await service.PlaySfxAsync(
                OneShotAudioKey,
                new SfxPlaybackOptions());
            AssertEqual(
                SfxPlaybackStatus.GlobalCapacityReached,
                capacityRejected.Status,
                "全局容量没有拒绝普通 SFX");
            AssertEqual(2L, service.RejectedSfxCount, "SFX 拒绝累计数量错误");

            Assert(service.TryStopSfx(critical.Handle), "有效句柄没有停止 SFX");
            Assert(!service.IsSfxPlaying(critical.Handle), "停止后 SFX 句柄仍然活动");
            Assert(!service.TryStopSfx(critical.Handle), "过期句柄重复停止返回 true");

            service.StopAllSfx();
            SfxPlaybackResult controlledLow = await service.PlaySfxAsync(
                AudioKey,
                new SfxPlaybackOptions(1f, priority: SfxPriority.Low));
            SfxPlaybackResult controlledNormal = await service.PlaySfxAsync(
                AlternateAudioKey,
                new SfxPlaybackOptions(1f));
            SfxPlaybackResult controlledCritical = await service.PlaySfxAsync(
                OneShotAudioKey,
                new SfxPlaybackOptions(
                    1f,
                    priority: SfxPriority.Critical,
                    allowStealLowerPriority: true));
            Assert(controlledCritical.Started, "Critical SFX 没有抢占受控 Low Voice");
            Assert(!service.IsSfxPlaying(controlledLow.Handle),
                "受控 Low Voice 被抢占后句柄仍然活动");
            Assert(service.IsSfxPlaying(controlledNormal.Handle),
                "Critical SFX 没有选择最低优先级 Voice");

            service.StopAllSfx();
            SfxPlaybackResult defaultOptions = await service.PlaySfxAsync(
                AudioKey,
                default(SfxPlaybackOptions));
            Assert(defaultOptions.Started, "default SFX 选项没有按默认值播放");
            Assert(service.TryStopSfx(defaultOptions.Handle), "默认选项句柄无法停止");

            SfxPlaybackResult oneShot = await service.PlaySfxAsync(
                OneShotAudioKey,
                new SfxPlaybackOptions());
            Assert(oneShot.Started, "受控非循环 SFX 没有开始播放");
            await WaitUntilAsync(
                () => !service.IsSfxPlaying(oneShot.Handle),
                "受控非循环 SFX 没有在超时前自然结束");
            Assert(!service.IsSfxPlaying(oneShot.Handle), "自然结束后 SFX 句柄仍然活动");
        }
        finally
        {
            runtime.SetProcess(runtimeWasProcessing);
            if (service.IsInsideTree())
                service.GetParent()?.RemoveChild(service);
            if (!service.IsQueuedForDeletion())
                service.QueueFree();
        }
    }

    private async Task VerifySfxPreparationAndPrewarm()
    {
        AudioService service = CreateIsolatedAudioService(4, 0);
        try
        {
            AssertEqual(0, service.PreparedSfxVoiceCount, "零初始容量仍创建了 SFX Voice");
            AssertEqual(3, service.PrewarmSfxVoices(3), "SFX Voice 没有预热到 3 路");
            AssertEqual(3, service.PreparedSfxVoiceCount, "SFX Voice 已准备数量错误");
            AssertEqual(0, service.PrewarmSfxVoices(3), "重复 SFX Voice 预热不是幂等操作");
            AssertEqual(0, service.PrewarmSfxVoices(2), "较小预热目标仍创建了 Voice");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PrewarmSfxVoices(-1),
                "负数 SFX Voice 预热目标没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PrewarmSfxVoices(5),
                "超过最大容量的 SFX Voice 预热目标没有被拒绝");

            await service.PrepareSfxAsync(AudioKey);
            AssertEqual(0, service.ActiveSfxCount, "准备 SFX 资源占用了活动 Voice");
            AssertEqual(0, service.PendingSfxCount, "准备 SFX 资源占用了待加载容量");

            AudioPlaybackException prepareFailure =
                await AssertThrowsAsync<AudioPlaybackException>(
                    () => service.PrepareSfxAsync(MissingKey),
                    "准备缺失 SFX 没有抛出 AudioPlaybackException");
            AssertEqual(MissingKey, prepareFailure.Key, "准备失败的 SFX Key 错误");
            AssertEqual(AudioGroup.Sfx, prepareFailure.Group, "准备失败的 SFX 分组错误");

            using var cancellation = new System.Threading.CancellationTokenSource();
            cancellation.Cancel();
            await AssertThrowsAsync<OperationCanceledException>(
                () => service.PrepareSfxAsync(AudioKey, cancellation.Token),
                "预取消的 SFX 资源准备没有取消");

            var preparedRequests = new Task<bool>[3];
            for (int index = 0; index < preparedRequests.Length; index++)
                preparedRequests[index] = service.PlaySfxAsync(AudioKey);
            bool[] preparedResults = await Task.WhenAll(preparedRequests);
            Assert(Array.TrueForAll(preparedResults, static result => result),
                "预热后的前三路 SFX 没有全部播放");
            AssertEqual(3, service.PreparedSfxVoiceCount,
                "活动 Voice 没有计入已准备总数");

            Assert(await service.PlaySfxAsync(AudioKey), "第四路 SFX 没有按需扩展 Voice");
            AssertEqual(4, service.PreparedSfxVoiceCount, "SFX Voice 没有扩展到最大容量");
            service.StopAllSfx();
            AssertEqual(4, service.PreparedSfxVoiceCount, "回收后丢失了已准备 Voice");
        }
        finally
        {
            if (service.IsInsideTree())
                service.GetParent()?.RemoveChild(service);
            if (!service.IsQueuedForDeletion())
                service.QueueFree();
        }
    }

    private async Task VerifySfxNaturalCompletion()
    {
        bool played = await _service.PlaySfxAsync(OneShotAudioKey);
        Assert(played, "非循环 SFX 没有开始播放");
        AssertEqual(1, _service.ActiveSfxCount, "非循环 SFX 活动数量错误");

        await WaitUntilAsync(
            () => _service.ActiveSfxCount == 0,
            "非循环 SFX 没有在超时前自然结束");
        AssertEqual(0, _service.ActiveSfxCount, "非循环 SFX 结束后没有自动归还");
    }

    private async Task VerifySfx3DPlaybackAndPrewarm()
    {
        AudioService service = CreateIsolatedAudioService(4, 0);
        try
        {
            AssertEqual(0, service.PreparedSfx3DVoiceCount, "零初始容量仍创建了 3D Voice");
            AssertEqual(3, service.PrewarmSfx3DVoices(3), "3D Voice 没有预热到 3 路");
            AssertEqual(3, service.PreparedSfx3DVoiceCount, "3D Voice 已准备数量错误");
            AssertEqual(0, service.PrewarmSfx3DVoices(3), "重复 3D Voice 预热不是幂等操作");
            AssertEqual(0, service.PrewarmSfx3DVoices(2), "较小 3D 预热目标仍创建了 Voice");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PrewarmSfx3DVoices(-1),
                "负数 3D Voice 预热目标没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PrewarmSfx3DVoices(5),
                "超过最大容量的 3D Voice 预热目标没有被拒绝");

            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PlaySfx3DAsync(AudioKey, Vector3.Zero, default),
                "未配置最大距离的 3D SFX 没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PlaySfx3DAsync(
                    AudioKey,
                    new Vector3(float.NaN, 0f, 0f),
                    new Sfx3DPlaybackOptions(50f)),
                "非有限 3D 坐标没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PlaySfx3DAsync(
                    AudioKey,
                    Vector3.Zero,
                    new Sfx3DPlaybackOptions(0f)),
                "零最大距离没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PlaySfx3DAsync(
                    AudioKey,
                    Vector3.Zero,
                    new Sfx3DPlaybackOptions(50f, unitSize: 0f)),
                "零衰减基准尺寸没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PlaySfx3DAsync(
                    AudioKey,
                    Vector3.Zero,
                    new Sfx3DPlaybackOptions(50f, volumeLinear: 1.1f)),
                "越界 3D 音量没有被拒绝");
            AssertThrows<ArgumentOutOfRangeException>(
                () => service.PlaySfx3DAsync(
                    AudioKey,
                    Vector3.Zero,
                    new Sfx3DPlaybackOptions(50f, pitchScale: 0f)),
                "零 3D 音高没有被拒绝");
            AssertEqual(0, service.ActiveSfx3DCount, "无效 3D 参数占用了活动 Voice");
            AssertEqual(0, service.PendingSfx3DCount, "无效 3D 参数占用了待加载容量");

            var position = new Vector3(3f, 4f, 5f);
            Sfx3DPlaybackResult result = await service.PlaySfx3DAsync(
                AudioKey,
                position,
                new Sfx3DPlaybackOptions(
                    50f,
                    unitSize: 2f,
                    volumeLinear: 0.5f,
                    pitchScale: 1.25f,
                    attenuationModel: AudioStreamPlayer3D.AttenuationModelEnum.Logarithmic));
            Assert(result.Started, "3D SFX 没有开始播放");
            Assert(result.Handle.IsValid, "3D SFX 没有返回有效句柄");
            Assert(service.IsSfx3DPlaying(result.Handle), "3D SFX 句柄没有处于活动状态");
            AssertEqual(1, service.ActiveSfx3DCount, "3D SFX 活动数量错误");

            Node3D root = service.GetNode<Node3D>("Sfx3DRoot");
            var voice = root.GetChild(0) as Sfx3DVoice ??
                throw new InvalidOperationException("3D SFX Root 子节点不是 Sfx3DVoice");
            Assert(voice.GlobalPosition.IsEqualApprox(position), "3D SFX 世界坐标没有应用");
            AssertNear(50f, voice.MaxDistance, "3D SFX 最大距离没有应用");
            AssertNear(2f, voice.UnitSize, "3D SFX 衰减基准尺寸没有应用");
            AssertNear(0.5f, voice.VolumeLinear, "3D SFX 逐次音量没有应用");
            AssertNear(1.25f, voice.PitchScale, "3D SFX 逐次音高没有应用");
            AssertEqual(
                AudioStreamPlayer3D.AttenuationModelEnum.Logarithmic,
                voice.AttenuationModel,
                "3D SFX 距离衰减模型没有应用");

            Assert(service.TryStopSfx3D(result.Handle), "3D SFX 句柄停止失败");
            Assert(!service.IsSfx3DPlaying(result.Handle), "停止后的 3D SFX 句柄仍活动");
            Assert(!service.TryStopSfx3D(result.Handle), "过期 3D SFX 句柄重复停止成功");
            AssertEqual(0, service.ActiveSfx3DCount, "停止后仍有活动 3D Voice");
            AssertEqual(3, service.PreparedSfx3DVoiceCount, "回收后丢失已准备 3D Voice");

            Sfx3DPlaybackResult oneShot = await service.PlaySfx3DAsync(
                OneShotAudioKey,
                Vector3.One,
                new Sfx3DPlaybackOptions(25f));
            Assert(oneShot.Started, "非循环 3D SFX 没有开始播放");
            await WaitUntilAsync(
                () => service.ActiveSfx3DCount == 0,
                "非循环 3D SFX 没有在超时前自然结束");
            AssertEqual(0, service.ActiveSfx3DCount, "非循环 3D SFX 结束后没有自动归还");
            Assert(!service.IsSfx3DPlaying(oneShot.Handle), "自然结束的 3D SFX 句柄仍活动");

            Assert(await service.PlaySfxAsync(AudioKey), "隔离验证的非空间 SFX 没有播放");
            Sfx3DPlaybackResult spatial = await service.PlaySfx3DAsync(
                AudioKey,
                Vector3.Zero,
                new Sfx3DPlaybackOptions(50f));
            Assert(spatial.Started, "隔离验证的 3D SFX 没有播放");
            service.StopAllSfx3D();
            AssertEqual(0, service.ActiveSfx3DCount, "StopAllSfx3D 后仍有活动 3D Voice");
            AssertEqual(1, service.ActiveSfxCount, "StopAllSfx3D 错误停止了非空间 SFX");
            service.StopAllSfx();
        }
        finally
        {
            if (service.IsInsideTree())
                service.GetParent()?.RemoveChild(service);
            if (!service.IsQueuedForDeletion())
                service.QueueFree();
        }
    }

    private async Task VerifyControlledSfx3DAdmission()
    {
        AudioService service = CreateIsolatedAudioService(2, 0);
        Node runtime = GetNode<Node>("/root/GoDoRuntime");
        bool runtimeWasProcessing = runtime.IsProcessing();

        try
        {
            Task<Sfx3DPlaybackResult> oldestLow;
            Task<Sfx3DPlaybackResult> newerLow;
            Task<Sfx3DPlaybackResult> criticalRequest;
            runtime.SetProcess(false);
            try
            {
                var lowOptions = new Sfx3DPlaybackOptions(
                    50f,
                    priority: SfxPriority.Low);
                oldestLow = service.PlaySfx3DAsync(AudioKey, Vector3.Zero, lowOptions);
                newerLow = service.PlaySfx3DAsync(AlternateAudioKey, Vector3.One, lowOptions);
                criticalRequest = service.PlaySfx3DAsync(
                    AudioKey,
                    Vector3.Up,
                    new Sfx3DPlaybackOptions(
                        50f,
                        priority: SfxPriority.Critical,
                        allowStealLowerPriority: true));

                AssertEqual(2, service.PendingSfx3DCount, "3D 抢占后待加载数量错误");
                AssertEqual(1L, service.PreemptedSfx3DCount, "待加载 3D SFX 没有被抢占");
            }
            finally
            {
                runtime.SetProcess(runtimeWasProcessing);
            }

            Sfx3DPlaybackResult preempted = await oldestLow;
            Sfx3DPlaybackResult newer = await newerLow;
            Sfx3DPlaybackResult critical = await criticalRequest;
            AssertEqual(
                SfxPlaybackStatus.PreemptedBeforeStart,
                preempted.Status,
                "最早的 Low 3D 请求没有在开始前被抢占");
            Assert(newer.Started && critical.Started, "未被抢占的 3D 请求没有开始播放");
            AssertEqual(2, service.ActiveSfx3DCount, "3D 抢占完成后活动数量错误");

            Sfx3DPlaybackResult perKeyRejected = await service.PlaySfx3DAsync(
                AudioKey,
                Vector3.Zero,
                new Sfx3DPlaybackOptions(
                    50f,
                    priority: SfxPriority.Critical,
                    maxConcurrentPerKey: 1,
                    allowStealLowerPriority: true));
            AssertEqual(
                SfxPlaybackStatus.PerKeyLimitReached,
                perKeyRejected.Status,
                "3D 相同资源限制没有拒绝请求");

            Sfx3DPlaybackResult capacityRejected = await service.PlaySfx3DAsync(
                OneShotAudioKey,
                Vector3.Zero,
                new Sfx3DPlaybackOptions(50f));
            AssertEqual(
                SfxPlaybackStatus.GlobalCapacityReached,
                capacityRejected.Status,
                "3D 全局容量满没有返回结构化拒绝");

            Sfx3DPlaybackResult replacement = await service.PlaySfx3DAsync(
                OneShotAudioKey,
                Vector3.Zero,
                new Sfx3DPlaybackOptions(
                    50f,
                    priority: SfxPriority.Critical,
                    allowStealLowerPriority: true));
            Assert(replacement.Started, "Critical 3D SFX 没有抢占活动 Low Voice");
            Assert(!service.IsSfx3DPlaying(newer.Handle), "被抢占的 3D Handle 仍活动");
            Assert(service.IsSfx3DPlaying(critical.Handle), "Critical 3D SFX 被错误抢占");
            AssertEqual(2L, service.RejectedSfx3DCount, "3D SFX 拒绝累计数量错误");
            AssertEqual(2L, service.PreemptedSfx3DCount, "3D SFX 抢占累计数量错误");
            service.StopAllSfx3D();
        }
        finally
        {
            if (service.IsInsideTree())
                service.GetParent()?.RemoveChild(service);
            if (!service.IsQueuedForDeletion())
                service.QueueFree();
        }
    }

    private async Task VerifySfx3DFollowUpdate()
    {
        AudioService service = CreateIsolatedAudioService(1, 0, 1);
        var target = new Node3D
        {
            Name = "Sfx3DFollowTarget",
            Position = new Vector3(2f, 3f, 4f),
        };
        AddChild(target);

        try
        {
            Assert(!service.IsPhysicsProcessing(), "没有跟随音效时仍启用了物理帧更新");

            var offset = new Vector3(1f, 0.5f, -2f);
            Sfx3DPlaybackResult result = await service.PlaySfx3DFollowAsync(
                AudioKey,
                target,
                offset,
                new Sfx3DPlaybackOptions(50f));
            Assert(result.Started, "跟随 3D SFX 没有开始播放");
            AssertEqual(1, service.FollowingSfx3DCount, "活动跟随数量错误");
            Assert(service.IsPhysicsProcessing(), "存在跟随音效时没有启用物理帧更新");

            Node3D voiceRoot = service.GetNode<Node3D>("Sfx3DRoot");
            var voice = voiceRoot.GetChild(0) as Sfx3DVoice ??
                throw new InvalidOperationException("3D SFX Root 子节点不是 Sfx3DVoice");
            Assert(
                voice.GlobalPosition.IsEqualApprox(target.ToGlobal(offset)),
                "跟随 3D SFX 初始位置错误");

            target.Position = new Vector3(-3f, 1f, 6f);
            service._PhysicsProcess(1d / 60d);
            Assert(
                voice.GlobalPosition.IsEqualApprox(target.ToGlobal(offset)),
                "跟随 3D SFX 没有在物理帧同步目标位置");

            Assert(service.TryStopSfx3D(result.Handle), "停止跟随 3D SFX 失败");
            AssertEqual(0, service.FollowingSfx3DCount, "停止后仍保留跟随绑定");
            Assert(!service.IsPhysicsProcessing(), "最后一个跟随停止后仍启用物理帧更新");

            Sfx3DPlaybackResult lowFollow = await service.PlaySfx3DFollowAsync(
                AudioKey,
                target,
                Vector3.Zero,
                new Sfx3DPlaybackOptions(50f, priority: SfxPriority.Low));
            Assert(lowFollow.Started, "用于抢占验证的 Low 跟随音效没有开始播放");

            Sfx3DPlaybackResult replacement = await service.PlaySfx3DAsync(
                AlternateAudioKey,
                Vector3.Zero,
                new Sfx3DPlaybackOptions(
                    50f,
                    priority: SfxPriority.Critical,
                    allowStealLowerPriority: true));
            Assert(replacement.Started, "Critical 静态 3D 音效没有抢占 Low 跟随 Voice");
            Assert(!service.IsSfx3DPlaying(lowFollow.Handle), "被抢占的跟随 Handle 仍然有效");
            AssertEqual(0, service.FollowingSfx3DCount, "被抢占后仍保留跟随绑定");
            Assert(!service.IsPhysicsProcessing(), "最后一个跟随被抢占后仍启用物理帧更新");
            service.StopAllSfx3D();
        }
        finally
        {
            service.StopAllSfx3D();
            if (target.IsInsideTree())
                RemoveChild(target);
            if (!target.IsQueuedForDeletion())
                target.QueueFree();
            if (service.IsInsideTree())
                service.GetParent()?.RemoveChild(service);
            if (!service.IsQueuedForDeletion())
                service.QueueFree();
        }
    }

    private async Task VerifySfx3DFollowLifecycle()
    {
        AudioService service = CreateIsolatedAudioService(3, 0, 2);
        var target = new Node3D { Name = "Sfx3DFollowLifecycleTarget" };
        var secondTarget = new Node3D { Name = "Sfx3DSecondFollowTarget" };
        var orphanTarget = new Node3D { Name = "Sfx3DOrphanFollowTarget" };
        AddChild(target);
        AddChild(secondTarget);

        try
        {
            AssertThrows<ArgumentException>(
                () => service.PlaySfx3DFollowAsync(
                    AudioKey,
                    orphanTarget,
                    Vector3.Zero,
                    new Sfx3DPlaybackOptions(50f)),
                "不在场景树中的跟随目标没有被拒绝");

            var options = new Sfx3DPlaybackOptions(50f);
            Sfx3DPlaybackResult first = await service.PlaySfx3DFollowAsync(
                AudioKey,
                target,
                Vector3.Zero,
                options);
            Sfx3DPlaybackResult second = await service.PlaySfx3DFollowAsync(
                AlternateAudioKey,
                secondTarget,
                Vector3.Zero,
                options);
            Assert(first.Started && second.Started, "跟随容量内的请求没有开始播放");

            Sfx3DPlaybackResult rejected = await service.PlaySfx3DFollowAsync(
                OneShotAudioKey,
                target,
                Vector3.Zero,
                options);
            AssertEqual(
                SfxPlaybackStatus.FollowCapacityReached,
                rejected.Status,
                "超过独立跟随上限没有返回结构化拒绝");
            AssertEqual(2, service.FollowingSfx3DCount, "跟随拒绝后活动数量错误");

            RemoveChild(target);
            service._PhysicsProcess(1d / 60d);
            Assert(!service.IsSfx3DPlaying(first.Handle), "目标离树后跟随音效仍在播放");
            AssertEqual(1, service.FollowingSfx3DCount, "目标离树后跟随绑定没有回收");
            Assert(service.IsPhysicsProcessing(), "仍有跟随音效时错误关闭了物理帧更新");

            RemoveChild(secondTarget);
            service._PhysicsProcess(1d / 60d);
            AssertEqual(0, service.FollowingSfx3DCount, "全部目标离树后仍有跟随绑定");
            Assert(!service.IsPhysicsProcessing(), "全部目标离树后仍启用物理帧更新");

            AddChild(target);
            Node runtime = GetNode<Node>("/root/GoDoRuntime");
            bool runtimeWasProcessing = runtime.IsProcessing();
            Task<Sfx3DPlaybackResult> pendingRequest;
            runtime.SetProcess(false);
            try
            {
                pendingRequest = service.PlaySfx3DFollowAsync(
                    AudioKey,
                    target,
                    Vector3.Zero,
                    options);
                RemoveChild(target);
            }
            finally
            {
                runtime.SetProcess(runtimeWasProcessing);
            }

            Sfx3DPlaybackResult unavailable = await pendingRequest;
            AssertEqual(
                SfxPlaybackStatus.TargetUnavailableBeforeStart,
                unavailable.Status,
                "加载期间离树的跟随目标没有返回结构化失败");
            AssertEqual(0, service.PendingSfx3DCount, "目标失效后仍占用待加载容量");
            AssertEqual(0, service.FollowingSfx3DCount, "目标失效后仍保留跟随绑定");
        }
        finally
        {
            service.StopAllSfx3D();
            if (target.IsInsideTree())
                RemoveChild(target);
            if (secondTarget.IsInsideTree())
                RemoveChild(secondTarget);
            if (!target.IsQueuedForDeletion())
                target.QueueFree();
            if (!secondTarget.IsQueuedForDeletion())
                secondTarget.QueueFree();
            if (!orphanTarget.IsQueuedForDeletion())
                orphanTarget.QueueFree();
            if (service.IsInsideTree())
                service.GetParent()?.RemoveChild(service);
            if (!service.IsQueuedForDeletion())
                service.QueueFree();
        }
    }

    private async Task VerifyExitCancellation()
    {
        Node runtime = GetNode<Node>("/root/GoDoRuntime");
        bool runtimeWasProcessing = runtime.IsProcessing();
        Task bgmRequest;
        Task<bool> sfxRequest;
        Task<Sfx3DPlaybackResult> sfx3DRequest;
        Task<Sfx3DPlaybackResult> followingSfx3DRequest;
        Task prepareRequest;
        var followTarget = new Node3D { Name = "ExitFollowTarget" };
        AddChild(followTarget);

        runtime.SetProcess(false);
        try
        {
            bgmRequest = _service.PlayBgmAsync(AudioKey);
            sfxRequest = _service.PlaySfxAsync(AudioKey);
            sfx3DRequest = _service.PlaySfx3DAsync(
                AudioKey,
                Vector3.Zero,
                new Sfx3DPlaybackOptions(50f));
            followingSfx3DRequest = _service.PlaySfx3DFollowAsync(
                AlternateAudioKey,
                followTarget,
                Vector3.Zero,
                new Sfx3DPlaybackOptions(50f));
            prepareRequest = _service.PrepareSfxAsync(AlternateAudioKey);
            RemoveChild(_service);
        }
        finally
        {
            runtime.SetProcess(runtimeWasProcessing);
        }

        await AssertThrowsAsync<OperationCanceledException>(
            () => bgmRequest,
            "AudioService 退出后 BGM 请求没有取消");
        await AssertThrowsAsync<OperationCanceledException>(
            () => sfxRequest,
            "AudioService 退出后 SFX 请求没有取消");
        await AssertThrowsAsync<OperationCanceledException>(
            () => sfx3DRequest,
            "AudioService 退出后 3D SFX 请求没有取消");
        await AssertThrowsAsync<OperationCanceledException>(
            () => followingSfx3DRequest,
            "AudioService 退出后待加载跟随 3D SFX 请求没有取消");
        await AssertThrowsAsync<OperationCanceledException>(
            () => prepareRequest,
            "AudioService 退出后 SFX 资源准备没有取消");

        RemoveChild(followTarget);
        followTarget.QueueFree();
    }

    private void CleanupService()
    {
        if (!GodotObject.IsInstanceValid(_service))
            return;

        if (_service.IsInsideTree())
            _service.GetParent()?.RemoveChild(_service);

        if (!_service.IsQueuedForDeletion())
            _service.QueueFree();
    }

    private AudioService CreateIsolatedAudioService(
        int maxVoices,
        int initialVoices = -1,
        int maxFollowingVoices = -1)
    {
        var bgmPlayer = new AudioStreamPlayer { Name = "BgmPlayer" };
        var secondaryBgmPlayer = new AudioStreamPlayer { Name = "BgmSecondaryPlayer" };
        var sfxRoot = new Node { Name = "SfxRoot" };
        var sfx3DRoot = new Node3D { Name = "Sfx3DRoot" };
        var service = new AudioService
        {
            Name = "ControlledAudioServiceUnderTest",
            BgmPlayerPath = new NodePath("BgmPlayer"),
            SecondaryBgmPlayerPath = new NodePath("BgmSecondaryPlayer"),
            SfxRootPath = new NodePath("SfxRoot"),
            SfxVoiceScene = ResourceHub.Load<PackedScene>(SfxVoiceKey),
            MaxSfxVoices = maxVoices,
            InitialSfxVoices = initialVoices < 0 ? maxVoices : initialVoices,
            Sfx3DRootPath = new NodePath("Sfx3DRoot"),
            Sfx3DVoiceScene = ResourceHub.Load<PackedScene>(Sfx3DVoiceKey),
            MaxSfx3DVoices = maxVoices,
            MaxFollowingSfx3DVoices = maxFollowingVoices < 0 ? maxVoices : maxFollowingVoices,
            InitialSfx3DVoices = 0,
        };
        service.AddChild(bgmPlayer);
        service.AddChild(secondaryBgmPlayer);
        service.AddChild(sfxRoot);
        service.AddChild(sfx3DRoot);
        AddChild(service);
        return service;
    }

    private SfxVoice GetActiveSfxVoice()
    {
        AssertEqual(1, _sfxRoot.GetChildCount(), "SFX Root 活动 Voice 数量错误");
        return _sfxRoot.GetChild(0) as SfxVoice ??
            throw new InvalidOperationException("SFX Root 子节点不是 SfxVoice");
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

    private async Task WaitUntilAsync(
        Func<bool> condition,
        string timeoutMessage,
        double timeoutSeconds = 1d)
    {
        long started = Stopwatch.GetTimestamp();
        long timeoutTicks = (long)(timeoutSeconds * Stopwatch.Frequency);
        while (!condition() && Stopwatch.GetTimestamp() - started < timeoutTicks)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Assert(condition(), timeoutMessage);
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertNear(float expected, float actual, string message)
    {
        if (!Mathf.IsEqualApprox(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}；期望 {expected}，实际 {actual}");
        }
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
