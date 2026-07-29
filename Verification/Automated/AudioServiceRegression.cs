using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private static readonly ResourceKey MissingKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/Audio/Missing.tres");
    private static readonly ResourceKey SfxVoiceKey =
        ResourceKey.Create("res://addons/godo_framework/Runtime/Audio/SfxVoice.tscn");

    private AudioService _service = null!;
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
            await RunAsync("SFX 容量与 StopAll", VerifySfxCapacityAndStopAll);
            await RunAsync("SFX 自然结束回收", VerifySfxNaturalCompletion);
            await RunAsync("100 路缓存突发", VerifyCachedBurst);
            await RunAsync("服务退出取消等待", VerifyExitCancellation);

            CleanupService();
            await ToSignal(
                GetTree().CreateTimer(0.2),
                SceneTreeTimer.SignalName.Timeout);
            GD.Print($"[AudioServiceRegression] PASS ({_passed}/8)");
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
        var bgmPlayer = new AudioStreamPlayer { Name = "BgmPlayer" };
        var sfxRoot = new Node { Name = "SfxRoot" };
        _service = new AudioService
        {
            Name = "AudioServiceUnderTest",
            BgmPlayerPath = new NodePath("BgmPlayer"),
            SfxRootPath = new NodePath("SfxRoot"),
            SfxVoiceScene = ResourceHub.Load<PackedScene>(SfxVoiceKey),
            MaxSfxVoices = MaxVoices,
            InitialSfxVoices = 8,
        };
        _service.AddChild(bgmPlayer);
        _service.AddChild(sfxRoot);
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

            _service.StopBgm();
            Assert(!_service.IsBgmLoading, "StopBgm 没有立即释放加载状态");

            replacementRequest = _service.PlayBgmAsync(AudioKey);
            Assert(_service.IsBgmLoading, "StopBgm 后无法立即开始新请求");
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

    private async Task VerifySfxNaturalCompletion()
    {
        bool played = await _service.PlaySfxAsync(OneShotAudioKey);
        Assert(played, "非循环 SFX 没有开始播放");
        AssertEqual(1, _service.ActiveSfxCount, "非循环 SFX 活动数量错误");

        await ToSignal(
            GetTree().CreateTimer(0.1),
            SceneTreeTimer.SignalName.Timeout);
        AssertEqual(0, _service.ActiveSfxCount, "非循环 SFX 结束后没有自动归还");
    }

    private async Task VerifyExitCancellation()
    {
        Node runtime = GetNode<Node>("/root/GoDoRuntime");
        bool runtimeWasProcessing = runtime.IsProcessing();
        Task bgmRequest;
        Task<bool> sfxRequest;

        runtime.SetProcess(false);
        try
        {
            bgmRequest = _service.PlayBgmAsync(AudioKey);
            sfxRequest = _service.PlaySfxAsync(AudioKey);
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
