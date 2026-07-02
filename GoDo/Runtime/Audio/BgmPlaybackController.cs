using System;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>AudioService 内部的单路 BGM 加载与播放控制。</summary>
internal sealed class BgmPlaybackController
{
    private readonly AudioStreamPlayer _player;
    private int _requestVersion;

    public ResourceKey? CurrentBgm { get; private set; }
    public bool IsPlaying => _player.Playing;
    public bool IsLoading { get; private set; }

    public BgmPlaybackController(AudioStreamPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _player.Bus = AudioBusController.BgmBus;
    }

    public async Task PlayAsync(ResourceKey key, bool restart)
    {
        RuntimeThreadGuard.VerifyAccess();

        if (IsLoading)
            throw new InvalidOperationException("已有背景音乐正在加载，不能重复发起请求。");

        if (CurrentBgm == key)
        {
            if (restart)
            {
                _player.StreamPaused = false;
                _player.Play();
            }

            return;
        }

        IsLoading = true;
        int requestVersion = ++_requestVersion;

        try
        {
            ResourceLoadOperation<AudioStream> operation = ResourceHub.LoadAsync<AudioStream>(key);
            AudioStream stream = await operation.Completion;
            RuntimeThreadGuard.VerifyAccess();

            if (requestVersion != _requestVersion)
                throw new OperationCanceledException("背景音乐加载完成前已被停止。");

            _player.Stop();
            _player.Stream = stream;
            _player.StreamPaused = false;
            _player.Play();
            CurrentBgm = key;
        }
        catch (Exception exception) when (
            exception is not AudioPlaybackException &&
            exception is not OperationCanceledException)
        {
            throw new AudioPlaybackException(
                key,
                AudioGroup.Bgm,
                $"背景音乐加载或播放准备失败: {key.Value}",
                exception);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Pause()
    {
        RuntimeThreadGuard.VerifyAccess();
        if (_player.Stream != null && _player.Playing)
            _player.StreamPaused = true;
    }

    public void Resume()
    {
        RuntimeThreadGuard.VerifyAccess();
        if (_player.Stream != null && _player.StreamPaused)
            _player.StreamPaused = false;
    }

    public void Stop()
    {
        RuntimeThreadGuard.VerifyAccess();
        _requestVersion++;
        _player.Stop();
        _player.StreamPaused = false;
        _player.Stream = null;
        CurrentBgm = null;
    }
}
