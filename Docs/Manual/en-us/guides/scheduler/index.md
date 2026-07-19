---
translation_of: Docs/Manual/zh-cn/guides/scheduler/index.md
translation_source_hash: sha256:ca8e8a27814b4596391d7dac7155f86aa3c047205e3d7f81551ce0cc92ff912c
---

# Schedule Delays, Repeating Tasks, and Async Waits

SchedulerService centralizes one-shot delays, repeating callbacks, and cancellable waits on Godot's main thread. It makes three choices explicit: whether slow motion affects time, whether time continues while the SceneTree is paused, and whether callbacks run during Process or Physics. Tasks can also cancel automatically when a scene owner leaves.

It does not create a `Timer` Node per task. A simple timer that belongs entirely to one scene and benefits from Inspector configuration is often clearer as a Godot `Timer`.

## When to use Scheduler

Use it for:

- Cooldowns, countdowns, delayed hint dismissal, and periodic checks.
- UI or connection timeouts that continue while the game is paused.
- Cancellable async waits inside Procedures.
- Tasks that must be cleaned up when a scene exits.

It is not intended for:

- Deterministic ticks, network synchronization clocks, or combat replay.
- Tween, AnimationPlayer, or audio-sample timing.
- Background-thread work, calendar reminders, offline rewards, or persisted timers.

## 1. Choose the correct clock

| Clock | Affected by `Engine.TimeScale` | Advances while SceneTree is paused | Typical use |
|---|---:|---:|---|
| `GameTime` | Yes | No | Cooldowns and gameplay countdowns |
| `UnscaledGameTime` | No | No | Logic unaffected by slow motion but stopped by game pause |
| `RealTime` | No | Yes | Pause UI, connection timeout, and debug hints |

The default is `GameTime + Process`:

```csharp
ISchedulerService scheduler = Services.Get<ISchedulerService>();

scheduler.Schedule(
    0.5,
    () => GD.Print("Runs after half a second."));
```

To continue during pause:

```csharp
scheduler.Schedule(
    3.0,
    CloseNotification,
    ScheduleOptions.RealTime);
```

`RealTime` means unaffected by TimeScale and SceneTree pause. It does not mean background execution; callbacks still run on Godot's main thread.

## 2. Bind a task to a scene Node

Assign an Owner that is already inside the tree:

```csharp
var options = new ScheduleOptions(
    clock: ScheduleClock.GameTime,
    phase: SchedulePhase.Process,
    owner: this);

ScheduleHandle handle = scheduler.Schedule(
    1.0,
    ShowNextHint,
    options);
```

When the Owner exits the scene tree, its tasks and async waits cancel automatically. Multiple tasks for one Owner share one exit listener, which is removed when no associated tasks remain.

The Owner must be valid and inside the tree. Do not pass a newly constructed Node that has not been attached, and do not create work for an object about to be `QueueFree()`d.

A task with a null Owner can survive a main-scene change. Its caller must retain a handle or CancellationToken and explicitly clean it up. Do not let a global task's closure retain an old scene object indefinitely.

## 3. Schedule repeating work

Run once per second, including a one-second wait before the first run:

```csharp
ScheduleHandle handle = scheduler.ScheduleRepeating(
    1.0,
    UpdateCountdown,
    new ScheduleOptions(owner: this));
```

Wait 0.25 seconds initially, then run every second:

```csharp
ScheduleHandle handle = scheduler.ScheduleRepeating(
    initialDelaySeconds: 0.25,
    intervalSeconds: 1.0,
    callback: UpdateCountdown,
    options: new ScheduleOptions(owner: this));
```

If a long frame misses several periods, Scheduler coalesces them into one callback and advances the next due time into the future. It does not burst old callbacks in one frame. For exact accumulation, recompute from authoritative state instead of treating callback count as absolute elapsed time.

Callback exceptions are reported through ErrorHub. A one-shot task ends; a repeating task is cancelled so it cannot produce the same error every period.

## 4. Cancel, pause, and inspect a task

```csharp
if (scheduler.IsScheduled(handle) &&
    scheduler.TryGetRemainingSeconds(handle, out double remaining))
{
    GD.Print($"{remaining:F2} seconds remain");
}

scheduler.Pause(handle);
scheduler.Resume(handle);
scheduler.Cancel(handle);
```

`Pause` stores remaining time in the task's own clock, and `Resume` continues from it. This pauses only that task, not SceneTree or TimeScale.

These methods return `false` for an invalid handle, a completed task, or an incompatible state; a missing task does not throw. `default(ScheduleHandle)` is invalid.

A repeating task may cancel or pause itself inside its callback:

```csharp
ScheduleHandle heartbeat = default;
heartbeat = scheduler.ScheduleRepeating(1.0, () =>
{
    if (!ShouldContinue())
        scheduler.Cancel(heartbeat);
}, new ScheduleOptions(owner: this));
```

## 5. Await from a Procedure

```csharp
using var cancellation = new CancellationTokenSource();

await scheduler.DelayAsync(
    1.0,
    new ScheduleOptions(
        ScheduleClock.UnscaledGameTime,
        SchedulePhase.Process),
    cancellation.Token);
```

After normal completion, the continuation resumes on the main thread. A Token may be triggered from a background thread, but cancellation takes effect during the next Scheduler main-thread update. Owner exit, framework shutdown, and Token cancellation all complete the wait as cancelled, so async flow should handle `OperationCanceledException` normally.

When a wait belongs to a Node, use both an Owner and a Procedure CancellationToken if useful: Owner covers scene lifetime; Token covers deliberate flow cancellation.

## 6. Choose Process or Physics

Default `Process` fits UI, flow, audio control, and ordinary business delays. Choose Physics only when the callback must align with a physics-update boundary:

```csharp
var physicsOptions = new ScheduleOptions(
    ScheduleClock.GameTime,
    SchedulePhase.Physics,
    owner: this);
```

Physics does not turn Scheduler into a deterministic simulator. Frame variation, TimeScale, and floating-point time remain. A deterministic combat system needs its own fixed-tick model.

A zero-second task runs no earlier than the next update of its selected phase. It does not reenter synchronously inside `Schedule()`. This can defer work to the next frame boundary, but it should not replace a more explicit Godot signal or Deferred API.

## 7. Diagnose with Debugger

In a Debug build, open **Runtime / Scheduler** in the GoDo Debugger to inspect:

- Active, paused, and repeating task counts.
- Task distribution across three clocks and Process/Physics.
- Last dispatch and cumulative cancellation counts.
- Owner-driven and callback-exception cancellation counts.
- Remaining time for the next task in each time domain.

Snapshots are generated at low frequency only while the page is visible and are not Release API. If task count keeps growing, inspect ownerless repeating work, undisposed Procedure tokens, and cross-scene waits that are created without cancellation.

## Parameters and shutdown

- A delay must be finite and at least zero seconds.
- A repeating interval must be finite and greater than zero.
- Every public API is main-thread only; only CancellationToken triggering may originate in the background.
- GoDoRuntime shutdown cancels every task and completes unfinished `DelayAsync` calls as cancelled.
- New tasks are rejected after framework shutdown.

## Common failures

- A pause-menu countdown stops: it uses GameTime or UnscaledGameTime; select RealTime.
- A UI hint slows down with slow motion: replace default GameTime with UnscaledGameTime or RealTime.
- A callback accesses a freed Node after scene change: the task has no Owner, or its closure retains the old scene.
- A repeating task does not replay every missed period after a hitch: missed periods are intentionally coalesced.
- `DelayAsync` throws cancellation: its Owner, Token, or framework shutdown cancelled it as designed.
- A Physics callback is not fully deterministic: Physics is a dispatch phase, not a deterministic tick system.
- A Release build cannot read diagnostics: Scheduler snapshots exist only for Debugger in Debug builds.

For exact members, see <xref:GoDo.ISchedulerService>, <xref:GoDo.ScheduleOptions>, <xref:GoDo.ScheduleClock>, <xref:GoDo.SchedulePhase>, and <xref:GoDo.ScheduleHandle>.
