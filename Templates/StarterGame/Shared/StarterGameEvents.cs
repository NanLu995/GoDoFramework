using GoDo;

namespace StarterGame;

/// <summary>StarterGame 模板的业务事件标记。</summary>
public interface IStarterGameEvent : IEventMessage { }

/// <summary>分数发生变化。</summary>
public readonly struct StarterScoreChangedEvent : IStarterGameEvent
{
    public int Score { get; }

    public StarterScoreChangedEvent(int score)
    {
        Score = score;
    }
}

/// <summary>玩家选择开始游戏。</summary>
public readonly struct StarterStartGameSelectedEvent : IStarterGameEvent { }

/// <summary>一局游戏已经结束。</summary>
public readonly struct StarterRunFinishedEvent : IStarterGameEvent
{
    public int Score { get; }

    public StarterRunFinishedEvent(int score)
    {
        Score = score;
    }
}

/// <summary>玩家在结算界面选择再来一局。</summary>
public readonly struct StarterRetrySelectedEvent : IStarterGameEvent { }

/// <summary>玩家在结算界面选择返回主菜单。</summary>
public readonly struct StarterReturnToMenuSelectedEvent : IStarterGameEvent { }
