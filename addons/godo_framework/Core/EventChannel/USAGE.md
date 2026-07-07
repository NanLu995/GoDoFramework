# EventChannel 使用指南

## 定位与优势

EventChannel 是 Godot 主线程内的类型安全一对多通知机制，适合发送“某件事已经发生”的消息。事件使用 `struct + IEventMessage`，支持优先级、一次性监听、派发期间安全增删、同类型重入、监听者异常隔离，以及 Node/纯 C# 对象两种生命周期管理。

它不应替代所有方法调用：需要立即返回结果时直接调用接口；父子节点的局部通知优先考虑 Godot Signal；只有需要一对多广播或解耦观察者时使用 EventChannel。

## 快速上手

`IEventMessage` 只是 EventChannel 的公共底层契约。具体游戏可以在业务命名空间中定义自己的分组接口，再让玩法事件实现它：

```csharp
public interface IGameEvent : IEventMessage { }

public readonly struct PlayerDiedEvent : IGameEvent
{
    public int PlayerId { get; init; }
    public Vector2 Position { get; init; }
}
```

GoDo 框架内部事件实现 internal `IFrameworkEvent`。框架未来拆成独立程序集后，外部业务无法实现该接口；当前 GoDo 与业务脚本仍在同一程序集，因此这是一条可检查的架构约定，而不是安全边界。框架内部事件优先放在所属模块旁；只有少量跨 Core 的稳定事件才集中维护，不建立一个不断膨胀的全局事件文件。

Node 中优先使用 `Bind`，节点退出树时自动解绑：

```csharp
public partial class GameHud : Control
{
    public override void _Ready()
    {
        EventChannel.Bind<PlayerDiedEvent>(this, OnPlayerDied, priority: -10);
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        GD.Print($"Player {evt.PlayerId} died at {evt.Position}");
    }
}

EventChannel.Emit(new PlayerDiedEvent
{
    PlayerId = 1,
    Position = Vector2.Zero,
});
```

`Bind` 的 Node 必须已经进入场景树；未入树节点不会注册监听。

## 纯 C# 对象与临时监听

纯 C# 对象用 `EventScope` 批量管理，并在自身生命周期结束时 `Dispose`：

```csharp
public sealed class SessionObserver : IDisposable
{
    private readonly EventScope _events = new();

    public SessionObserver()
    {
        _events
            .On<PlayerDiedEvent>(OnPlayerDied)
            .Once<GameStartedEvent>(OnFirstGameStarted);
    }

    public void Dispose() => _events.Dispose();

    private void OnPlayerDied(PlayerDiedEvent evt) { }
    private void OnFirstGameStarted(GameStartedEvent evt) { }
}
```

直接使用 `On` 时必须保存具名方法并对称 `Off`；不要用无法解绑的匿名 lambda。
`Once` 只保证事件成功派发后移除，不会自动跟随任意 Node；事件可能永远不发生时，应交给 `EventScope` 管理，或在所有者退出时手动 `Off`。

## API 与派发语义

| API | 用途 |
|---|---|
| `On<T>(handler, priority)` | 持续监听，需手动解绑 |
| `Once<T>(handler)` | 成功派发一次后自动移除；未触发前仍需管理生命周期 |
| `Off<T>(handler)` | 移除指定监听 |
| `Bind<T>(node, handler, priority)` | 跟随 Node 退出树自动解绑 |
| `Emit<T>(evt)` | 同步派发事件 |
| `EventScope` | 管理纯 C# 对象的多项订阅 |

- `priority` 越小越先执行；相同优先级保持注册顺序。
- 派发是同步的；`Emit` 返回时本轮监听已经执行完成。
- 派发期间的注册和注销会延迟到最外层派发结束后提交。
- 单个监听者抛出异常会交给 ErrorHub，不阻断后续监听者。
- 相同委托不能重复注册；不要依赖重复注册制造多次回调。

## 线程与性能

- EventChannel 仅允许 Godot 主线程调用，不提供线程安全保证。
- `struct` 事件避免事件对象的堆分配，但监听委托和首次容器扩容仍可能分配。
- 不要用 EventChannel 传递超大结构体；必要时传递轻量标识或稳定引用。
- Debug 可用 `GetListenerCount<T>()` 和 `DumpRegistry()`；Release 不应依赖这些诊断结果。

## 常见误用

| 应该 | 避免 |
|---|---|
| Node 使用 `Bind` | Node 使用 `On` 后忘记 `Off` |
| 纯 C# 对象持有并释放 `EventScope` | 创建临时 Scope 后立即丢失引用 |
| 事件表达已经发生的事实 | 用事件隐藏必须返回结果的请求 |
| 使用具名回调 | 使用无法对称解绑的匿名 lambda |
| 业务层自定义 `IGameEvent : IEventMessage` | 把业务分组接口误当成框架公共契约 |
| 业务事件留在业务命名空间 | 把 Player、Enemy 等玩法概念放入 `GoDo.*` |
