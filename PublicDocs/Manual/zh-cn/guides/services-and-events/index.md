# 获取长期服务与发送业务事件

GoDo 提供两种用途不同的协作方式：Services 用于取得一个长期能力并调用它；EventChannel 用于广播“某件事已经发生”。先判断调用方是否需要返回值，通常就能选对工具。

```text
需要结果、失败或明确操作顺序 → 调用 Service 接口
只通知一个父节点或子节点     → 优先使用 Godot Signal
向多个互不持有的对象广播事实 → 使用 EventChannel
```

## 什么时候使用 Services

Services 适合 Scene、Audio、Save、UI 等由 GoDoRuntime 创建并长期存在的框架能力。业务代码依赖接口，不需要查找 Autoload 节点路径，也不持有具体实现。

```csharp
ISceneService scenes = Services.Get<ISceneService>();
await scenes.ChangeAsync(GameScenes.Gameplay);
```

它不是自动构造对象的依赖注入容器，也不是全局变量仓库。关卡节点、玩家数据、临时 Controller 和一次性请求不应注册进去。

## 1. 在合适的位置获取服务

Procedure 中优先从上下文获取：

```csharp
public override async ValueTask EnterAsync(ProcedureContext context)
{
    ISceneService scenes = context.GetService<ISceneService>();
    await scenes.ChangeAsync(GameScenes.MainMenu);
}
```

普通 Node 可以在 `_Ready()` 获取并缓存长期服务：

```csharp
public partial class PauseButton : Button
{
    private IUiService? _ui;

    public override void _Ready()
    {
        _ui = Services.Get<IUiService>();
    }
}
```

不要在 `_Process()` 中每帧查询。所有 Services API 都只能从 Godot 主线程调用。

## 2. 区分必需服务和可选能力

框架正常运行必须存在的服务使用 `Get<T>()`。缺失时抛出 `InvalidOperationException`，可以尽早暴露安装或初始化错误：

```csharp
IAudioService audio = Services.Get<IAudioService>();
```

只有功能确实允许缺失时才使用 `TryGet<T>()`：

```csharp
if (Services.TryGet<ILocalizationService>(out ILocalizationService? localization))
    GD.Print(localization.CurrentLocale);
```

不要用 `TryGet` 静默隐藏本应存在的核心服务。框架服务由 GoDoRuntime 注册和清理，业务代码通常不调用 `Register` 或 `Unregister`。

## 扩展：注册项目自己的长期服务

只有框架启动层或测试装配代码需要直接注册服务。服务必须按接口注册，而且注册者必须拥有比所有调用者更长的生命周期：

```csharp
IGameSessionService session = new GameSessionService();
Services.Register<IGameSessionService>(session);

// 后续查询会得到同一个实例。
IGameSessionService current = Services.Get<IGameSessionService>();
GD.Print(ReferenceEquals(session, current)); // True
```

同一接口重复注册会抛出 `InvalidOperationException`，按具体类型注册会抛出 `ArgumentException`；注册表不会隐式替换已有实例。测试需要临时替换时，应先用原实例注销，结束后恢复原注册，避免污染后续用例。

注销必须传入注册时的同一个实例：

```csharp
bool removed = Services.Unregister<IGameSessionService>(session);
GD.Print(removed); // True
```

接口未注册或实例不匹配时，`Unregister` 返回 `false`，不会移除现有服务。Services 只保存引用，不会调用 `Dispose()` 或释放 Godot 对象；创建服务的启动层仍负责停止任务、断开订阅和释放资源。短生命周期场景不得承担这种注册职责。

## 什么时候使用 EventChannel

EventChannel 适合已经发生、无需返回值、可能有多个观察者的事实，例如：

- 当前玩家档案已经加载。
- 任务目标已经更新。
- 活动语言或输入设备已经改变。
- 一局游戏已经结束，HUD、统计和音频需要分别响应。

它不适合“请加载存档并告诉我是否成功”这类请求。需要结果或明确错误语义时，直接调用对应 Service。

## 3. 定义游戏自己的事件

在业务命名空间中建立分组接口：

```csharp
using Godot;
using GoDo;

namespace MyGame;

public interface IGameEvent : IEventMessage { }

public readonly struct PlayerDiedEvent : IGameEvent
{
    public int PlayerId { get; init; }
    public Vector2 Position { get; init; }
}
```

事件名使用已经发生的事实，例如 `PlayerDiedEvent`，而不是命令式的 `KillPlayerEvent`。保持结构体轻量；大型数据通过稳定 ID 或长期引用定位，不要复制进事件。

玩法事件留在游戏命名空间，不放进 `GoDo.*`。`IEventMessage` 是公共底层契约，不代表所有事件都属于框架。

## 4. Node 使用 Bind 自动管理生命周期

HUD 监听玩家死亡：

```csharp
public partial class GameHud : Control
{
    public override void _Ready()
    {
        EventChannel.Bind<PlayerDiedEvent>(this, OnPlayerDied);
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        ShowDeathMessage(evt.PlayerId);
    }
}
```

发布事件：

```csharp
EventChannel.Emit(new PlayerDiedEvent
{
    PlayerId = playerId,
    Position = GlobalPosition,
});
```

`Bind` 要求 Node 已经进入场景树，并在 Node 退出树时自动解绑。不要再手工 `Off` 同一项绑定，也不要用匿名 lambda 代替需要长期维护的具名方法。

派发是同步的：`Emit` 返回时，本轮监听者已经执行完毕。监听者应快速响应；耗时加载仍应交给 Service 或由监听者启动受控的异步流程。

## 5. 纯 C# 对象使用 EventScope

```csharp
public sealed class SessionObserver : IDisposable
{
    private readonly EventScope _events = new();

    public SessionObserver()
    {
        _events
            .On<PlayerDiedEvent>(OnPlayerDied)
            .Once<SessionStartedEvent>(OnFirstSessionStarted);
    }

    public void Dispose() => _events.Dispose();

    private void OnPlayerDied(PlayerDiedEvent evt) { }
    private void OnFirstSessionStarted(SessionStartedEvent evt) { }
}
```

保存 Scope，并在所有者生命周期结束时 `Dispose()`。不要创建临时 Scope 后丢失引用；`Once` 会在调用回调前标记移除，即使回调抛错也不会再次执行，但事件永远不发生时仍需要生命周期所有者负责清理。

直接使用 `EventChannel.On` 时，必须保存具名委托并通过 `Off` 对称移除。Node 通常应优先使用 `Bind`。

## 6. 理解顺序、Once 和重入

```csharp
EventChannel.Bind<PlayerDiedEvent>(analyticsNode, OnAnalytics, priority: -10);
EventChannel.Bind<PlayerDiedEvent>(hudNode, OnHud, priority: 0);
```

- `priority` 越小越先执行；相同优先级保持注册顺序。
- `Once` 在调用回调前标记移除，同类型重入不会再次执行。
- 同一个委托不会重复注册，不要依赖重复绑定获得多次回调。
- 派发期间新增监听会延迟到最外层派发结束后生效；`Off` 会立即跳过本轮尚未执行的目标回调，底层列表变更仍延迟提交。
- 同类型事件可以重入派发，但复杂重入很难推理，应尽量保持事件处理简单。
- 单个监听者抛出异常会交给 ErrorHub，不阻断后续监听者。

如果正确性依赖“监听者 A 必须先修改状态，监听者 B 再读取”，优先把这段顺序放进一个明确的 Service 方法或 Procedure，而不是堆叠大量优先级。

## 7. 决定 Signal、EventChannel 还是 Service

| 需求 | 推荐 |
|---|---|
| Button 通知自己的面板被点击 | Godot Signal |
| HUD、统计、音频同时响应一局结束 | EventChannel |
| 加载场景并等待完成或失败 | `ISceneService` |
| 查询当前语言或保存结果 | 对应 Service 接口 |
| 框架模块之间广播稳定状态变化 | EventChannel |

不要为了“解耦”把每个方法调用都变成全局事件。事件越多，调用来源和执行顺序越难追踪。

## 常见错误

- `Get<T>()` 报服务缺失：GoDoRuntime 未正确启用、调用早于初始化，或查询了没有注册的接口。
- 把场景 Node 注册为全局服务：Services 不管理其释放，场景切换后会留下失效引用。
- Node 离开场景后仍收到事件：使用了 `On` 却忘记 `Off`；改用 `Bind`。
- `Bind` 没有生效：调用时 Node 尚未进入场景树。
- `Once` 永久占用：事件未发生，且所有者没有用 EventScope 或 `Off` 清理。
- 事件处理顺序变得脆弱：业务流程依赖多个监听者副作用，应改为直接调用或由 Procedure 协调。
- 后台任务直接 Emit：EventChannel 仅允许 Godot 主线程，先切回主线程边界。

精确接口可查询 <xref:GoDo.Services>、<xref:GoDo.IEventMessage>、<xref:GoDo.EventChannel> 和 <xref:GoDo.EventScope>。

## 能力全景图

<div class="godo-capability-list">
<section><h4>注册、获取与注销长期服务</h4><p>Services 不拥有实例生命周期；注销必须使用同一实例。</p><pre class="godo-capability-call"><code>Services.Register&lt;IMyService&gt;(service);
IMyService required = Services.Get&lt;IMyService&gt;();
bool found = Services.TryGet&lt;IMyService&gt;(out IMyService optional);
Services.Unregister&lt;IMyService&gt;(service);</code></pre></section>
<section><h4>发送同步业务事件</h4><p>无载荷或强类型消息都在当前主线程调用栈派发。</p><pre class="godo-capability-call"><code>EventChannel.Emit&lt;GamePausedEvent&gt;();
EventChannel.Emit(new ScoreChangedEvent(score));</code></pre></section>
<section><h4>订阅、单次订阅与取消</h4><p>长期对象显式 On/Off；一次性等待使用 Once。</p><pre class="godo-capability-call"><code>EventChannel.On&lt;ScoreChangedEvent&gt;(OnScoreChanged, priority: 10);
EventChannel.Once&lt;GameReadyEvent&gt;(OnReady);
EventChannel.Off&lt;ScoreChangedEvent&gt;(OnScoreChanged);</code></pre></section>
<section><h4>绑定 Node 生命周期</h4><p>Node 离树时自动解绑，适合场景对象。</p><pre class="godo-capability-call"><code>EventChannel.Bind&lt;ScoreChangedEvent&gt;(this, OnScoreChanged);</code></pre></section>
</div>
