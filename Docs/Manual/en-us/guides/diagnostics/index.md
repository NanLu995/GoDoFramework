---
translation_of: Docs/Manual/zh-cn/guides/diagnostics/index.md
translation_source_hash: sha256:1da31847b656f4f054cefd8038511ec6735066eabca5565d35ccc0b1cb3c2533
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

- Collapsed mode shows only FPS. Warning or Error activity changes the text color according to the highest severity; inspect the Overview for exact counts.
- Click it to open a card-based runtime overview, then use the navigation tree to inspect the structured System, Performance, Services, Events, Input, Scheduler, Audio, Scene, Resources, DataTable, and UI dashboards, plus the Console page.
- Drag the title bar to move the window, drag the lower-right Resize Debugger handle to resize the entire panel, or click Reset to restore the default layout.
- The current page refreshes every 0.25 seconds while expanded; collapsed mode creates no module snapshots.
- The panel is read-only and cannot modify services or game data.
- Release builds do not create it, so game logic must never depend on it.

The Services page maps each registered service contract to its current implementation type. Search matches short and fully qualified names for both sides, and selecting a row displays the complete contract-to-implementation relationship below the list. The page is read-only: it neither returns service instances nor replaces registrations.

The System page reports the current platform, Debug build, rendering method, and engine uptime, then groups Godot/.NET versions, process architecture, locale, window mode and size, VSync, rendering driver, and video-adapter details. Static environment values are read once during panel initialization, while dynamic window state refreshes only when this page is selected. Unsupported values are displayed as unavailable.

The Performance page shows FPS, Process/Physics time, and 30-second trends for Godot engine memory and the .NET managed heap. The left side of each graph shows reference ticks that follow the recent sample range, while the colored values on the right show the latest value for each line. Details are grouped into memory, objects, rendering, 2D/3D physics, and Pipeline metrics. Sampling runs every 0.25 seconds only while this page is selected and stops when you leave it. FPS and some monitors update about once per second, while Pipeline values are cumulative for the current run. Use this page to spot a direction quickly, then use the Godot Profiler to locate specific functions.

The Events page summarizes event types and listener counts. Its search field matches both short and fully qualified type names, and selecting a row displays the full type name below the list so same-named events from different namespaces remain distinguishable. This page shows events that currently have listeners; it does not retain an event-emission history.

The Input page uses status cards for the current backend, active device, sample sequence, and Action count, followed by separate Context-stack and Action-state tables. The sequence normally increases after each successful per-frame sample, remains unchanged after a failed sample, and resets when the backend is reinstalled or the service shuts down. Action rows include value type, current value, and just-pressed/just-released edges. Search filters the complete snapshot by Action name or value type and renders at most the first 32 matches.

The Audio page reports whether BGM is loading, playing, loaded but not playing, or stopped, together with the current resource key. Its SFX card shows active voices, capacity, and utilization, while the lower cards show linear Master, BGM, and SFX volume. Because the current audio interface cannot distinguish a paused stream from one that ended naturally, either case is conservatively reported as loaded but not playing.

The Scene page uses status cards for the current scene, node count, transition state, and progress. Its detail table shows the resource key currently loading and the most recent transition target and result. Node counting runs once per second only while this page is selected. Missing SceneService registration and custom implementations without a Debug snapshot are shown as explicit degraded states.

The Resources page uses summary cards for active loads, synchronous/asynchronous requests, same-key merges, and success/failure totals. Its active-request table is stably sorted by resource key and renders at most 32 entries. ResourceHub retains 32 recent requests in memory, while the page displays the newest eight first. These values describe ResourceHub requests and bounded history, not the global Godot cache or a persistent resource log.

The DataTable page reports published data sets, cached tables, active loads, and cumulative failures. Expand a data-set row to inspect table IDs and actual cached types; loading rows show table-level progress and the runtime directory. Recent results cover successful loads, cancellation, failure, and unload operations. The page renders at most 32 data sets and 64 tables, retains 16 results, and displays the newest eight. This diagnostic state exists only in Debug builds and adds no Release history.

The UI page reports the number of Scene-layer interfaces, View and Modal stack depths, and the current topmost View or Modal. Its list is ordered from top to bottom and shows each managed node, its opening resource key, and its visible or hidden state, making return-order and covered-View issues easy to inspect; an unusually deep stack renders only the top 64 entries. Missing UiService registration, custom implementations without snapshot support, and nodes released outside the service are shown explicitly. Resource keys for this page are recorded only in Debug builds.

The Procedure page reports the current procedure, entering or exiting phase, and pending request. Details retain the previous procedure, latest success, and latest failure. A failure stores only one summary of at most 256 characters and does not retain the exception object; this diagnostic state is absent from Release builds.

The Console toolbar provides counted All, Debug, Info, Warning, and Error chips. All is selected by default. Click a level to show only that level, click additional levels to combine them, or click All to reset. Search scans the complete in-memory history; when results span multiple pages, use Previous and Next, or click the separate Latest Logs button on the right to return directly to the final page and scroll to the bottom. While you remain on the latest page and refresh is not paused, new logs automatically follow the bottom. Scrolling upward stops following and enables Latest Logs; scrolling back to the bottom or clicking that button resumes following. Pause stops automatic refresh and scrolling. Copy copies only the text currently displayed by the active filters, search, and page. The search field captures input only after a click and releases focus when you submit the search or leave the Console.

Console pages retain only limited recent data. LogHub uses a 1,000-entry ring; filtering and search scan the entire history, while each page renders at most 100 normal logs. Consecutive identical logs are aggregated into a `×count` entry with first and last timestamps. The Godot output console receives at most 100 LogHub lines per second, while suppressed lines remain available in Debugger history. ErrorHub summary capacity is 16, and each Warning/Error filter displays at most 12 matching entries. This is a quick inspection tool, not a persistent log archive or profiler.

## Inspect rolling logs across sessions

GoDoRuntime automatically writes logs to `user://logs/godo_framework.log`. A file rolls after reaching 2 MiB, and up to four archives named `godo_framework.1.log` through `godo_framework.4.log` are retained. Debug builds record Debug, Info, Warning, Error, and Fatal entries. Release builds record only the Warning, Error, and Fatal entries that remain in the compiled application.

Disk writes run through a bounded background queue and do not block error dispatch. A full queue drops entries and emits a summary warning. If the directory cannot be created or the disk cannot be written, file logging is disabled for the current run and the console reports the failure once. Other tools may open the active log file for reading while the game runs. The current baseline is validated on Windows; mobile sandbox paths and shutdown flushing still require device testing.

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
