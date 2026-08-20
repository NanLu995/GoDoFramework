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

/// <summary>玩家选择从主菜单开始游戏。</summary>
public readonly struct StartGameSelectedEvent : IDemo3DEvent { }

/// <summary>玩家请求暂停当前游戏。</summary>
public readonly struct PauseRequestedEvent : IDemo3DEvent { }

/// <summary>玩家选择恢复当前游戏。</summary>
public readonly struct ResumeSelectedEvent : IDemo3DEvent { }

/// <summary>玩家选择重新开始。</summary>
public readonly struct RetrySelectedEvent : IDemo3DEvent { }

/// <summary>玩家选择返回主菜单。</summary>
public readonly struct ReturnToMenuSelectedEvent : IDemo3DEvent { }
