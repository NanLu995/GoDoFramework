---
translation_of: Docs/Manual/zh-cn/guides/procedure-recovery/index.md
translation_source_hash: sha256:2909d098e443fed2b3fb6344e6dd7a317751e052f0db9b0a0219386a727cec86
---

# Organize Procedure Changes, Cleanup, and Failure Recovery

Procedure represents top-level phases such as boot, main menu, loading, gameplay, and results. It centralizes Scene, UI, Audio, Input, and Save ordering and gives every phase symmetrical entry and exit boundaries.

It is not a character, AI, skill, or UI-page state machine, and it provides no flow stack or automatic rollback.

## 1. Keep entry order recoverable

```csharp
public sealed class GameplayProcedure : IProcedure
{
    private IUiService? _ui;
    private Control? _hud;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        ISceneService scenes = context.GetService<ISceneService>();
        IAudioService audio = context.GetService<IAudioService>();
        IInputService input = context.GetService<IInputService>();
        _ui = context.GetService<IUiService>();

        await scenes.ChangeAsync(GameResources.GameplayScene);
        _hud = _ui.Open(GameResources.GameplayHud, UiLayer.Scene);
        input.SetBaseContext(GameInput.Gameplay);
        await audio.PlayBgmAsync(GameAudio.GameplayTheme);
    }
```

Complete likely-to-fail operations that do not corrupt existing state before committing later state. Scene change must precede Scene-layer UI, because scene commit clears that layer.

When a later step can fail after earlier work succeeds, register synchronous ownership with the Context immediately. Enter failure and normal exit clean these registrations in reverse order:

```csharp
UiScope<GameplayHud> hud = ui.OpenScoped<GameplayHud>(GameUi.GameplayHud);
context.RegisterCleanup(hud);
context.Events.On<PlayerQuitRequested>(OnPlayerQuitRequested);
```

Use `context.LifetimeToken` for long-running async work instead of maintaining a CancellationTokenSource in every Procedure. Still order non-reversible game operations as validate first, commit later.

## 2. Exit symmetrically

```csharp
    public Task ExitAsync(ProcedureContext context)
    {
        if (_ui != null && _hud != null && GodotObject.IsInstanceValid(_hud))
            _ui.Close(_hud);

        _hud = null;
        _ui = null;
        return Task.CompletedTask;
    }
}
```

Exit cleans only UI, subscriptions, Scheduler Handles, CancellationTokens, and temporary game objects owned by this flow. Never globally clear another system's Views, Modals, audio, or event listeners.

GoDoRuntime shutdown does not invoke the current game Procedure's `ExitAsync`. The game must actively save critical data before its own exit boundary.

## 3. Understand the two failure states

Change exits the old flow, clears `Current`, then enters the new one:

- Old Exit fails: the new flow is not entered and `Current` remains the old flow.
- New Enter fails: the old flow already exited and `Current` is null.

An unprocessed `RequestChange` created inside a failing boundary is discarded. It cannot run unexpectedly after a later recovery succeeds.

The framework therefore cannot automatically return to the old flow after entry failure. Choose an explicit recovery target:

```csharp
try
{
    await procedures.ChangeAsync<GameplayProcedure>();
}
catch (ProcedureChangeException exception)
{
    ErrorHub.Report(exception, "Game.Flow", context: "MainMenu -> Gameplay");

    if (procedures.Current == null)
        await procedures.ChangeAsync(new RecoveryProcedure(exception));
}
```

RecoveryProcedure should depend only on minimal reliable content, such as a built-in error page or title scene. Do not immediately repeat the failing resource chain.

## 4. Do not recursively ChangeAsync inside Enter/Exit

Direct recursive change is rejected. Request the next flow through Context:

```csharp
public Task EnterAsync(ProcedureContext context)
{
    if (!HasValidProfile())
        context.RequestChange<ProfileSelectionProcedure>();

    return Task.CompletedTask;
}
```

UI and scene scripts publish player intent; the current Procedure coordinator calls `RequestChange`. The request runs serially after the current boundary ends.

Each Procedure activation accepts only its first request, so this is not a command queue. Event callbacks that need the arbitration result use `TryRequestChange`; later requests return `false` and never replace the accepted target. Pass game data explicitly with `TryRequestChange(new ResultProcedure(data))`.

## 5. Prevent repeated clicks and concurrent changes

```csharp
private async void OnStartPressed()
{
    if (_procedures.IsChanging)
        return;

    _startButton.Disabled = true;
    try
    {
        await _procedures.ChangeAsync<GameplayProcedure>();
    }
    catch (ProcedureChangeException exception)
    {
        ErrorHub.Report(exception, "Game.Flow", "Start gameplay");
        _startButton.Disabled = false;
    }
}
```

A second simultaneous ChangeAsync throws `ProcedureChangeException`. Disabling UI improves experience; ProcedureService rejection remains the correctness boundary.

Never trigger changes from `_Process()` or use fire-and-forget that loses exceptions.

## 6. Own cancellation and long async work

Use the activation lifetime token supplied by the Context:

```csharp
public async Task EnterAsync(ProcedureContext context)
{
    ISchedulerService scheduler = context.GetService<ISchedulerService>();
    await scheduler.DelayAsync(1.0, ScheduleOptions.RealTime, context.LifetimeToken);
}
```

Handle expected `OperationCanceledException` separately instead of reporting content corruption. Exit is not called on this instance while Enter is still awaiting, so any background-style game operation started during Enter still needs an explicit owner and observed exceptions.

If GoDoRuntime shuts down while a flow change is awaiting, the change ends with `ProcedureChangeException` whose `InnerException` is `OperationCanceledException`. The old asynchronous operation cannot write `Current` again after shutdown.

A long-lived game coordinator may subscribe to `IProcedureService.RequestedChangeFailed` for deferred request failures. Notification runs after `IsChanging` resets. Direct `ChangeAsync` failures and shutdown cancellation do not raise the event, so recovery can explicitly try one safe Procedure without creating a notification loop. Unsubscribe with the coordinator's lifetime.

`ProcedureChangeException.Phase` distinguishes Requesting, Exiting, Cleanup, and Entering. Base recovery decisions on the phase and `Current`, never on parsed message text.

## 7. Keep flows small and diagnosable

- Give `Name` a stable readable value for exceptions and logs.
- Coordinate modules; do not embed movement, combat rules, or complex UI logic.
- Delegate concrete work to game services or scene Controllers.
- Log source, target, and useful context without player-sensitive data.
- `Current` represents only the successfully entered flow, not history.

## Common failures

- Old flow is assumed valid after Enter fails: `Current` is usually null; enter explicit recovery.
- New flow is forced after Exit fails: the old flow remains Current and must be handled first.
- Enter calls ChangeAsync directly: this is reentrancy; use RequestChange.
- Several buttons change flows: centralize intent and observe `IsChanging`.
- Exit clears every UI page: ownership is broken; close only instances created by this flow.
- ExitAsync is expected to save on application shutdown: Runtime shutdown does not invoke it; save earlier.
- Procedure becomes a giant controller: move concrete gameplay into services and scene Nodes.

For exact members, see <xref:GoDo.IProcedure>, <xref:GoDo.IProcedureService>, <xref:GoDo.ProcedureContext>, <xref:GoDo.ProcedureChangePhase>, and <xref:GoDo.ProcedureChangeException>.
