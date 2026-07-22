---
translation_of: Docs/Manual/zh-cn/troubleshooting/index.md
translation_source_hash: sha256:9c7ef5880c9fbdda8ef4679cd8e979744753670538ac3c1dd48b65bbf4a1d08d
---

# Troubleshooting

This page is organized by runtime symptom. Find the closest match and confirm causes in order. Do not start by editing framework source or swallowing exceptions.

## Setup cannot install Runtime

**Symptom:** Install Runtime is disabled, or Setup still reports an error afterward.

**Confirm:**

- The project uses the Godot .NET version declared in Getting Started.
- Exactly one `.csproj` exists at root and a Debug build succeeded.
- `addons/godo_framework/Core/GoDoRuntime.tscn` exists.
- No other Autoload owns `GoDoRuntime` or points another name to the same path.

**Fix:** Resolve the earliest Setup error and refresh. Never manually add a second Runtime. See [Install, Upgrade, and Uninstall](../getting-started/install-upgrade-uninstall.md).

## `Services.Get<T>()` says the service is not registered

Common causes are a missing Runtime installation, a call before Runtime initialization, a duplicate framework instance in a game scene, or an interface that is not registered.

Confirm the single Autoload, then query from scene `_Ready()` or a Procedure. Do not change required service access to `TryGet` merely to hide the fault. See [Services and Events](../guides/services-and-events/index.md).

## A Resource or scene cannot load

For `ResourceLoadException` or `SceneChangeException`, confirm that:

- The Key is canonical `res://` or valid `uid://`, with matching case.
- Boot loaded ResourceManifest and the semantic ID exists.
- The requested generic type matches the actual Resource.
- The same path is not being loaded asynchronously as another type.

A pre-commit scene failure should preserve the old scene. Record `Key` and `InnerException` rather than immediately repeating the request. See [Resources and Scenes](../guides/resources-and-scenes/index.md).

## Code fails after a scene change

After successful commit, SceneService calls `QueueFree()` on the old CurrentScene. Once `await ChangeAsync()` returns, use only the returned new scene and no old Node references.

Put loading UI in a persistent UI layer, not the scene being replaced. Disable repeated transition input and serialize it through Procedure.

## UI back order is wrong or a page remains

Check whether managed UI was directly `QueueFree()`d, a non-top View/Modal was closed, an old Procedure failed to close its View, or several Nodes handle the same back Action.

Use `Close()` / `TryGoBack()` and make the opener the owner. Scene UI clears on scene change; View and Modal remain by default. See [UI and Audio](../guides/ui-and-audio/index.md).

## The character still responds behind a Modal

Modal blocks only pointer input to lower Controls. It does not pause SceneTree or stop keyboard, gamepad, and `_UnhandledInput`. Change InputService Context and apply the game's pause policy, then restore both when closing.

## InputService has `IsReady == false`

Confirm GUIDE, GuideCs, and GoDoRuntime Autoload order; a one-time Boot `GuideInputBackendInstaller`; exact Action/Context IDs; and a completed Godot scan and build.

Do not install the backend again in Gameplay or menus. For stale Frame, axis mismatch, and Context Pop order, see [Input](../guides/input/index.md).

## BGM or SFX does not play

- BGM rejected: another BGM is loading; await it.
- SFX returned `false`: Voice capacity is full, not corrupt content.
- Loop never returns: it never finishes naturally; use `StopAllSfx()` or a game player.
- Spatial sound has no position: AudioService is non-spatial.
- Bus warning appears each launch: create BGM/SFX in the project Audio Bus Layout.

## A save cannot load or recovered from backup

NotFound is an empty slot, not an exception. RecoveredFromBackup means `.bak` was used; notify the player about possible recent progress loss and save later at a safe point.

Inspect slot, operation, and InnerException in `SaveException`. If both files are corrupt, do not delete automatically; offer retry, another slot, or confirmed deletion. The game Codec migrates or rejects unsupported versions.

## A setting cannot apply

- Resolution, window mode, or VSync is Unsupported: check `Supports` and hide the control.
- Values reset after restart: `Save()` was not called.
- Locale is rejected: translations were not loaded or it does not match AvailableLocales.
- Volume throws: it is not a finite 0–1 value.

## Text does not update after locale change

Godot can translate ordinary Controls automatically. Runtime-composed, cached, and non-Control text must refresh on `LocaleChangedEvent`.

Boxes indicate missing Theme font fallback. A key displayed verbatim indicates missing translation content or an omitted export Resource. RTL also requires layout, focus, icon, and custom-drawing validation.

## Procedure has no current flow after a change

When new `EnterAsync` fails, the old flow has exited and `Current` is null. Enter a RecoveryProcedure or safe title flow; there is no automatic rollback.

When old `ExitAsync` fails, `Current` remains the old flow and the new one is not entered. Use `RequestChange` inside Enter/Exit rather than recursive ChangeAsync. See [Procedure Recovery](../guides/procedure-recovery/index.md).

## Scheduler callback timing is unexpected

- Stops during pause: GameTime and UnscaledGameTime both honor SceneTree Pause; use RealTime.
- Slows with slow motion: default GameTime honors TimeScale.
- Fires once after a hitch: missed repeating periods are coalesced.
- Survives scene change: it has no Owner and no cancelled Handle/Token.
- `DelayAsync` cancels: Owner exit, Token, or shutdown caused normal cancellation.

## NodePool state is wrong after reuse

Godot `_Ready()` does not run again. Reset signals, Tweens, Scheduler work, collision, and external references in `OnAcquire` / `OnRelease`.

Release false usually means a duplicate return, wrong Pool, or external QueueFree. Active Nodes belong to their Pool.

## DataTable generation or export fails

- Run `check` and fix CSV encoding, types, keys, ranges, and foreign keys.
- After source changes, run `generate`, then `verify-generated`.
- Single-table mode needs a healthy full baseline; generate all after table-set or other-table changes.
- Use `godo_datatable_export.py` for release; direct export in the supported Godot 4.x version cannot reliably abort a bad package, so revalidate this limitation after an engine upgrade.

DataTable is experimental; regenerate and build after upgrading.

## If the cause is still unclear

Collect the minimum useful context: Godot/.NET and framework version, platform, full exception and InnerException, lifecycle stage, ResourceKey/slot/flow name, and stable reproduction steps. Inspect GoDo Debugger and Godot output, then query the exception and member in API Reference.

Never include save contents, access tokens, user paths, or other sensitive data.
