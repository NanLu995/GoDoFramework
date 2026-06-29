// ==========================================
// GameEvents.cs —— 统一定义所有游戏事件
// 建议整个项目只有这一个事件定义文件，
// 一眼看清游戏里有哪些事件在流动。
// ==========================================
using Godot;

namespace GoDo.Events
{
    // ── 玩家相关 ──────────────────────────
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

    // ── 场景/游戏流程相关 ─────────────────
    public struct GameStartedEvent : IGameEvent { }
    public struct GamePausedEvent : IGameEvent { }
    public struct GameResumedEvent : IGameEvent { }
    public struct GameOverEvent : IGameEvent { public bool IsWin; }

    // ── 敌人相关 ──────────────────────────
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
}
