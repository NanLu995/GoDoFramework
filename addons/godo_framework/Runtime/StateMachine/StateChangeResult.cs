#nullable enable

namespace GoDo;

/// <summary>表示同步状态切换请求的处理结果。</summary>
public enum StateChangeResult
{
    /// <summary>请求已同步完成；请求期间产生的 FIFO 后续切换也已处理完成。</summary>
    Changed,

    /// <summary>请求发生在生命周期回调中，已加入 FIFO 队列等待当前切换完成。</summary>
    Queued,

    /// <summary>目标与当前状态是同一实例，因此没有调用任何生命周期回调。</summary>
    IgnoredSameState,
}
