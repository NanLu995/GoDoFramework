// 示例项目的业务事件，不属于 GoDo 框架 API。
using Godot;
using GoDo;

namespace GoDoFramework.Example.Events;

/// <summary>示例业务事件的统一标记，不属于 GoDo 框架 API。</summary>
public interface IGameEvent : IEventMessage { }

public struct PlayerDiedEvent : IGameEvent
{
    public int PlayerId;
    public Vector2 Position;
}

public struct PlayerHurtEvent : IGameEvent
{
    public int PlayerId;
    public int Damage;
    public int HpRemaining;
}

public struct PlayerLevelUpEvent : IGameEvent
{
    public int PlayerId;
    public int NewLevel;
}

public struct GameStartedEvent : IGameEvent { }
public struct GamePausedEvent : IGameEvent { }
public struct GameResumedEvent : IGameEvent { }

public struct GameOverEvent : IGameEvent
{
    public bool IsWin;
}

public struct EnemySpawnedEvent : IGameEvent
{
    public string EnemyType;
    public Vector2 SpawnPosition;
}

public struct EnemyDiedEvent : IGameEvent
{
    public string EnemyType;
    public Vector2 Position;
    public int ScoreValue;
}
