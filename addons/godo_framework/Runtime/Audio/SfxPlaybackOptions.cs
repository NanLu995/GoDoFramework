namespace GoDo;

/// <summary>
/// 单次受控 SFX 播放的参数与突发准入策略。该值类型可复用，不产生额外引用对象分配。
/// </summary>
public readonly struct SfxPlaybackOptions
{
    private readonly bool _initialized;

    /// <summary>本次播放的有限线性音量，范围为 0 到 1。</summary>
    public float VolumeLinear { get; }

    /// <summary>本次播放的有限正数音高与速度倍率。</summary>
    public float PitchScale { get; }

    /// <summary>本次请求的相对优先级。</summary>
    public SfxPriority Priority { get; }

    /// <summary>相同 ResourceKey 允许的活动加待加载数量；0 表示不单独限制。</summary>
    public int MaxConcurrentPerKey { get; }

    /// <summary>全局容量满时是否允许取代更低优先级的活动或待加载请求。</summary>
    public bool AllowStealLowerPriority { get; }

    /// <summary>创建使用默认音量、音高、普通优先级且不抢占的选项。</summary>
    public SfxPlaybackOptions()
        : this(1f, 1f, SfxPriority.Normal, 0, false)
    {
    }

    /// <summary>创建一组受控 SFX 播放选项；参数在提交给 AudioService 时统一校验。</summary>
    /// <param name="volumeLinear">本次播放的线性音量。</param>
    /// <param name="pitchScale">本次播放的音高与速度倍率。</param>
    /// <param name="priority">突发准入优先级。</param>
    /// <param name="maxConcurrentPerKey">相同资源并发上限；0 表示不限制。</param>
    /// <param name="allowStealLowerPriority">容量满时是否允许抢占更低优先级请求。</param>
    public SfxPlaybackOptions(
        float volumeLinear,
        float pitchScale = 1f,
        SfxPriority priority = SfxPriority.Normal,
        int maxConcurrentPerKey = 0,
        bool allowStealLowerPriority = false)
    {
        VolumeLinear = volumeLinear;
        PitchScale = pitchScale;
        Priority = priority;
        MaxConcurrentPerKey = maxConcurrentPerKey;
        AllowStealLowerPriority = allowStealLowerPriority;
        _initialized = true;
    }

    internal SfxPlaybackOptions Normalize() =>
        _initialized ? this : new SfxPlaybackOptions();
}
