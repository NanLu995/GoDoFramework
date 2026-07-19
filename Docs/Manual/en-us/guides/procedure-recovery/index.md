---
translation_of: Docs/Manual/zh-cn/guides/procedure-recovery/index.md
translation_source_hash: sha256:43226b922b6284570e8132d562c550d7a1180caf04ea3ab90bc359cc59281ab1
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

If a later step can fail after earlier work succeeds, catch inside the Procedure, clean up what this entry created, and rethrow. ProcedureService wraps it in `ProcedureChangeException`.

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

Only the latest request is retained, so this is not a command queue. A failed requested change is reported through ErrorHub. Pass game data explicitly with `RequestChange(new ResultProcedure(data))`.

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

The Procedure creates a CancellationTokenSource and cancels it on Exit:

```csharp
private CancellationTokenSource? _lifetime;

public async Task EnterAsync(ProcedureContext context)
{
    _lifetime = new CancellationTokenSource();
    ISchedulerService scheduler = context.GetService<ISchedulerService>();
    await scheduler.DelayAsync(1.0, ScheduleOptions.RealTime, _lifetime.Token);
}

public Task ExitAsync(ProcedureContext context)
{
    _lifetime?.Cancel();
    _lifetime?.Dispose();
    _lifetime = null;
    return Task.CompletedTask;
}
```

Handle expected `OperationCanceledException` separately instead of reporting content corruption. Exit is not called on this instance while Enter is still awaiting, so any background-style game operation started during Enter still needs an explicit owner and observed exceptions.

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

For exact members, see <xref:GoDo.IProcedure>, <xref:GoDo.IProcedureService>, <xref:GoDo.ProcedureContext>, and <xref:GoDo.ProcedureChangeException>.
