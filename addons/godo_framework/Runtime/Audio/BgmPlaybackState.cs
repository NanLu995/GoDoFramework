namespace GoDo;

/// <summary>背景音乐服务当前的加载、播放与过渡状态。</summary>
public enum BgmPlaybackState
{
    /// <summary>未持有背景音乐资源，也没有正在执行的请求。</summary>
    Stopped,

    /// <summary>正在加载新的背景音乐；已有音乐可能继续播放。</summary>
    Loading,

    /// <summary>当前背景音乐正在播放。</summary>
    Playing,

    /// <summary>当前背景音乐或过渡已由业务显式暂停。</summary>
    Paused,

    /// <summary>两路背景音乐正在执行交叉淡化。</summary>
    Transitioning,

    /// <summary>当前背景音乐已自然结束，但资源引用仍然保留。</summary>
    Ended,
}
