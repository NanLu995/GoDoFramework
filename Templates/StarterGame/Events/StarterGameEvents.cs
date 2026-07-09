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

/// <summary>一局游戏已经结束。</summary>
public readonly struct StarterRunFinishedEvent : IStarterGameEvent
{
    public int Score { get; }

    public StarterRunFinishedEvent(int score)
    {
        Score = score;
    }
}
