---
translation_of: Docs/Manual/zh-cn/guides/ui-and-audio/index.md
translation_source_hash: sha256:0cdaaa524e27ee0182f2ad0d58ed0560139bac46f7f2336af5529b1cfd583fe7
---

# Organize Complex UI and Long-Lived Audio

UiService manages screen-space `Control` layers, instances, and back order. AudioService manages non-spatial BGM, short SFX, and group volume. GoDoRuntime owns both for the long term, so changing the main scene does not free Views, Modals, or playing music.

Game code still owns UI content, input priority, pause policy, animation, and concrete audio choices.

## 1. Choose the correct UI layer

| Layer | Purpose | Scene change | Back behavior |
|---|---|---|---|
| `Scene` | HUD, crosshair, level hint | Cleared after a successful change | Not on the back stack |
| `View` | Settings, inventory, full menu | Retained by default | New View hides the old one; back restores it |
| `Modal` | Confirmation and blocking choices | Retained by default | Only the top Modal can close |

```csharp
Control hud = ui.Open(HudKey, UiLayer.Scene);
Control inventory = ui.Open(InventoryKey, UiLayer.View);
Control confirm = ui.Open(ConfirmKey, UiLayer.Modal);
```

The UI PackedScene root must inherit `Control`. World-space health bars, Node2D/Node3D labels, and character-following UI remain game-scene responsibilities.

## 2. Give every UI instance an owner

The flow or coordinator that opens UI retains its instance and closes what it created:

```csharp
private IUiService? _ui;
private Control? _hud;

public async Task EnterAsync(ProcedureContext context)
{
    _ui = context.GetService<IUiService>();
    _hud = _ui.Open(HudKey, UiLayer.Scene);
}

public Task ExitAsync(ProcedureContext context)
{
    if (_ui != null && _hud != null && GodotObject.IsInstanceValid(_hud))
        _ui.Close(_hud);

    _hud = null;
    _ui = null;
    return Task.CompletedTask;
}
```

Never call `QueueFree()` or `RemoveChild()` on managed UI. That bypasses UiService collections and back-stack maintenance. A covered View is hidden, not freed, so its state and memory remain. Avoid an indefinitely deep View stack.

## 3. Centralize back input

UiService does not listen to `ui_cancel`, Android Back, or gamepad buttons. A single game input boundary decides the order:

```csharp
private void HandleBackRequested()
{
    if (_ui.TryGoBack())
        return;

    EventChannel.Emit<PauseRequestedEvent>();
}
```

`TryGoBack()` closes the top Modal first, then the top View, and returns `false` when neither exists. Do not let HUD, menus, and character controllers all handle the same back Action.

The Modal Host blocks pointer events from reaching lower Controls, but it does not pause SceneTree or prevent keyboard, gamepad, and `_UnhandledInput` processing. When opening a pause Modal:

1. Let a Procedure or pause coordinator choose SceneTree pause policy.
2. Change InputService Context to suppress Gameplay Actions.
3. Restore both in reverse order when closing.

## 4. Handle UI opening failures

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

A missing Resource, non-Control root, instantiation failure, or tree attachment failure throws `UiOpenException`. Failure does not hide the current View or modify managed layer state.

Closing unmanaged UI, a non-top View, or a non-top Modal throws `InvalidOperationException`. This normally indicates broken ownership or ordering and should not be silently ignored.

## 5. Let Procedures select BGM

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

`PauseBgm()` and `ResumeBgm()` affect only current BGM, not SFX or SceneTree. The game design decides whether a pause menu pauses music.

## 6. Treat short-SFX capacity correctly

```csharp
try
{
    bool played = await audio.PlaySfxAsync(GameAudio.ButtonClick);
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

A non-looping sound returns to the pool after natural completion. A looping AudioStream never emits Finished, and the current public API has no per-SFX Handle. Use `StopAllSfx()` for global cleanup, or a game-owned `AudioStreamPlayer` / `AudioStreamPlayer2D/3D` for a loop or spatial sound requiring independent control.

Do not use global SFX playback for footsteps, engines, or ambient loops that need positioning and individual stopping.

## 7. Volume, settings, and Audio Bus

```csharp
audio.SetVolume(AudioGroup.Master, settings.MasterVolume);
audio.SetVolume(AudioGroup.Bgm, settings.BgmVolume);
audio.SetVolume(AudioGroup.Sfx, settings.SfxVolume);
```

Values are finite linear numbers from 0 to 1. Apply them immediately after SettingsService loads player settings, preview slider changes, and let the settings page choose when to save.

Prefer defining `BGM` and `SFX` in the project's Audio Bus Layout. When missing, the framework creates them at runtime with a Warning but does not modify the persistent layout. Treat this as a fallback, not the production configuration workflow.

## 8. Scene and framework shutdown

- `GoDoUI`, AudioService, and players live outside CurrentScene.
- A successful scene change clears Scene UI but retains Views, Modals, and audio.
- A flow explicitly closes the Views and Modals it owns; it must not clear another system's pages.
- AudioService exit stops BGM, cancels loads, and disposes the SFX pool.
- `StopAllSfx()` returns active Voices and cancels pending SFX requests.

Every UI and Audio public API is main-thread only. Opening UI and first-time audio loading do not belong on a per-frame path.

## Common failures

- The character moves behind a Modal: Modal blocks only GUI pointers; change Input Context or pause the flow.
- One back press closes the wrong page: several Nodes handle back input; centralize it.
- A View unexpectedly survives a scene change: that is default behavior; its owner must close it.
- Direct QueueFree corrupts navigation: managed UI exits through Close/TryGoBack.
- A BGM request is occasionally rejected: another BGM is still loading because flows were not serialized.
- SFX returns false: capacity is full; skip noncritical sound or revisit design.
- A looping SFX never returns: loops do not finish naturally; use a separately managed game player.
- Volume resets after restart: SetVolume was called without saving through SettingsService.
- A spatial sound has no position: AudioService handles non-spatial audio only.

For exact members, see <xref:GoDo.IUiService>, <xref:GoDo.UiLayer>, <xref:GoDo.UiOpenException>, <xref:GoDo.IAudioService>, <xref:GoDo.AudioGroup>, and <xref:GoDo.AudioPlaybackException>.
