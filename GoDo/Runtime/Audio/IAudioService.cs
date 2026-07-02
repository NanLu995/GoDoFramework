using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的背景音乐与音效服务。</summary>
public interface IAudioService
{
    /// <summary>当前背景音乐资源；未设置时为 null。</summary>
    ResourceKey? CurrentBgm { get; }

    /// <summary>背景音乐播放器当前是否正在播放。</summary>
    bool IsBgmPlaying { get; }

    /// <summary>当前是否正在加载背景音乐。</summary>
    bool IsBgmLoading { get; }

    /// <summary>当前活动音效数量。</summary>
    int ActiveSfxCount { get; }

    /// <summary>允许同时播放的最大音效数量。</summary>
    int MaxSfxVoices { get; }

    /// <summary>异步加载并播放背景音乐。</summary>
    /// <param name="key">AudioStream 资源键。</param>
    /// <param name="restart">同一资源正在播放时是否从头重新播放。</param>
    Task PlayBgmAsync(ResourceKey key, bool restart = false);

    /// <summary>暂停背景音乐。</summary>
    void PauseBgm();

    /// <summary>恢复背景音乐。</summary>
    void ResumeBgm();

    /// <summary>停止背景音乐并释放当前资源引用。</summary>
    void StopBgm();

    /// <summary>
    /// 异步加载并播放一次音效。达到并发上限时返回 false，不抢占正在播放的音效。
    /// </summary>
    Task<bool> PlaySfxAsync(ResourceKey key);

    /// <summary>停止并回收全部活动音效。</summary>
    void StopAllSfx();

    /// <summary>获取指定分组的线性音量，范围为 0 到 1。</summary>
    float GetVolume(AudioGroup group);

    /// <summary>设置指定分组的线性音量，范围为 0 到 1。</summary>
    void SetVolume(AudioGroup group, float linearVolume);
}
