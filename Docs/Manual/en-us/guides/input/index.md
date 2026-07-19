---
translation_of: Docs/Manual/zh-cn/guides/input/index.md
translation_source_hash: sha256:bae88e40010d218c33ae81183b70a5a2fc30490dcdad04d7a0da754915b26521
---

# Read Semantic Input and Manage Contexts

InputService lets game code read semantic Actions such as move, jump, and confirm instead of depending on a spacebar, gamepad button, or third-party plugin type. It provides the current render-frame snapshot, a Context stack, the active device, and optional rebinding, persistence, and prompt-query interfaces.

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

After copying dependencies, let Godot finish scanning files and rebuilding its global script-class cache, then complete one C# build. Open:

```text
GoDo → GUIDE Input Setup...
```

Install or repair according to the checks. The normal Autoload order is:

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
input.PushContext(GameInput.PauseMenu, InputContextMode.Exclusive);

// Closing pause must match the top ID exactly.
input.PopContext(GameInput.PauseMenu);
```

`Exclusive` blocks lower Contexts; `Overlay` remains active with lower Contexts. A Context cannot be pushed twice, and an incorrect Pop throws `InputOperationException`.

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

        if (frame.JustPressed(GameInput.Jump))
            GD.Print("Jump requested");

        ApplyMovementIntent(move, delta);
    }

    private void ApplyMovementIntent(Vector2 move, double delta)
    {
        // Implement game-specific movement here.
    }
}
```

Obtain a new `InputFrame` each render frame. It is a lightweight handle to the current snapshot; reading it in a later frame throws a stale-Frame error.

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
- Context Pop failure: page closing order differs from Push order.
- Duplicate input: game code also reads GUIDE Actions directly and bypasses the GoDo snapshot.

For exact members, see <xref:GoDo.IInputService>, <xref:GoDo.InputFrame>, <xref:GoDo.InputContextMode>, <xref:GoDo.InputOperationException>, and <xref:GoDo.GuideInput.GuideInputProfile>.
