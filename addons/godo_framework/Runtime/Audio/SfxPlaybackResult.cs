namespace GoDo;

/// <summary>受控 SFX 播放的结构化结果。</summary>
public readonly struct SfxPlaybackResult
{
    /// <summary>请求的准入或启动状态。</summary>
    public SfxPlaybackStatus Status { get; }

    /// <summary>成功开始播放时返回的句柄；其他状态下为无效句柄。</summary>
    public SfxPlaybackHandle Handle { get; }

    /// <summary>请求是否已经开始播放。</summary>
    public bool Started => Status == SfxPlaybackStatus.Started;

    internal SfxPlaybackResult(
        SfxPlaybackStatus status,
        SfxPlaybackHandle handle = default)
    {
        Status = status;
        Handle = handle;
    }
}
