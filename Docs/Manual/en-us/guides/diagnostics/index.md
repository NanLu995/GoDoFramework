---
translation_of: Docs/Manual/zh-cn/guides/diagnostics/index.md
translation_source_hash: sha256:33c902d773dd15b107a0814970b3fb25c5d6ec862ea828fa8cb50e46adc97085
---

# Log Activity, Report Errors, and Inspect Runtime State

GoDo separates runtime information into two channels. LogHub records normal-flow development diagnostics, while ErrorHub records degradation, failures, and fatal conditions that require attention. Debug builds also display a read-only Debugger for inspecting framework state while the game runs.

This separation is not merely about printing more text. Development logs disappear from Release builds, while real errors remain visible in shipped builds.

## Choose the right channel first

| Situation | Use |
|---|---|
| Normal flow entry, cache hit, or development-only state change | `LogHub.Debug` / `LogHub.Info` |
| The operation recovered with degraded behavior | `ErrorHub.Warn` |
| The current operation failed and produced an exception | `ErrorHub.Report` |
| The game cannot continue safely | `ErrorHub.Fatal`, followed by a decision at the game boundary |
| A message for the player | Game UI; do not expose console text directly |

`Fatal` is only the highest error level; it does not exit the game. The caller that understands the game context decides whether to retry, fall back, return to the title screen, or terminate the process.

## 1. Add development logs for normal flow

```csharp
LogHub.Info("Entered the main-menu flow.", "Game.Procedure");
LogHub.Debug("Resource cache hit.", "Game.Inventory", context: "item=sword");
```

The console format is:

```text
[module] [level] (optional context) message
```

Use stable module names such as `Game.Boot`, `Game.Save`, and `Game.Inventory`. The message says what happened; `context` carries a slot, resource ID, flow name, or similar locator. Do not repeat the level and module inside the message.

LogHub can only be called from Godot's main thread. Its calls use `Conditional("DEBUG")`: Release removes each call site and does not evaluate argument expressions. Never rely on a logging argument to perform a side effect.

## 2. Report a recoverable problem

When an operation can continue by using a fallback:

```csharp
ErrorHub.Warn(
    "Volume setting was missing; the default was applied.",
    "Game.Settings",
    context: "key=audio.master");
```

A Warning should explain where behavior degraded and what result was used. Do not report frequent normal states as warnings; an error storm hides the issue that actually matters.

## 3. Handle exceptions at a feature boundary

Catch an exception only where code can choose a recovery policy:

```csharp
try
{
    SaveLoadResult<PlayerSave> result = saves.Load<PlayerSave>(
        SaveSlot.Create("slot-1"),
        PlayerSaveCodec.Instance);

    ApplySave(result.Value);
}
catch (SaveException exception)
{
    ErrorHub.Report(exception, "Game.Save", context: "slot=slot-1");
    ShowLoadFailedDialog();
}
```

Report one failure once. If a lower layer throws and an upper layer owns handling, the lower layer should not report first and rethrow. Doing both duplicates console entries, Reporter payloads, and player telemetry.

For a startup failure that cannot continue:

```csharp
catch (Exception exception)
{
    ErrorHub.Fatal(exception, "Game.Boot", context: "phase=initialization");
    ShowFatalStartupScreen();
}
```

The startup boundary still chooses whether to show a safe screen, return to the title, or exit.

## 4. Listen temporarily and update game UI

`OnError` is a raw C# event. A Node with a shorter lifetime than GoDoRuntime must unsubscribe symmetrically:

```csharp
public override void _EnterTree()
{
    ErrorHub.OnError += OnError;
}

public override void _ExitTree()
{
    ErrorHub.OnError -= OnError;
}

private void OnError(ErrorReport report)
{
    if (report.Level >= ErrorLevel.Error)
        ShowErrorToast(report.Message);
}
```

Listeners should return quickly, avoid mutating error-system state, and never call ErrorHub recursively. If one listener throws, ErrorHub isolates it and continues notifying the remaining listeners.

Player-facing copy usually needs localization and privacy filtering. `ErrorReport.Message` is intended for development diagnosis and should not be shown to players by default.

## 5. Add a custom Reporter

To write a file or integrate an error platform, implement `IErrorReporter`:

```csharp
public sealed class GameErrorReporter : IErrorReporter, IDisposable
{
    public void Report(in ErrorReport report)
    {
        // Enqueue quickly; do not synchronously write or wait for a network call.
    }

    public void Dispose()
    {
        // Flush the reporter's bounded queue and release resources.
    }
}
```

Create and retain one instance in the one-time Boot scene:

```csharp
_reporter = new GameErrorReporter();
ErrorHub.AddReporter(_reporter);
```

To remove it early:

```csharp
ErrorHub.RemoveReporter(_reporter);
_reporter.Dispose();
```

Reporters run synchronously on the error-dispatch call stack. Do not use `.Wait()`, `.Result`, or synchronous network requests. On shutdown, GoDoRuntime clears registered Reporters and calls `Dispose()` on those that implement `IDisposable`.

Before connecting a remote platform, the game project must define user consent, private-field filtering, offline buffering, retry limits, and platform compliance. The framework does not upload data for you.

## 6. Use the runtime Debugger

After enabling the `GoDoRuntime.tscn` Autoload, Debug builds automatically show a compact health button with no shortcut configuration.

- Collapsed mode shows FPS and recent Warning/Error counts.
- Click it to open a card-based runtime overview, then use the navigation tree to inspect Services, Events, Input, the structured Scheduler and Audio dashboards, and Console pages.
- Drag the title bar to move the window, drag the lower-right Resize Debugger handle to resize the entire panel, or click Reset to restore the default layout.
- The current page refreshes every 0.25 seconds while expanded; collapsed mode creates no module snapshots.
- The panel is read-only and cannot modify services or game data.
- Release builds do not create it, so game logic must never depend on it.

The Services page maps each registered service contract to its current implementation type. Search matches short and fully qualified names for both sides, and selecting a row displays the complete contract-to-implementation relationship below the list. The page is read-only: it neither returns service instances nor replaces registrations.

The Events page summarizes event types and listener counts. Its search field matches both short and fully qualified type names, and selecting a row displays the full type name below the list so same-named events from different namespaces remain distinguishable. This page shows events that currently have listeners; it does not retain an event-emission history.

The Input page uses status cards for the current backend, active device, sample sequence, and Action count, followed by separate Context-stack and Action-state tables. The sequence normally increases after each successful per-frame sample, remains unchanged after a failed sample, and resets when the backend is reinstalled or the service shuts down. Action rows include value type, current value, and just-pressed/just-released edges. Search filters the complete snapshot by Action name or value type and renders at most the first 32 matches.

The Audio page reports whether BGM is loading, playing, loaded but not playing, or stopped, together with the current resource key. Its SFX card shows active voices, capacity, and utilization, while the lower cards show linear Master, BGM, and SFX volume. Because the current audio interface cannot distinguish a paused stream from one that ended naturally, either case is conservatively reported as loaded but not playing.

The Console toolbar provides counted All, Debug, Info, Warning, and Error chips. All is selected by default. Click a level to show only that level, click additional levels to combine them, or click All to reset. Search scans the complete in-memory history; when results span multiple pages, use Previous and Next, or click the separate Latest Logs button on the right to return directly to the final page and scroll to the bottom. While you remain on the latest page and refresh is not paused, new logs automatically follow the bottom. Scrolling upward stops following and enables Latest Logs; scrolling back to the bottom or clicking that button resumes following. Pause stops automatic refresh and scrolling. Copy copies only the text currently displayed by the active filters, search, and page. The search field captures input only after a click and releases focus when you submit the search or leave the Console.

Console pages retain only limited recent data. LogHub uses a 1,000-entry ring; filtering and search scan the entire history, while each page renders at most 100 normal logs. Consecutive identical logs are aggregated into a `×count` entry with first and last timestamps. The Godot output console receives at most 100 LogHub lines per second, while suppressed lines remain available in Debugger history. ErrorHub summary capacity is 16, and each Warning/Error filter displays at most 12 matching entries. This is a quick inspection tool, not a persistent log archive or profiler. Use a future rolling file-log facility when diagnostics must survive a session or cover the complete runtime.

## Background threads and error storms

LogHub is main-thread only. ErrorHub may be called from a background thread, but it places reports in a bounded queue of at most 1,024 entries. GoDoRuntime dispatches at most 256 per frame, and listeners and Reporters still run on the main thread.

When the queue fills, reports are dropped and summarized as a Warning on the main thread. A background Fatal also writes synchronously to the fallback console. Fix or rate-limit a repeated source instead of treating ErrorHub as an unbounded queue.

## Common failures

- Info is missing in Release: expected behavior; use ErrorHub for shipped failures.
- A player message exposes technical details: replace the raw exception with localized game copy.
- The same exception appears repeatedly: check for report-and-rethrow at several call layers.
- Callbacks continue after a scene switch: a short-lived object forgot to unsubscribe from `OnError`.
- Reporting freezes the game: a Reporter is writing synchronously, waiting on a lock, or calling the network.
- The game continues after Fatal: Fatal does not terminate; the game boundary must act explicitly.
- The Debugger disappears from an exported build: Release does not create it by design.

For exact members, see <xref:GoDo.LogHub>, <xref:GoDo.ErrorHub>, <xref:GoDo.ErrorReport>, <xref:GoDo.ErrorLevel>, and <xref:GoDo.IErrorReporter>.
