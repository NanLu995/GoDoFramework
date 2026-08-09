---
translation_of: Docs/Manual/zh-cn/guides/ui-and-audio/index.md
translation_source_hash: sha256:a9039443efd19982db30d73f217fb368d4dd7804ae04d2d16baaecd16c4b98ee
---

# Organize Complex UI and Long-Lived Audio

UiService manages screen-space `Control` layers, instances, and back order. AudioService manages non-spatial BGM, non-spatial/3D short SFX, and group volume. GoDoRuntime owns both for the long term, so changing the main scene does not free Views, Modals, or playing music.

Game code still owns UI content, input priority, pause policy, animation, and concrete audio choices.

## 1. Choose the correct UI layer

| Layer | Purpose | Scene change | Back behavior |
|---|---|---|---|
| `Scene` | HUD, crosshair, level hint | Cleared after a successful change | Not on the back stack |
| `View` | Settings, inventory, full menu | Retained by default | New View hides the old one; back restores it |
| `Modal` | Confirmation and blocking choices | Retained by default | Only the top Modal can close |
| `Overlay` | Toasts, loading indicators, guide masks | Retained by default | Not on the back stack |

```csharp
Control hud = ui.Open(HudKey, UiLayer.Scene);
Control inventory = ui.Open(InventoryKey, UiLayer.View);
Control confirm = ui.Open(ConfirmKey, UiLayer.Modal);
Control toast = ui.Open(ToastKey, UiLayer.Overlay);
```

The UI PackedScene root must inherit `Control`. World-space health bars, Node2D/Node3D labels, and character-following UI remain game-scene responsibilities.

## 2. Maintain interfaces in a UiConfig

Create a `UiConfig` Resource in the Godot Inspector. Expand `Entries` and configure each `UiConfigEntry`:

- `Id`: the semantic identifier used by game code.
- `Locator`: select the UI PackedScene with the file picker.
- `Layer`: the default Scene, View, Modal, or Overlay layer.
- `InstanceMode`: `Single` or `Multiple`.
- `ReuseInstance`: whether closing retains one `Single` instance for reuse.

Load the catalog during startup before opening managed UI:

```csharp
private static readonly ResourceKey UiConfigKey =
    ResourceKey.Create("res://Config/UiConfig.tres");
private static readonly UiId SettingsId = UiId.Create("settings");

IUiService ui = context.GetService<IUiService>();
ui.LoadUiConfig(UiConfigKey);
Control settings = ui.Open(SettingsId);
```

Configuration loading validates empty or duplicate IDs, invalid resource locators, layers, instance policies, and reuse combinations in one step. A `Single` ID cannot be opened twice; `Multiple` creates a separate instance for each open. An unregistered ID throws instead of silently returning null. `Open(ResourceKey, UiLayer)` remains available for low-level calls that do not need configuration or need to choose a layer directly.

### Async opening and cancellation

```csharp
SettingsView settings = await ui.OpenAsync<SettingsView>(
    SettingsId,
    view => view.Initialize(model),
    progress => loadingBar.Value = progress * 100f,
    cancellationToken);
```

After threaded resource loading, nodes are still instantiated, configured, and mounted on Godot's main thread. A cancellation token or `CancelOpenRequests(SettingsId)` prevents the request from mounting, but does not stop a ResourceHub load that may be shared. A scene change automatically cancels pending Scene-layer requests.

### Queries, bulk closing, and reuse

```csharp
if (ui.TryGetTop<SettingsView>(SettingsId, out var settings))
    settings.Refresh();

ui.CloseAll(UiLayer.Overlay);
ui.CloseTo(SettingsId); // Keep Settings and close UI above it.
ui.ClearCachedInstance(SettingsId);
```

Use `IsOpen`, `GetOpenCount`, `IsOpening`, and `GetOpeningCount` for game-state decisions. With `ReuseInstance`, a closed `Single` node still consumes memory and retains state and signal connections. Enable it only for interfaces with measured creation cost that can reliably reset on every open.

## 3. Give every UI instance an owner

The flow or coordinator that opens UI retains ownership and closes what it created. A Procedure can hand a `UiScope` directly to activation cleanup:

```csharp
public async Task EnterAsync(ProcedureContext context)
{
    IUiService ui = context.GetService<IUiService>();
    UiScope<GameplayHud> hud = ui.OpenScoped<GameplayHud>(HudId);
    context.RegisterCleanup(hud);
    hud.View.Refresh();
}
```

Call `UiScope.Dispose()` only on Godot's main thread. It is idempotent and completes normally if another path already closed the interface. Owners outside a Procedure may still retain the node and clean it up symmetrically through `Close` or `TryClose`.

Managed UI should exit through `Close()` or `TryGoBack()`, not direct `QueueFree()` or `RemoveChild()`. If an interface is freed externally, UiService removes the stale record on its next operation, restores the previous valid View, and releases an empty Modal Host. Direct removal or reparenting still bypasses normal ownership and back order. A covered View is hidden, not freed, so its state and memory remain. Avoid an indefinitely deep View stack.

## 4. Centralize back input

UiService does not listen to `ui_cancel`, Android Back, or gamepad buttons. A single game input boundary decides the order:

```csharp
private void HandleBackRequested()
{
    if (_ui.TryGoBack())
        return;

    EventChannel.Emit<PauseRequestedEvent>();
}
```

`TryGoBack()` closes the top Modal first, then the top View, and returns `false` when neither exists. Scene and Overlay do not participate in back navigation. Do not let HUD, menus, and character controllers all handle the same back Action.

The Modal Host blocks pointer events from reaching lower Controls, but it does not pause SceneTree or prevent keyboard, gamepad, and `_UnhandledInput` processing. When opening a pause Modal:

1. Let a Procedure or pause coordinator choose SceneTree pause policy.
2. Change InputService Context to suppress Gameplay Actions.
3. Restore both in reverse order when closing.

## 5. Handle UI opening failures

```csharp
try
{
    _ui.Open(SettingsKey, UiLayer.View);
}
catch (UiOpenException exception)
{
    ErrorHub.Report(exception, "Game.UI", context: SettingsKey.Value);
    ShowFallbackMessage();
}
```

A missing Resource, non-Control root, instantiation failure, or tree attachment failure throws `UiOpenException`. `Phase` distinguishes Loading, Preparing, and Committing; recovery and logs should use this structured value instead of parsing messages. Failure does not hide the current View or modify managed layer state.

Closing unmanaged UI, a non-top View, or a non-top Modal throws `InvalidOperationException`. This normally indicates broken ownership or ordering and should not be silently ignored.

## 6. Let Procedures select BGM

```csharp
IAudioService audio = context.GetService<IAudioService>();

try
{
    await audio.PlayBgmAsync(GameAudio.GameplayTheme);
}
catch (OperationCanceledException)
{
    // StopBgm or framework shutdown cancelled a pending load.
}
catch (AudioPlaybackException exception)
{
    ErrorHub.Report(exception, "Game.Audio", GameAudio.GameplayTheme.Value);
}
```

Requesting the same Resource does not restart it by default; pass `restart: true` only when restart is intentional. Do not Stop before loading the next BGM. AudioService replaces the current stream after loading, reducing silence.

Only one BGM load may run at a time. Serialize flow changes instead of letting several pages compete for music. Call `StopBgm()` explicitly for a silent state.

`StopBgm()` releases the logical loading state immediately, so a replacement BGM request may start at once. The old waiter receives `OperationCanceledException` after ResourceHub's shared underlying load finishes and cannot overwrite the replacement state.

`PauseBgm()` and `ResumeBgm()` affect only current BGM, not SFX or SceneTree. The game design decides whether a pause menu pauses music.

Use the separate Crossfade API when a smooth transition is required, without changing the existing immediate-switch contract:

```csharp
using CancellationTokenSource transitionCancellation = new();

try
{
    await audio.CrossfadeBgmAsync(
        GameAudio.GameplayTheme,
        durationSeconds: 0.5d,
        cancellationToken: transitionCancellation.Token);
}
catch (OperationCanceledException)
{
    // The caller, StopBgm, shutdown, or a newer playback request cancelled this transition.
}
```

The old track continues while the target Resource loads. Once ready, two long-lived players apply an equal-power crossfade. Transition time ignores `Engine.TimeScale` but still follows the AudioService node's SceneTree pause state. A newer Crossfade request wins and the older Task receives `OperationCanceledException`. If fading has started, the service keeps the louder player so cancellation does not produce complete silence. The existing `PlayBgmAsync` still rejects concurrent calls while any BGM request is active.

`BgmState` distinguishes loading, playing, paused, transitioning, natural completion, and fully stopped states. Until a normal Crossfade completes, `CurrentBgm` remains the previously committed key. `PauseBgm()` pauses both players and transition progress; `StopBgm()` cancels the request and clears both players.

To enter a silent BGM state smoothly, fade the current track instead of repeatedly changing Bus volume:

```csharp
try
{
    await audio.FadeOutBgmAsync(
        durationSeconds: 0.5d,
        cancellationToken: context.LifetimeToken);
}
catch (OperationCanceledException)
{
    // This flow ended, or a newer Crossfade/FadeOut replaced this fade.
}
```

After FadeOut completes, state is `Stopped` and `CurrentBgm` is empty. With no current track, it completes immediately. Caller cancellation after fading starts restores the current track at normal volume. A newer Crossfade or FadeOut cancels the older transition and owns the resulting state.

## 7. Treat short-SFX capacity correctly

```csharp
try
{
    bool played = await audio.PlaySfxAsync(
        GameAudio.ButtonClick,
        volumeLinear: 0.75f,
        pitchScale: 1.05f);
    if (!played)
        LogHub.Debug("SFX capacity reached.", "Game.Audio");
}
catch (OperationCanceledException)
{
}
catch (AudioPlaybackException exception)
{
    ErrorHub.Report(exception, "Game.Audio", GameAudio.ButtonClick.Value);
}
```

`false` means Voice capacity is full, a normal capacity branch rather than corrupt content. Defaults are eight prewarmed and 32 maximum Voices. Loading requests reserve capacity so simultaneous completions cannot exceed the limit.

When per-play parameters are omitted, both volume and pitch scale default to 1. `volumeLinear` must be finite and between 0 and 1. `pitchScale` must be finite and positive, and changes both pitch and playback speed. Invalid values throw `ArgumentOutOfRangeException` before resource loading or capacity reservation. Values affect only the current playback and reset when its Voice returns to the pool. Game code remains responsible for choosing any random variation.

### Prepare Resources and Voices before a large encounter

Move known audio loading and player instantiation into the dungeon loading flow instead of waiting for the first wave of simultaneous abilities:

```csharp
await Task.WhenAll(
    audio.PrepareSfxAsync(GameAudio.PlayerHit, loadingToken),
    audio.PrepareSfxAsync(GameAudio.RemoteExplosion, loadingToken),
    audio.PrepareSfxAsync(GameAudio.CriticalWarning, loadingToken));

int createdVoices = audio.PrewarmSfxVoices(32);
int created3DVoices = audio.PrewarmSfx3DVoices(32);
```

`PrepareSfxAsync` reuses ResourceHub and Godot's Resource cache. It creates no second Audio cache and reserves no active or pending Voice capacity. Caller cancellation stops only that wait; a shared underlying load may continue. `StopAllSfx()` does not cancel preparation, while AudioService shutdown cancels outstanding waits. A load failure or non-AudioStream Resource throws `AudioPlaybackException`.

`PrewarmSfxVoices` synchronously instantiates non-spatial Voices on the main thread, while `PrewarmSfx3DVoices` warms the independent 3D pool. Each returns the number created by that call. Active and idle Voices count toward the corresponding Prepared value; repeating the same or a smaller target creates nothing and never shrinks a pool. A target cannot exceed its matching Max capacity. Instantiation trades loading-screen time and retained objects for a smoother combat burst, so do not call it during a combat frame.

`StopAllSfx()` immediately returns active Voices and releases logical capacity reserved by pending requests. Old waiters end with `OperationCanceledException` after the shared underlying load finishes and cannot decrement capacity owned by the new request generation.

### Handle combat-scale SFX bursts

Do not handle a same-frame burst by blindly raising the Voice limit or queueing late sounds. Give disposable sounds a low priority and a per-Resource limit, then let critical feedback opt into explicit preemption:

```csharp
SfxPlaybackResult result = await audio.PlaySfxAsync(
    GameAudio.RemoteExplosion,
    new SfxPlaybackOptions(
        volumeLinear: 0.7f,
        pitchScale: 1f,
        priority: SfxPriority.Low,
        maxConcurrentPerKey: 4,
        allowStealLowerPriority: false));

if (result.Status == SfxPlaybackStatus.GlobalCapacityReached ||
    result.Status == SfxPlaybackStatus.PerKeyLimitReached)
{
    // Expected drop; do not queue it for late playback.
}
```

Player confirmation and critical warnings can use `High` or `Critical` with `allowStealLowerPriority`. At full capacity, AudioService considers only lower-priority candidates, chooses the lowest priority first, then the oldest submission within that priority. Both active Voices and pending loads participate. Requests made through the legacy bool API participate as `Normal`, so an advanced request may replace them: a pending legacy request returns `false`, while an active sound stops early.

`maxConcurrentPerKey` counts both active and pending requests for the same Resource. It is useful for repeated explosions and impacts, but does not replace distance cutoff. An extreme battle can still skip obviously inaudible requests in game code based on Listener distance.

A successful result provides an `SfxPlaybackHandle`. A looping AudioStream never emits Finished, so retain its Handle and call `TryStopSfx(handle)`. Natural completion, stopping, preemption, or service shutdown invalidates the Handle; `IsSfxPlaying(handle)` and repeated stopping then return `false`. `StopAllSfx()` remains the flow-level cleanup operation.

### Play 3D SFX at a static world position

Explosions, impact points, and landing sounds can submit a world position with an explicit audible range:

```csharp
Sfx3DPlaybackResult result = await audio.PlaySfx3DAsync(
    GameAudio.RemoteExplosion,
    explosion.GlobalPosition,
    new Sfx3DPlaybackOptions(
        maxDistance: 80f,
        unitSize: 8f,
        volumeLinear: 0.8f,
        priority: SfxPriority.Low,
        maxConcurrentPerKey: 8));

if (result.Started)
{
    // Retain the Handle for a loop or early termination.
    audio.TryStopSfx3D(result.Handle);
}
```

`MaxDistance` and `UnitSize` must be finite and positive. Maximum distance lets Godot stop mixing after the Listener moves out of range and must not be omitted as an unbounded value. The attenuation curve also depends on `AttenuationModel`. The 3D and non-spatial pools keep separate capacity, prewarming, rejection, and preemption statistics and never preempt across pools. Resources still use the same `PrepareSfxAsync` path.

Keep short explosions, impacts, and footsteps on the static-position entry to avoid continuous synchronization. Use the follow entry only for moving engine loops, sustained abilities, and similar sounds that genuinely need a target:

```csharp
Sfx3DPlaybackResult engineLoop = await audio.PlaySfx3DFollowAsync(
    GameAudio.VehicleEngine,
    vehicle,
    new Vector3(0f, 0.5f, -1f),
    new Sfx3DPlaybackOptions(maxDistance: 60f, unitSize: 4f));
```

Follow positions update at the fixed physics rate, not the render rate, and AudioService disables this physics path while no followed Voice is active. `MaxFollowingSfx3DVoices` is an independent hard budget, defaults to 16, and cannot exceed `MaxSfx3DVoices`. Active plus pending follow requests return `FollowCapacityReached` at that limit. A target must be in the scene tree when submitted. Losing it during loading returns `TargetUnavailableBeforeStart`; leaving the tree or deletion during playback stops the Voice automatically. A loop still requires retaining its Handle and stopping it when business ownership ends.

The Viewport also needs an active `Camera3D` or `AudioListener3D`. Use `TryStopSfx3D`, `IsSfx3DPlaying`, and `StopAllSfx3D` for the independent 3D Handles and pool; they do not stop non-spatial SFX.

## 8. Volume, settings, and Audio Bus

```csharp
audio.SetVolume(AudioGroup.Master, settings.MasterVolume);
audio.SetVolume(AudioGroup.Bgm, settings.BgmVolume);
audio.SetVolume(AudioGroup.Sfx, settings.SfxVolume);
```

Values are finite linear numbers from 0 to 1. Apply them immediately after SettingsService loads player settings, preview slider changes, and let the settings page choose when to save.

Prefer defining `BGM` and `SFX` in the project's Audio Bus Layout. When missing, the framework creates them at runtime with a Warning but does not modify the persistent layout. Treat this as a fallback, not the production configuration workflow.

## 9. Scene and framework shutdown

- `GoDoUI`, AudioService, and players live outside CurrentScene.
- A successful scene change clears Scene UI but retains Views, Modals, and audio.
- A flow explicitly closes the Views and Modals it owns; it must not clear another system's pages.
- AudioService exit stops both BGM players, cancels loads and transitions, and disposes both non-spatial and 3D SFX pools.
- `StopAllSfx()` and `StopAllSfx3D()` independently return active Voices and cancel the matching pending requests.

Every UI and Audio public API is main-thread only. Opening UI and first-time audio loading do not belong on a per-frame path.

## Common failures

- The character moves behind a Modal: Modal blocks only GUI pointers; change Input Context or pause the flow.
- One back press closes the wrong page: several Nodes handle back input; centralize it.
- A View unexpectedly survives a scene change: that is default behavior; its owner must close it.
- UI order looks wrong after direct QueueFree: continuing through UiService removes stale records, but game code should still use Close/TryGoBack to preserve explicit ownership.
- A BGM request is occasionally rejected: another BGM is still loading because flows were not serialized.
- A Crossfade Task is cancelled: a newer playback request replaced it, or the caller, Stop, or framework lifecycle cancelled it. Handle it as flow ownership rather than corrupt content.
- SFX returns false: the legacy entry reached capacity or its pending reservation was replaced by an advanced request. Skip noncritical sound; use the structured entry when the rejection reason matters.
- The first large SFX burst still hitches: call `PrepareSfxAsync` during dungeon loading and prewarm non-spatial and 3D Voices independently to measured budgets. Do not only raise the maximum.
- A looping SFX never returns: loops do not finish naturally. Retain the successful Handle and call `TryStopSfx` when its owner ends.
- Volume resets after restart: SetVolume was called without saving through SettingsService.
- A 3D sound has no position: verify that it uses `PlaySfx3DAsync` or `PlaySfx3DFollowAsync`, the Viewport has an active Listener, and its position, `UnitSize`, and `MaxDistance` match the game world's scale.
- A moving object's 3D sound remains behind: the static entry samples only the submitted position. Use `PlaySfx3DFollowAsync` for sustained moving audio and check whether follow capacity or target lifecycle rejected the request.

For exact members, see <xref:GoDo.IUiService>, <xref:GoDo.UiLayer>, <xref:GoDo.UiOpenException>, <xref:GoDo.IAudioService>, <xref:GoDo.BgmPlaybackState>, <xref:GoDo.SfxPlaybackOptions>, <xref:GoDo.SfxPlaybackResult>, <xref:GoDo.SfxPlaybackHandle>, <xref:GoDo.Sfx3DPlaybackOptions>, <xref:GoDo.Sfx3DPlaybackResult>, <xref:GoDo.Sfx3DPlaybackHandle>, <xref:GoDo.AudioGroup>, and <xref:GoDo.AudioPlaybackException>.
