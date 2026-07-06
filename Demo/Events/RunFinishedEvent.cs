using GoDo;

namespace GoDoFramework.Demo;

/// <summary>点击挑战 Demo 的业务事件标记。</summary>
public interface IDemoEvent : IEventMessage { }

/// <summary>一局点击挑战已经结束。</summary>
public readonly struct RunFinishedEvent : IDemoEvent
{
    public int Score { get; }

    public RunFinishedEvent(int score)
    {
        Score = score;
    }
}
