using Godot;

namespace GoDo;

/// <summary>
/// 单次 3D 空间 SFX 的距离衰减、播放参数与突发准入策略。
/// 必须显式提供最大可听距离，避免无界空间 Voice 持续参与混音。
/// </summary>
public readonly struct Sfx3DPlaybackOptions
{
    /// <summary>超过此距离后不再听到本次声音，必须为有限正数。</summary>
    public float MaxDistance { get; }

    /// <summary>距离衰减的基准尺寸，必须为有限正数。</summary>
    public float UnitSize { get; }

    /// <summary>本次播放的有限线性音量，范围为 0 到 1。</summary>
    public float VolumeLinear { get; }

    /// <summary>本次播放的有限正数音高与速度倍率。</summary>
    public float PitchScale { get; }

    /// <summary>本次播放采用的 Godot 距离衰减模型。</summary>
    public AudioStreamPlayer3D.AttenuationModelEnum AttenuationModel { get; }

    /// <summary>本次请求的相对优先级。</summary>
    public SfxPriority Priority { get; }

    /// <summary>相同 ResourceKey 允许的活动加待加载数量；0 表示不单独限制。</summary>
    public int MaxConcurrentPerKey { get; }

    /// <summary>3D 容量满时是否允许取代更低优先级的活动或待加载请求。</summary>
    public bool AllowStealLowerPriority { get; }

    /// <summary>创建一组 3D 空间 SFX 播放选项；参数在提交给 AudioService 时统一校验。</summary>
    /// <param name="maxDistance">超过此距离后不再听到声音。</param>
    /// <param name="unitSize">距离衰减的基准尺寸。</param>
    /// <param name="volumeLinear">本次播放的线性音量。</param>
    /// <param name="pitchScale">本次播放的音高与速度倍率。</param>
    /// <param name="attenuationModel">Godot 距离衰减模型。</param>
    /// <param name="priority">突发准入优先级。</param>
    /// <param name="maxConcurrentPerKey">相同资源并发上限；0 表示不限制。</param>
    /// <param name="allowStealLowerPriority">容量满时是否允许抢占更低优先级请求。</param>
    public Sfx3DPlaybackOptions(
        float maxDistance,
        float unitSize = 10f,
        float volumeLinear = 1f,
        float pitchScale = 1f,
        AudioStreamPlayer3D.AttenuationModelEnum attenuationModel =
            AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
        SfxPriority priority = SfxPriority.Normal,
        int maxConcurrentPerKey = 0,
        bool allowStealLowerPriority = false)
    {
        MaxDistance = maxDistance;
        UnitSize = unitSize;
        VolumeLinear = volumeLinear;
        PitchScale = pitchScale;
        AttenuationModel = attenuationModel;
        Priority = priority;
        MaxConcurrentPerKey = maxConcurrentPerKey;
        AllowStealLowerPriority = allowStealLowerPriority;
    }
}
