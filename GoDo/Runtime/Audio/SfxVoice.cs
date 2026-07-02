using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>NodePool 管理的单路非空间音效播放器。</summary>
public sealed partial class SfxVoice : AudioStreamPlayer, IPoolable
{
    internal event Action<SfxVoice>? PlaybackFinished;

    public void OnAcquire()
    {
        Bus = AudioBusController.SfxBus;
        Stream = null;
        StreamPaused = false;
        VolumeLinear = 1f;
        PitchScale = 1f;
        Finished += OnFinished;
    }

    public void OnRelease()
    {
        Finished -= OnFinished;
        Stop();
        StreamPaused = false;
        Stream = null;
        PlaybackFinished = null;
    }

    internal void PlayStream(AudioStream stream)
    {
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Play();
    }

    private void OnFinished()
    {
        PlaybackFinished?.Invoke(this);
    }
}
