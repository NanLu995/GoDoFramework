using GoDo;

namespace Demo3D;

/// <summary>Demo3D 的业务事件标记。</summary>
public interface IDemo3DEvent : IEventMessage { }

/// <summary>玩家收集到一个能量核心。</summary>
public readonly struct CollectibleCollectedEvent : IDemo3DEvent { }

/// <summary>收集进度已经更新。</summary>
public readonly struct CollectionProgressChangedEvent : IDemo3DEvent
{
    public int Current { get; }
    public int Total { get; }

    public CollectionProgressChangedEvent(int current, int total)
    {
        Current = current;
        Total = total;
    }
}

/// <summary>玩家选择重新开始。</summary>
public readonly struct RetrySelectedEvent : IDemo3DEvent { }
