namespace GoDo;

/// <summary>受控 3D 空间 SFX 播放的结构化结果。</summary>
public readonly struct Sfx3DPlaybackResult
{
    /// <summary>请求的准入或启动状态。</summary>
    public SfxPlaybackStatus Status { get; }

    /// <summary>成功开始播放时返回的 3D 句柄；其他状态下为无效句柄。</summary>
    public Sfx3DPlaybackHandle Handle { get; }

    /// <summary>请求是否已经开始播放。</summary>
    public bool Started => Status == SfxPlaybackStatus.Started;

    internal Sfx3DPlaybackResult(
        SfxPlaybackStatus status,
        Sfx3DPlaybackHandle handle = default)
    {
        Status = status;
        Handle = handle;
    }
}
