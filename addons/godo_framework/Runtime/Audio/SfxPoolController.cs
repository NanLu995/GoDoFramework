using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>AudioService 内部的非空间音效池与并发限制。</summary>
internal sealed class SfxPoolController : IDisposable
{
    private delegate TResult PlaybackResultFactory<TResult>(
        SfxPlaybackStatus status,
        SfxPlaybackHandle handle);

    private static readonly PlaybackResultFactory<bool> LegacyResultFactory =
        static (status, _) => status == SfxPlaybackStatus.Started;
    private static readonly PlaybackResultFactory<SfxPlaybackResult> ControlledResultFactory =
        static (status, handle) => new SfxPlaybackResult(status, handle);

    private readonly NodePool<SfxVoice> _pool;
    private readonly Node _voiceRoot;
    private readonly SfxAdmissionState<SfxVoice> _admission;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly int _maxVoices;
    private int _requestVersion;
    private bool _disposed;

    public int ActiveCount => _admission.ActiveCount;
    public int PendingCount => _admission.PendingCount;
    public int PreparedCount => _pool.ActiveCount + _pool.IdleCount;
    public int MaxVoices => _maxVoices;
    public long RejectedCount => _admission.RejectedCount;
    public long PreemptedCount => _admission.PreemptedCount;

    public SfxPoolController(
        PackedScene voiceScene,
        Node voiceRoot,
        int maxVoices,
        int initialVoices)
    {
        _voiceRoot = voiceRoot ?? throw new ArgumentNullException(nameof(voiceRoot));

        if (maxVoices <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxVoices), "最大音效数量必须大于 0。");
        if (initialVoices < 0 || initialVoices > maxVoices)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialVoices),
                "初始音效数量必须在 0 到最大音效数量之间。");
        }

        _maxVoices = maxVoices;
        _admission = new SfxAdmissionState<SfxVoice>(maxVoices);
        _lifetimeToken = _lifetimeCancellation.Token;
        _pool = new NodePool<SfxVoice>(voiceScene, initialVoices, maxVoices);
    }

    public Task<bool> PlayAsync(ResourceKey key) =>
        PlayCoreAsync(
            key,
            new SfxPlaybackOptions(),
            LegacyResultFactory);

    public Task<bool> PlayAsync(
        ResourceKey key,
        float volumeLinear,
        float pitchScale)
    {
        ValidatePlaybackParameters(volumeLinear, pitchScale);
        return PlayCoreAsync(
            key,
            new SfxPlaybackOptions(volumeLinear, pitchScale),
            LegacyResultFactory);
    }

    public Task<SfxPlaybackResult> PlayAsync(
        ResourceKey key,
        SfxPlaybackOptions options)
    {
        options = options.Normalize();
        ValidateOptions(options);
        return PlayCoreAsync(key, options, ControlledResultFactory);
    }

    public Task PrepareAsync(
        ResourceKey key,
        CancellationToken cancellationToken)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        return PrepareCoreAsync(key, cancellationToken);
    }

    public int Prewarm(int targetVoiceCount)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        if (targetVoiceCount < 0 || targetVoiceCount > _maxVoices)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetVoiceCount),
                $"SFX Voice 预热目标必须在 0 到 {_maxVoices} 之间。");
        }

        return _pool.PrewarmTo(targetVoiceCount);
    }

    public bool IsPlaying(SfxPlaybackHandle handle)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        return handle.IsValid && _admission.TryGetVoice(handle.Value, out _);
    }

    public bool TryStop(SfxPlaybackHandle handle)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        if (!handle.IsValid || !_admission.TryGetVoice(handle.Value, out SfxVoice? voice))
            return false;

        ReleaseVoice(voice!);
        return true;
    }

    private async Task<TResult> PlayCoreAsync<TResult>(
        ResourceKey key,
        SfxPlaybackOptions options,
        PlaybackResultFactory<TResult> resultFactory)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        SfxAdmissionState<SfxVoice>.AdmissionDecision admission = _admission.Reserve(
            key,
            options.Priority,
            options.MaxConcurrentPerKey,
            options.AllowStealLowerPriority);
        if (!admission.Accepted)
            return resultFactory(admission.Status, default);

        if (admission.PreemptedVoice != null)
            RecyclePreemptedVoice(admission.PreemptedVoice);

        ulong playbackId = admission.PlaybackId;
        int requestVersion = _requestVersion;
        bool reservationTransferred = false;
        bool reservationReleased = false;

        try
        {
            ResourceLoadOperation<AudioStream> operation = ResourceHub.LoadAsync<AudioStream>(key);
            AudioStream stream;
            try
            {
                stream = await operation.Completion;
            }
            catch
            {
                if (requestVersion != _requestVersion)
                    throw new OperationCanceledException("音效加载完成前已停止全部音效。");
                if (_admission.ConsumePreempted(playbackId))
                {
                    reservationReleased = true;
                    return resultFactory(SfxPlaybackStatus.PreemptedBeforeStart, default);
                }

                throw;
            }

            if (requestVersion != _requestVersion)
                throw new OperationCanceledException("音效加载完成前已停止全部音效。");
            if (_admission.ConsumePreempted(playbackId))
            {
                reservationReleased = true;
                return resultFactory(SfxPlaybackStatus.PreemptedBeforeStart, default);
            }

            MainThreadGuard.VerifyAccess();
            ThrowIfDisposed();

            SfxVoice voice = _pool.Acquire(_voiceRoot);
            var handle = new SfxPlaybackHandle(playbackId);
            if (!_admission.Activate(playbackId, voice))
            {
                _pool.Release(voice);
                throw new InvalidOperationException("SFX 请求的准入预留状态已丢失。");
            }

            voice.PlaybackFinished += OnPlaybackFinished;
            reservationTransferred = true;

            try
            {
                voice.PlayStream(stream, options.VolumeLinear, options.PitchScale);
            }
            catch
            {
                ReleaseVoice(voice);
                throw;
            }

            return resultFactory(SfxPlaybackStatus.Started, handle);
        }
        catch (Exception exception) when (
            exception is not AudioPlaybackException &&
            exception is not OperationCanceledException)
        {
            throw new AudioPlaybackException(
                key,
                AudioGroup.Sfx,
                $"音效加载或播放准备失败: {key.Value}",
                exception);
        }
        finally
        {
            if (!reservationTransferred && !reservationReleased)
            {
                _admission.AbandonPending(playbackId);
            }
        }
    }

    private async Task PrepareCoreAsync(
        ResourceKey key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource? linkedCancellation = null;

        try
        {
            CancellationToken waitToken = _lifetimeToken;
            if (cancellationToken.CanBeCanceled)
            {
                linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _lifetimeToken);
                waitToken = linkedCancellation.Token;
            }

            ResourceLoadOperation<AudioStream> operation =
                ResourceHub.LoadAsync<AudioStream>(key);
            await operation.Completion.WaitAsync(waitToken);
        }
        catch (OperationCanceledException exception)
            when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "调用方取消了 SFX 资源准备。底层共享加载可能仍会完成。",
                exception,
                cancellationToken);
        }
        catch (OperationCanceledException exception)
            when (_lifetimeToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "AudioService 已退出，SFX 资源准备停止等待。底层共享加载可能仍会完成。",
                exception,
                _lifetimeToken);
        }
        catch (Exception exception) when (exception is not AudioPlaybackException)
        {
            throw new AudioPlaybackException(
                key,
                AudioGroup.Sfx,
                $"音效资源准备失败: {key.Value}",
                exception);
        }
        finally
        {
            linkedCancellation?.Dispose();
        }
    }

    private static void ValidatePlaybackParameters(
        float volumeLinear,
        float pitchScale)
    {
        if (!float.IsFinite(volumeLinear) || volumeLinear < 0f || volumeLinear > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(volumeLinear),
                "单次音效线性音量必须是 0 到 1 之间的有限值。");
        }

        if (!float.IsFinite(pitchScale) || pitchScale <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pitchScale),
                "单次音效音高倍率必须是有限正数。");
        }
    }

    private static void ValidateOptions(SfxPlaybackOptions options)
    {
        ValidatePlaybackParameters(options.VolumeLinear, options.PitchScale);

        if (options.Priority < SfxPriority.Low || options.Priority > SfxPriority.Critical)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "SFX 优先级不是已定义值。");
        }

        if (options.MaxConcurrentPerKey < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "相同 SFX 并发上限不能小于 0。");
        }
    }

    public void StopAll()
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        _requestVersion++;
        _admission.CancelPending();

        SfxVoice[] voices = _admission.GetActiveSnapshot();
        for (int i = 0; i < voices.Length; i++)
            ReleaseVoice(voices[i]);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _lifetimeCancellation.Cancel();
        StopAll();
        _pool.Dispose();
        _lifetimeCancellation.Dispose();
        _disposed = true;
    }

    private void OnPlaybackFinished(SfxVoice voice)
    {
        MainThreadGuard.VerifyAccess();
        if (!_disposed)
            ReleaseVoice(voice);
    }

    private void ReleaseVoice(SfxVoice voice)
    {
        if (!_admission.Release(voice))
            return;

        RecyclePreemptedVoice(voice);
    }

    private void RecyclePreemptedVoice(SfxVoice voice)
    {
        voice.PlaybackFinished -= OnPlaybackFinished;
        _pool.Release(voice);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

}
