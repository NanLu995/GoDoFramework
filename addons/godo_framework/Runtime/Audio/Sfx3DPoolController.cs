using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>AudioService 内部的 3D 空间音效池与并发限制。</summary>
internal sealed class Sfx3DPoolController : IDisposable
{
    private readonly NodePool<Sfx3DVoice> _pool;
    private readonly Node3D _voiceRoot;
    private readonly SfxAdmissionState<Sfx3DVoice> _admission;
    private readonly Dictionary<Sfx3DVoice, FollowBinding> _followBindings;
    private readonly HashSet<ulong> _pendingFollowRequests;
    private readonly Sfx3DVoice[] _invalidFollowVoices;
    private readonly Action<bool> _setFollowProcessing;
    private readonly int _maxVoices;
    private readonly int _maxFollowingVoices;
    private bool _followProcessingEnabled;
    private int _requestVersion;
    private bool _disposed;

    public int ActiveCount => _admission.ActiveCount;
    public int PendingCount => _admission.PendingCount;
    public int PreparedCount => _pool.ActiveCount + _pool.IdleCount;
    public int MaxVoices => _maxVoices;
    public int FollowingCount => _followBindings.Count;
    public int MaxFollowingVoices => _maxFollowingVoices;
    public long RejectedCount => _admission.RejectedCount;
    public long PreemptedCount => _admission.PreemptedCount;

    public Sfx3DPoolController(
        PackedScene voiceScene,
        Node3D voiceRoot,
        int maxVoices,
        int initialVoices,
        int maxFollowingVoices,
        Action<bool> setFollowProcessing)
    {
        _voiceRoot = voiceRoot ?? throw new ArgumentNullException(nameof(voiceRoot));

        if (maxVoices <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxVoices), "最大 3D 音效数量必须大于 0。");
        if (initialVoices < 0 || initialVoices > maxVoices)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialVoices),
                "初始 3D 音效数量必须在 0 到最大 3D 音效数量之间。");
        }
        if (maxFollowingVoices < 0 || maxFollowingVoices > maxVoices)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxFollowingVoices),
                "3D 跟随数量上限必须在 0 到最大 3D 音效数量之间。");
        }

        _maxVoices = maxVoices;
        _maxFollowingVoices = maxFollowingVoices;
        _setFollowProcessing = setFollowProcessing ??
            throw new ArgumentNullException(nameof(setFollowProcessing));
        _admission = new SfxAdmissionState<Sfx3DVoice>(maxVoices);
        _followBindings = new Dictionary<Sfx3DVoice, FollowBinding>(
            maxFollowingVoices,
            ReferenceEqualityComparer.Instance);
        _pendingFollowRequests = new HashSet<ulong>(maxFollowingVoices);
        _invalidFollowVoices = new Sfx3DVoice[maxFollowingVoices];
        _pool = new NodePool<Sfx3DVoice>(voiceScene, initialVoices, maxVoices);
    }

    public Task<Sfx3DPlaybackResult> PlayAsync(
        ResourceKey key,
        Vector3 globalPosition,
        Sfx3DPlaybackOptions options)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        Validate(globalPosition, options);
        return PlayCoreAsync(key, globalPosition, options, null);
    }

    public Task<Sfx3DPlaybackResult> PlayFollowAsync(
        ResourceKey key,
        Node3D target,
        Vector3 localOffset,
        Sfx3DPlaybackOptions options)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        ValidateFollowTarget(target);
        if (!localOffset.IsFinite())
        {
            throw new ArgumentOutOfRangeException(
                nameof(localOffset),
                "3D SFX 跟随偏移必须是有限值。");
        }

        Validate(Vector3.Zero, options);
        if (_followBindings.Count + _pendingFollowRequests.Count >= _maxFollowingVoices)
        {
            _admission.RecordRejection();
            return Task.FromResult(
                new Sfx3DPlaybackResult(SfxPlaybackStatus.FollowCapacityReached));
        }

        return PlayCoreAsync(
            key,
            Vector3.Zero,
            options,
            new FollowRequest(target, localOffset));
    }

    public int Prewarm(int targetVoiceCount)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        if (targetVoiceCount < 0 || targetVoiceCount > _maxVoices)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetVoiceCount),
                $"3D SFX Voice 预热目标必须在 0 到 {_maxVoices} 之间。");
        }

        return _pool.PrewarmTo(targetVoiceCount);
    }

    public bool IsPlaying(Sfx3DPlaybackHandle handle)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        return handle.IsValid && _admission.TryGetVoice(handle.Value, out _);
    }

    public bool TryStop(Sfx3DPlaybackHandle handle)
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        if (!handle.IsValid ||
            !_admission.TryGetVoice(handle.Value, out Sfx3DVoice? voice))
            return false;

        ReleaseVoice(voice!);
        return true;
    }

    private async Task<Sfx3DPlaybackResult> PlayCoreAsync(
        ResourceKey key,
        Vector3 globalPosition,
        Sfx3DPlaybackOptions options,
        FollowRequest? followRequest)
    {
        SfxAdmissionState<Sfx3DVoice>.AdmissionDecision admission = _admission.Reserve(
            key,
            options.Priority,
            options.MaxConcurrentPerKey,
            options.AllowStealLowerPriority);
        if (!admission.Accepted)
            return new Sfx3DPlaybackResult(admission.Status);

        if (admission.PreemptedVoice != null)
            RecyclePreemptedVoice(admission.PreemptedVoice);
        if (admission.PreemptedPendingId != 0)
            _pendingFollowRequests.Remove(admission.PreemptedPendingId);

        ulong playbackId = admission.PlaybackId;
        if (followRequest.HasValue)
            _pendingFollowRequests.Add(playbackId);
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
                    throw new OperationCanceledException("3D 音效加载完成前已停止全部 3D 音效。");
                if (_admission.ConsumePreempted(playbackId))
                {
                    reservationReleased = true;
                    return new Sfx3DPlaybackResult(SfxPlaybackStatus.PreemptedBeforeStart);
                }

                throw;
            }

            if (requestVersion != _requestVersion)
                throw new OperationCanceledException("3D 音效加载完成前已停止全部 3D 音效。");
            if (_admission.ConsumePreempted(playbackId))
            {
                reservationReleased = true;
                return new Sfx3DPlaybackResult(SfxPlaybackStatus.PreemptedBeforeStart);
            }

            MainThreadGuard.VerifyAccess();
            ThrowIfDisposed();

            FollowBinding? followBinding = null;
            Vector3 playbackPosition = globalPosition;
            if (followRequest is FollowRequest follow)
            {
                if (!IsFollowTargetAvailable(follow.Target))
                {
                    return new Sfx3DPlaybackResult(
                        SfxPlaybackStatus.TargetUnavailableBeforeStart);
                }

                followBinding = new FollowBinding(follow.Target, follow.LocalOffset);
                playbackPosition = follow.Target.ToGlobal(follow.LocalOffset);
            }

            Sfx3DVoice voice = _pool.Acquire(_voiceRoot);
            var handle = new Sfx3DPlaybackHandle(playbackId);
            if (!_admission.Activate(playbackId, voice))
            {
                _pool.Release(voice);
                throw new InvalidOperationException("3D SFX 请求的准入预留状态已丢失。");
            }

            reservationTransferred = true;
            voice.PlaybackFinished += OnPlaybackFinished;

            try
            {
                if (followBinding.HasValue)
                {
                    _pendingFollowRequests.Remove(playbackId);
                    _followBindings.Add(voice, followBinding.Value);
                    RefreshFollowProcessing();
                }

                voice.PlayStream(stream, playbackPosition, options);
            }
            catch
            {
                ReleaseVoice(voice);
                throw;
            }

            return new Sfx3DPlaybackResult(SfxPlaybackStatus.Started, handle);
        }
        catch (Exception exception) when (
            exception is not AudioPlaybackException &&
            exception is not OperationCanceledException)
        {
            throw new AudioPlaybackException(
                key,
                AudioGroup.Sfx,
                $"3D 音效加载或播放准备失败: {key.Value}",
                exception);
        }
        finally
        {
            _pendingFollowRequests.Remove(playbackId);
            if (!reservationTransferred && !reservationReleased)
                _admission.AbandonPending(playbackId);
        }
    }

    public void PhysicsUpdateFollowingVoices()
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        int invalidCount = 0;
        foreach (KeyValuePair<Sfx3DVoice, FollowBinding> pair in _followBindings)
        {
            FollowBinding binding = pair.Value;
            if (!IsFollowTargetAvailable(binding.Target))
            {
                _invalidFollowVoices[invalidCount++] = pair.Key;
                continue;
            }

            pair.Key.GlobalPosition = binding.Target.ToGlobal(binding.LocalOffset);
        }

        for (int index = 0; index < invalidCount; index++)
        {
            Sfx3DVoice voice = _invalidFollowVoices[index];
            _invalidFollowVoices[index] = null!;
            ReleaseVoice(voice);
        }
    }

    public void StopAll()
    {
        MainThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        _requestVersion++;
        _admission.CancelPending();
        _pendingFollowRequests.Clear();

        Sfx3DVoice[] voices = _admission.GetActiveSnapshot();
        for (int i = 0; i < voices.Length; i++)
            ReleaseVoice(voices[i]);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopAll();
        _pool.Dispose();
        _disposed = true;
    }

    private void OnPlaybackFinished(Sfx3DVoice voice)
    {
        MainThreadGuard.VerifyAccess();
        if (!_disposed)
            ReleaseVoice(voice);
    }

    private void ReleaseVoice(Sfx3DVoice voice)
    {
        if (!_admission.Release(voice))
            return;

        RecyclePreemptedVoice(voice);
    }

    private void RecyclePreemptedVoice(Sfx3DVoice voice)
    {
        if (_followBindings.Remove(voice))
            RefreshFollowProcessing();

        voice.PlaybackFinished -= OnPlaybackFinished;
        _pool.Release(voice);
    }

    private void RefreshFollowProcessing()
    {
        bool shouldProcess = _followBindings.Count > 0;
        if (_followProcessingEnabled == shouldProcess)
            return;

        _followProcessingEnabled = shouldProcess;
        _setFollowProcessing(shouldProcess);
    }

    private static void ValidateFollowTarget(Node3D target)
    {
        if (!IsFollowTargetAvailable(target))
        {
            throw new ArgumentException(
                "3D SFX 跟随目标必须有效、位于场景树中且未进入删除队列。",
                nameof(target));
        }
    }

    private static bool IsFollowTargetAvailable(Node3D? target) =>
        target != null &&
        GodotObject.IsInstanceValid(target) &&
        target.IsInsideTree() &&
        !target.IsQueuedForDeletion();

    private static void Validate(
        Vector3 globalPosition,
        Sfx3DPlaybackOptions options)
    {
        if (!globalPosition.IsFinite())
        {
            throw new ArgumentOutOfRangeException(
                nameof(globalPosition),
                "3D SFX 世界坐标必须是有限值。");
        }

        if (!float.IsFinite(options.MaxDistance) || options.MaxDistance <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "3D SFX 最大可听距离必须是有限正数。");
        }

        if (!float.IsFinite(options.UnitSize) || options.UnitSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "3D SFX 衰减基准尺寸必须是有限正数。");
        }

        if (!float.IsFinite(options.VolumeLinear) ||
            options.VolumeLinear < 0f ||
            options.VolumeLinear > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "3D SFX 线性音量必须是 0 到 1 之间的有限值。");
        }

        if (!float.IsFinite(options.PitchScale) || options.PitchScale <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "3D SFX 音高倍率必须是有限正数。");
        }

        if (options.AttenuationModel <
                AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance ||
            options.AttenuationModel >
                AudioStreamPlayer3D.AttenuationModelEnum.Disabled)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "3D SFX 距离衰减模型不是已定义值。");
        }

        if (options.Priority < SfxPriority.Low ||
            options.Priority > SfxPriority.Critical)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "3D SFX 优先级不是已定义值。");
        }

        if (options.MaxConcurrentPerKey < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "相同 3D SFX 并发上限不能小于 0。");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private readonly record struct FollowRequest(
        Node3D Target,
        Vector3 LocalOffset);

    private readonly record struct FollowBinding(
        Node3D Target,
        Vector3 LocalOffset);
}
