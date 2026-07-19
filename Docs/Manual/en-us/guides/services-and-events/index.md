---
translation_of: Docs/Manual/zh-cn/guides/services-and-events/index.md
translation_source_hash: sha256:3aba4800f218ec4a31517fe802c48b09cdca6e956be7ef1e47505128d7208d5c
---

# Get Long-Lived Services and Publish Game Events

GoDo provides two collaboration mechanisms with different purposes. Services obtains a long-lived capability and calls it; EventChannel broadcasts that something has already happened. Whether the caller needs a result is usually enough to choose correctly.

```text
Needs a result, failure, or explicit ordering → Call a Service interface
Notifies one parent or child Node            → Prefer a Godot Signal
Broadcasts a fact to unrelated observers     → Use EventChannel
```

## When to use Services

Services fits framework capabilities such as Scene, Audio, Save, and UI that GoDoRuntime creates and keeps alive. Game code depends on interfaces without looking up Autoload node paths or retaining concrete implementations.

```csharp
ISceneService scenes = Services.Get<ISceneService>();
await scenes.ChangeAsync(GameScenes.Gameplay);
```

It is not a dependency-injection container that constructs objects, and it is not a global-variable store. Level Nodes, player data, temporary controllers, and one-shot requests do not belong in it.

## 1. Obtain a service at the right boundary

Prefer the context inside a Procedure:

```csharp
public override async ValueTask EnterAsync(ProcedureContext context)
{
    ISceneService scenes = context.GetService<ISceneService>();
    await scenes.ChangeAsync(GameScenes.MainMenu);
}
```

An ordinary Node can obtain and cache a long-lived service in `_Ready()`:

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

Do not query it every `_Process()` frame. Every Services API is restricted to Godot's main thread.

## 2. Distinguish required services from optional capabilities

Use `Get<T>()` for a service that must exist in a valid framework runtime. A missing registration throws `InvalidOperationException`, exposing setup or initialization errors early:

```csharp
IAudioService audio = Services.Get<IAudioService>();
```

Use `TryGet<T>()` only when the feature genuinely permits absence:

```csharp
if (Services.TryGet<ILocalizationService>(out ILocalizationService? localization))
    GD.Print(localization.CurrentLocale);
```

Do not use `TryGet` to silently hide a missing core service. GoDoRuntime registers and clears framework services; game code normally does not call `Register` or `Unregister`.

## When to use EventChannel

EventChannel fits facts that already happened, need no return value, and may have several observers. For example:

- The active player profile was loaded.
- A quest objective changed.
- The active locale or input device changed.
- A game session ended, and HUD, statistics, and audio react independently.

It does not fit a request such as “load this save and tell me whether it succeeded.” Call the relevant Service when a result or explicit failure contract is required.

## 3. Define game-owned events

Create a grouping interface in the game namespace:

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

Name an event as a fact that occurred, such as `PlayerDiedEvent`, rather than a command such as `KillPlayerEvent`. Keep the struct small; locate large data through a stable ID or long-lived reference instead of copying it into the event.

Gameplay events remain in the game namespace, not `GoDo.*`. `IEventMessage` is the shared low-level contract; it does not make every event part of the framework.

## 4. Use Bind for automatic Node lifetime management

A HUD listens for player death:

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

Publish the event:

```csharp
EventChannel.Emit(new PlayerDiedEvent
{
    PlayerId = playerId,
    Position = GlobalPosition,
});
```

`Bind` requires a Node that is already inside the scene tree and automatically unsubscribes when that Node exits. Do not manually `Off` the same binding, and use a named method instead of an anonymous lambda for maintained subscriptions.

Dispatch is synchronous: when `Emit` returns, this dispatch has completed. A listener should react quickly; expensive loading still belongs in a Service or in a controlled async flow started by the listener.

## 5. Use EventScope for plain C# objects

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

Retain the Scope and call `Dispose()` when its owner ends. Do not create a temporary Scope and lose its reference. A `Once` subscription also remains forever if its event never occurs, so it still needs a lifetime owner.

Direct `EventChannel.On` usage requires a retained named delegate and a matching `Off`. Nodes should normally use `Bind` instead.

## 6. Understand ordering, Once, and reentrancy

```csharp
EventChannel.Bind<PlayerDiedEvent>(analyticsNode, OnAnalytics, priority: -10);
EventChannel.Bind<PlayerDiedEvent>(hudNode, OnHud, priority: 0);
```

- A lower `priority` runs first; equal priorities preserve registration order.
- `Once` removes itself after one successful dispatch.
- The same delegate is not registered twice; do not depend on duplicate binding for repeated callbacks.
- Additions and removals during dispatch are committed after the outermost dispatch ends.
- The same event type can be emitted reentrantly, but complex reentrancy is hard to reason about and should be avoided.
- One listener throwing is reported to ErrorHub and does not block later listeners.

If correctness depends on listener A mutating state before listener B reads it, put that sequence in an explicit Service method or Procedure instead of stacking many priorities.

## 7. Choose Signal, EventChannel, or Service

| Need | Recommended |
|---|---|
| A Button tells its own panel that it was clicked | Godot Signal |
| HUD, statistics, and audio all react to session end | EventChannel |
| Load a scene and await completion or failure | `ISceneService` |
| Query the current locale or a save result | Relevant Service interface |
| Framework modules broadcast a stable state change | EventChannel |

Do not convert every method call into a global event merely to appear decoupled. More events make origins and execution order harder to trace.

## Common failures

- `Get<T>()` reports a missing service: GoDoRuntime is not enabled, the call ran before initialization, or the interface is not registered.
- A scene Node was registered globally: Services does not manage its lifetime, so a scene transition leaves an invalid reference.
- A Node still receives events after leaving: it used `On` without `Off`; switch to `Bind`.
- `Bind` did nothing: the Node was not inside the scene tree when called.
- A `Once` subscription remains forever: its event never occurred and no EventScope or `Off` cleaned it up.
- Event ordering becomes fragile: the flow depends on side effects across listeners and should use direct calls or Procedure coordination.
- A background task emits directly: EventChannel is main-thread only; return to the main-thread boundary first.

For exact members, see <xref:GoDo.Services>, <xref:GoDo.IEventMessage>, <xref:GoDo.EventChannel>, and <xref:GoDo.EventScope>.
