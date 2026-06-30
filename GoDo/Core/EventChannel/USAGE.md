# GoDo.EventChannel 使用指南

## 快速上手

### 1. 定义事件（在 GameEvents.cs 里统一添加）
```csharp
// 事件必须是 struct + IGameEvent
public struct PlayerDiedEvent : IGameEvent
{
    public int     PlayerId;
    public Vector2 Position;
}
```

### 2. 监听事件

```csharp
public partial class GameUI : Control
{
    public override void _Ready()
    {
        // ✅ 推荐：Bind 自动跟随节点生命周期，无需手动 Off
        GoDo.EventChannel.Bind<PlayerDiedEvent>(this, OnPlayerDied);

        // 只触发一次（如教程引导）
        GoDo.EventChannel.Once<EnemySpawnedEvent>(OnFirstEnemy);

        // 带优先级（数字越小越先执行）
        GoDo.EventChannel.Bind<GameOverEvent>(this, OnGameOver, priority: -10);
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        GD.Print($"Player {evt.PlayerId} died at {evt.Position}");
    }
}
```

### 3. 发送事件

```csharp
public partial class Player : CharacterBody2D
{
    private void Die()
    {
        GoDo.EventChannel.Emit(new PlayerDiedEvent
        {
            PlayerId = Id,
            Position = GlobalPosition
        });
    }
}
```

## 调试（仅 Debug 模式可用）

```csharp
// 查看某事件有多少监听者
int count = GoDo.EventChannel.GetListenerCount<PlayerDiedEvent>();

// 打印所有事件注册情况
GoDo.EventChannel.DumpRegistry();
// 输出:
// ── EventChannel Registry ──
//   PlayerDiedEvent:  3 listener(s)
//   GameOverEvent:    1 listener(s)
// ───────────────────────────
```

## 注意事项

| ✅ 应该 | ❌ 避免 |
|---|---|
| 用 `Bind` 代替 `On`（节点里） | 在节点里用 `On` 却忘记 `Off` |
| 事件定义集中在 GameEvents.cs | 事件定义散落在各个文件 |
| 事件用 `struct` | 事件用 `class`（产生 GC） |
| 纯 C# 类里用 `On` + 手动 `Off` | 在纯 C# 类里用 `Bind` |

---

## EventScope —— 纯 C# 类的批量管理

Node 类用 `Bind`，纯 C# 类用 `EventScope`：

```csharp
public class NetworkManager
{
    // 持有一个 Scope，统一管理所有监听
    private readonly EventScope _scope = new();

    public void Initialize()
    {
        // 链式注册，整洁
        _scope
            .On<PlayerDiedEvent>(OnPlayerDied)
            .On<GameOverEvent>(OnGameOver)
            .Once<GameStartedEvent>(OnFirstStart);
    }

    // 销毁时一行清完，不用一个个 Off
    public void Dispose() => _scope.Dispose();

    private void OnPlayerDied(PlayerDiedEvent evt) { }
    private void OnGameOver(GameOverEvent evt) { }
    private void OnFirstStart(GameStartedEvent evt) { }
}

// 也可以配合 using 语句，离开作用域自动清除
using var scope = new EventScope();
scope.On<PlayerDiedEvent>(handler);
// ... scope 离开 using 块时自动 Dispose
```

## 选哪个？

| 场景 | 推荐方式 |
|---|---|
| 继承 Node 的脚本 | `EventChannel.Bind(this, handler)` |
| 纯 C# 类（服务、管理器） | `EventScope` |
| 只触发一次 | `EventChannel.Once` 或 `scope.Once` |
| 临时监听一段逻辑 | `using var scope = new EventScope()` |
