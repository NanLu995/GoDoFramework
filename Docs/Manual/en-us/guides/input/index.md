---
translation_of: Docs/Manual/zh-cn/guides/input/index.md
translation_source_hash: sha256:a1d32b42eaa842158e6c9fa04b848b06fe5a80f21a849d249e202d434b5da9cc
---

# Read Semantic Input and Manage Contexts

InputService lets game code read semantic Actions such as move, jump, and confirm instead of depending on a spacebar, gamepad button, or third-party plugin type. It provides the current render-frame snapshot, disposable Context ownership, the active device, and optional rebinding, persistence, and prompt-query interfaces. InputActionRouter handles prioritized discrete commands.

The core InputService does not include a key-mapping backend. Without a backend, `IsReady` is `false`, and reading a Frame or changing Contexts fails explicitly. The framework currently provides an optional G.U.I.D.E-CSharp adapter.

## When to use InputService

Use it when:

- Keyboard, mouse, gamepad, and touch should produce the same gameplay actions.
- Gameplay, menus, pause, and dialog require different active input sets.
- Game code should survive a future input-plugin replacement.

It is not intended for:

- Local multiplayer device assignment.
- Fighting-game command history, rhythm timing, or network prediction.
- Concrete movement speed, camera rotation, or other gameplay rules.

## 1. Install the optional GUIDE backend

The target project needs these directories:

```text
addons/godo_framework/
addons/guideCS/
addons/godo_framework/Integrations/GuideInput/
```

The verified combination is GUIDE `0.13.0` with GUIDE-CSharp `0.3.7--0.14.0`. Obtain it from the [official GitHub source](https://github.com/Phlegmlee/G.U.I.D.E-CSharp), ensure the resulting path is `addons/guideCS/`, and pin the actual commit so later source updates do not make the dependency unreproducible. **Open GitHub source...** only opens that page in the system browser; it never downloads, extracts, or overwrites third-party files.

This combination passes GoDo's functional regression, but GUIDE-CSharp currently uses `Activator.CreateInstance` to create generic wrappers and produces `IL2087` under strict Native AOT/Trimming analysis. Do not treat the GUIDE integration as validated for iOS Native AOT. Omitting the optional integration does not affect the GoDo core runtime.

After copying dependencies, let Godot finish scanning files and rebuilding its global script-class cache, then complete one C# build. Open:

```text
GoDo Framework → Open GoDo Framework... → Editor Extensions → GUIDE Input
```

The status report, hint, and actions appear directly on the right-hand page without a second setup window. **Recheck** reports completion in the hint area. Install or repair according to the checks. The normal Autoload order is:

```text
GUIDE
GuideCs
GoDoRuntime
```

Do not edit third-party source or copy the framework workbench's `project.godot`. The setup tool enables missing plugins and adjusts necessary Autoloads only after confirmation. A healthy repeated check performs no writes.

If the first scan briefly reports a missing `GUIDEActionMapping`, wait for scanning to finish, restart the editor, and rebuild. Before export, require a clean setup check and one error-free editor startup.

## 2. Define stable game-owned IDs

Create `res://Input/GameInput.cs`:

```csharp
using GoDo;

namespace MyGame;

public static class GameInput
{
    public static readonly InputActionId Move =
        InputActionId.Create("gameplay.move");
    public static readonly InputActionId Jump =
        InputActionId.Create("gameplay.jump");
    public static readonly InputActionId Confirm =
        InputActionId.Create("ui.confirm");

    public static readonly InputContextId Gameplay =
        InputContextId.Create("gameplay");
    public static readonly InputContextId MainMenu =
        InputContextId.Create("main_menu");
    public static readonly InputContextId PauseMenu =
        InputContextId.Create("pause_menu");
}
```

IDs are case-sensitive and reject blank values or surrounding whitespace. They are stable game contracts; do not generate them from key labels, resource paths, or array positions.

## 3. Create a GUIDE Profile

First use the G.U.I.D.E editor to create matching Action and Mapping Context resources:

- `gameplay.move` maps to an Axis2D Action.
- `gameplay.jump` and `ui.confirm` map to Bool Actions.
- Gameplay, MainMenu, and PauseMenu each use their own Mapping Context.

Then create a `GuideInputProfile` Resource in the inspector:

1. Under **Actions**, enter each GoDo Action ID and assign its GUIDE Action.
2. Under **Contexts**, enter each GoDo Context ID and assign its GUIDE Mapping Context.
3. If runtime rebinding is needed, register stable Binding IDs and rebindable slots under **Bindings**.

IDs, GUIDE Resources, and rebindable targets must not be duplicated. The Bool, Axis1D, Axis2D, or Axis3D type of each Action is fixed after backend installation.

## 4. Install the backend from a one-time startup scene

Add the Installer as a child of `Boot`:

```text
Boot
└─ GuideInputBackendInstaller
   ├─ Profile = res://Input/GameInputProfile.tres
   └─ PersistenceSlot = godo-input-bindings
```

Godot calls child `_Ready()` methods first, so the Installer runs before `Boot._Ready()`. GoDoRuntime owns the installed backend afterward, and replacing the Boot scene does not uninstall it.

The Installer belongs only in a one-time startup scene. Do not add it to Gameplay, levels, or menus. A process can install only one backend.

If the backend supports binding persistence, load it before the first Procedure:

```csharp
IInputService input = Services.Get<IInputService>();
if (!input.IsReady)
    throw new InvalidOperationException("The input backend was not installed.");

if (input.TryGetRebindingPersistence(
        out IInputRebindingPersistence? persistence))
{
    InputBindingLoadStatus status = persistence.LoadAndApply();
    if (status == InputBindingLoadStatus.RecoveredFromBackup)
        ErrorHub.Warn("Input bindings were recovered from backup.", "GameBoot");
}
```

No saved configuration applies default bindings. Disk or Codec failures use SaveService's `SaveException` and should be reported by the Boot startup boundary.

## 5. Set the base Context from Procedures

In `MainMenuProcedure.EnterAsync()`:

```csharp
IInputService input = context.GetService<IInputService>();
input.SetBaseContext(GameInput.MainMenu);
```

In `GameplayProcedure.EnterAsync()`:

```csharp
IInputService input = context.GetService<IInputService>();
input.SetBaseContext(GameInput.Gameplay);
```

`SetBaseContext()` clears every temporary Context, which makes it suitable for top-level flow changes. Do not let a character script and several UI pages compete to set the base Context.

A pause menu can temporarily suppress Gameplay:

```csharp
using InputContextLease pauseContext =
    input.PushContextScoped(GameInput.PauseMenu, InputContextMode.Exclusive);
```

`Exclusive` blocks lower Contexts; `Overlay` remains active with lower Contexts. A Context cannot be pushed twice. A lease disposes only the Context it owns even when pages close out of order; `SetBaseContext()` or service shutdown safely invalidates older leases. Legacy `PushContext / PopContext` remains for one migration cycle, still requires strict top-of-stack pairing, and cannot pop a lease-owned entry.

## 6. Read the current frame from a gameplay Node

Example controller:

```csharp
using Godot;
using GoDo;
using MyGame;

public partial class PlayerController : Node
{
    private IInputService? _input;

    public override void _Ready()
    {
        _input = Services.Get<IInputService>();
    }

    public override void _Process(double delta)
    {
        if (_input?.IsReady != true)
            return;

        InputFrame frame = _input.Frame;
        Vector2 move = frame.Axis2(GameInput.Move);

        ApplyMovementIntent(move, delta);
    }

    private void ApplyMovementIntent(Vector2 move, double delta)
    {
        // Implement game-specific movement here.
    }
}
```

Obtain a new `InputFrame` each render frame. It is a lightweight handle to the current snapshot; reading it in a later frame throws a stale-Frame error.

Copy a value snapshot when one Action's complete state must be retained:

```csharp
InputActionFrameState jump = frame.GetState(GameInput.Jump);
```

It contains Idle/Ongoing/Performed status, all Started/Performed/Completed/Cancelled transitions accumulated in this sample window, elapsed time, `[0,1]` progress, and sample sequence. The value can be retained across frames. Continue reading continuous movement and look directly from the Frame.

Use the Router for discrete menu confirmation, pause, and interaction commands:

```csharp
IInputActionRouter router = Services.Get<IInputActionRouter>();
using InputRouteScope scope = router.PushScope("PauseMenu");
using InputRouteBinding binding = scope.Bind(
    GameInput.Confirm,
    InputActionTransitions.Performed,
    (state, matched) =>
    {
        ConfirmSelection();
        return InputRouteResult.Handled;
    });
```

Newer scopes have priority. `Handled` stops only the current Action from reaching lower scopes; `Pass` continues. After a Context or route change, a bound Action that has not returned to Idle is gated until it is released, preventing a held confirmation from immediately retriggering in the newly opened page. The Router is the input-dispatch boundary; do not also broadcast the same player command through the global EventChannel.

For a controller driven by `_PhysicsProcess()`, cache continuous axes and latch `JustPressed` in `_Process()`, then consume them from physics frames. This prevents mismatched render and physics rates from losing one-shot input.

## 7. Display prompts for the active device

A backend with prompt-query support can provide fallback binding text:

```csharp
if (input.ActiveDevice != InputDeviceKind.Unknown &&
    input.TryGetPromptQuery(out IInputPromptQuery? prompts))
{
    IReadOnlyList<InputPromptInfo> jumpPrompts = prompts.GetPrompts(
        GameInput.Gameplay,
        GameInput.Jump,
        input.ActiveDevice);
}
```

Refresh prompts at low frequency after `InputDeviceChangedEvent` or `InputBindingsChangedEvent`, not every frame. `DisplayText` is only a text fallback; keycap icons, controller branding, localization, and layout remain game UI responsibilities.

## Common failures

- `IsReady == false`: the Installer did not run, the Profile is invalid, or GUIDE/GuideCs Autoloads are incomplete.
- Unknown Action: code IDs and Profile IDs differ.
- Axis type mismatch: code calls `Axis2()`, but the GUIDE Action is not Axis2D.
- Stale Frame: a previous frame's `InputFrame` was stored and read later.
- Context Pop failure: use leases in new code; legacy API closing order must match Push order.
- Duplicate input: game code also reads GUIDE Actions directly and bypasses the GoDo snapshot.

For exact members, see <xref:GoDo.IInputService>, <xref:GoDo.IInputActionRouter>, <xref:GoDo.InputFrame>, <xref:GoDo.InputActionFrameState>, <xref:GoDo.InputContextLease>, <xref:GoDo.InputOperationException>, and <xref:GoDo.GuideInput.GuideInputProfile>.

## Capability map

<div class="godo-capability-list">
<section><h4>Read readiness, frame, device, and capabilities</h4><p>Consume Frame only in its current frame.</p><pre class="godo-capability-call"><code>input.IsReady
input.Frame
input.ActiveDevice
input.Capabilities</code></pre></section>
<section><h4>Set the base Context</h4><pre class="godo-capability-call"><code>input.SetBaseContext(GameInputContexts.Gameplay);</code></pre></section>
<section><h4>Own and query Contexts</h4><pre class="godo-capability-call"><code>using InputContextLease menu = input.PushContextScoped(GameInputContexts.Menu, InputContextMode.Exclusive);
input.IsContextActive(GameInputContexts.Menu);
menu.Dispose();</code></pre></section>
<section><h4>Route discrete transitions</h4><pre class="godo-capability-call"><code>using InputRouteScope scope = router.PushScope("Menu");
using InputRouteBinding binding = scope.Bind(action, InputActionTransitions.Performed, handler);</code></pre></section>
<section><h4>Obtain optional input extensions</h4><pre class="godo-capability-call"><code>input.TryGetRebinding(out IInputRebinding rebinding)
input.TryGetRebindingPersistence(out IInputRebindingPersistence persistence)
input.TryGetPromptQuery(out IInputPromptQuery prompts)</code></pre></section>
</div>
