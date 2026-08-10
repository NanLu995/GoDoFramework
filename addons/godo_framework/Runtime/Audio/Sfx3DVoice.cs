using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>NodePool 管理的单路 3D 空间音效播放器。</summary>
public sealed partial class Sfx3DVoice : AudioStreamPlayer3D, IPoolable
{
    internal event Action<Sfx3DVoice>? PlaybackFinished;

    /// <inheritdoc />
    public void OnAcquire()
    {
        Bus = AudioBusController.SfxBus;
        Stream = null;
        StreamPaused = false;
        VolumeLinear = 1f;
        PitchScale = 1f;
        AttenuationModel = AttenuationModelEnum.InverseDistance;
        UnitSize = 10f;
        MaxDistance = 0f;
        DopplerTracking = DopplerTrackingEnum.Disabled;
        EmissionAngleEnabled = false;
        Finished += OnFinished;
    }

    /// <inheritdoc />
    public void OnRelease()
    {
        Finished -= OnFinished;
        Stop();
        StreamPaused = false;
        Stream = null;
        PlaybackFinished = null;
    }

    internal void PlayStream(
        AudioStream stream,
        Vector3 globalPosition,
        Sfx3DPlaybackOptions options)
    {
        GlobalPosition = globalPosition;
        MaxDistance = options.MaxDistance;
        UnitSize = options.UnitSize;
        VolumeLinear = options.VolumeLinear;
        PitchScale = options.PitchScale;
        AttenuationModel = options.AttenuationModel;
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        Play();
    }

    private void OnFinished()
    {
        PlaybackFinished?.Invoke(this);
    }
}
