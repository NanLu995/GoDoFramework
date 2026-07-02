using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>AudioService 内部的非空间音效池与并发限制。</summary>
internal sealed class SfxPoolController : IDisposable
{
    private readonly NodePool<SfxVoice> _pool;
    private readonly Node _voiceRoot;
    private readonly HashSet<SfxVoice> _activeVoices = new(ReferenceEqualityComparer.Instance);
    private readonly int _maxVoices;
    private int _pendingLoads;
    private int _requestVersion;
    private bool _disposed;

    public int ActiveCount => _activeVoices.Count;
    public int MaxVoices => _maxVoices;

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
        _pool = new NodePool<SfxVoice>(voiceScene, initialVoices, maxVoices);
    }

    public async Task<bool> PlayAsync(ResourceKey key)
    {
        RuntimeThreadGuard.VerifyAccess();
        ThrowIfDisposed();

        if (_activeVoices.Count + _pendingLoads >= _maxVoices)
            return false;

        _pendingLoads++;
        int requestVersion = _requestVersion;

        try
        {
            ResourceLoadOperation<AudioStream> operation = ResourceHub.LoadAsync<AudioStream>(key);
            AudioStream stream = await operation.Completion;
            RuntimeThreadGuard.VerifyAccess();
            ThrowIfDisposed();

            if (requestVersion != _requestVersion)
                throw new OperationCanceledException("音效加载完成前已停止全部音效。");

            if (_activeVoices.Count >= _maxVoices)
                return false;

            SfxVoice voice = _pool.Acquire(_voiceRoot);
            _activeVoices.Add(voice);
            voice.PlaybackFinished += OnPlaybackFinished;

            try
            {
                voice.PlayStream(stream);
            }
            catch
            {
                ReleaseVoice(voice);
                throw;
            }

            return true;
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
            _pendingLoads--;
        }
    }

    public void StopAll()
    {
        RuntimeThreadGuard.VerifyAccess();
        ThrowIfDisposed();
        _requestVersion++;

        if (_activeVoices.Count == 0)
            return;

        var voices = new SfxVoice[_activeVoices.Count];
        _activeVoices.CopyTo(voices);
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

    private void OnPlaybackFinished(SfxVoice voice)
    {
        RuntimeThreadGuard.VerifyAccess();
        if (!_disposed)
            ReleaseVoice(voice);
    }

    private void ReleaseVoice(SfxVoice voice)
    {
        if (!_activeVoices.Remove(voice))
            return;

        voice.PlaybackFinished -= OnPlaybackFinished;
        _pool.Release(voice);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
