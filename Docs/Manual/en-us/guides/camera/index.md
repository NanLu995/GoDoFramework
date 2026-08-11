---
translation_of: Docs/Manual/zh-cn/guides/camera/index.md
translation_source_hash: sha256:81397c01f0bcd484ac4a61a0bb092c5fbb7281a2b6dbd625cc8a609d2b96e233
---

# Configure, Switch, and Restore the Main Camera

CameraService manages the active main camera through stable game-owned IDs. Flow code says “switch to the Gameplay camera” or “restore the previous camera” without depending on Phantom Camera types. The concrete backend still owns following, damping, collision, and composition.

The core CameraService does not provide a Camera3D that can render a view. Use the optional Phantom Camera integration or implement a custom `CameraRig` adapter.

## When to use CameraService

Use it when:

- Gameplay, cutscene, aiming, and item-inspection cameras need explicit switching.
- A temporary shot should return to the latest camera that still exists.
- Procedures should not depend on a particular camera plugin.

It is not intended for:

- Minimap, monitor, or split-screen viewports rendered at the same time.
- Character movement, mouse input, or orbit rules.
- Replacing Godot `Camera3D` or Phantom Camera's following and collision features.

## 1. Install the optional Phantom Camera integration

The target project needs:

```text
addons/godo_framework/
addons/phantom_camera/
addons/godo_framework/Integrations/PhantomCamera/
```

Enable the single **GoDo Framework** plugin, then open:

```text
GoDo → 幻影相机配置 (Phantom Camera Settings)...
```

The setup window checks the third-party and adapter files, the third-party version, and its enabled state. The adapter is currently verified against Phantom Camera 0.11. The Enable Phantom Camera button is available only when all required files exist, the version is exactly 0.11, and the third-party plugin is still disabled. Confirmation only enables the installed third-party plugin; it does not modify scenes, runtime configuration, or third-party source.

Observable result: when all three checks pass, the window reports Correctly Configured and disables the enable button. If it reports Needs Attention, follow the hint to restore missing files, use the verified version, or inspect the editor output. A different version is not automatically unusable, but an upgrade requires a fresh build, automated regression, and validation in a real camera scene. Only the third-party Phantom Camera plugin is enabled in Godot's plugin list; the GoDo adapter is not a second EditorPlugin.

## 2. Add the third-person Rig to a scene

Instantiate this preset in the Gameplay 3D scene:

```text
res://addons/godo_framework/Integrations/PhantomCamera/ThirdPerson/GoDoPhantomThirdPersonRig.tscn
```

The preset contains:

```text
GoDoPhantomThirdPersonRig (PhantomCameraRig)
├─ MainCamera3D
│  └─ PhantomCameraHost
└─ ThirdPersonPcam
```

Complete these Inspector settings:

1. Set the root `RigId` to `camera/gameplay`.
2. Assign `Follow Target` on `ThirdPersonPcam`.
3. Configure distance, damping, collision layers, and `SpringArm3D` margin for the game.
4. Keep `ActivePriority` greater than `InactivePriority`; their defaults are 20 and 0.

When the Rig enters the scene tree, it validates the backend, writes the inactive priority, and registers itself. Do not add another registration script or simulate CameraService state by manually toggling `Camera3D`.

## 3. Define stable camera IDs

Create `res://Camera/GameCameraIds.cs`:

```csharp
using GoDo;

namespace MyGame;

public static class GameCameraIds
{
    public static readonly CameraId Gameplay =
        CameraId.Create("camera/gameplay");
    public static readonly CameraId Intro =
        CameraId.Create("camera/intro");
    public static readonly CameraId Inspect =
        CameraId.Create("camera/inspect");
}
```

IDs are case-sensitive and reject blank values or surrounding whitespace. Each scene `RigId` must exactly match its code ID. Use game meaning, not a node path, scene filename, or array position.

## 4. Activate the main camera from a Procedure

Activate after the Gameplay scene has loaded and its Rig has entered the tree:

```csharp
ICameraService cameras = context.GetService<ICameraService>();
cameras.ActivatePrimary(GameCameraIds.Gameplay);
```

Observable result: after a successful call, `ActivePrimary` equals `GameCameraIds.Gameplay`, the target Pcam uses `ActivePriority`, and the previous primary Rig uses `InactivePriority`. A backend read or write failure does not return normally: it throws `CameraOperationException` and preserves or rolls back the current camera according to the failed switch stage.

Switch when a cutscene starts:

```csharp
cameras.ActivatePrimary(GameCameraIds.Intro);
```

Activating the same concrete Rig again is a no-op and does not add history. `ActivePrimary` is the game-level logical state; it is not necessarily the same as Phantom Camera's internally selected candidate.

## 5. End a temporary shot and restore

When item inspection or a short cutscene ends:

```csharp
if (!cameras.RestorePreviousPrimary())
    cameras.ActivatePrimary(GameCameraIds.Gameplay);
```

`RestorePreviousPrimary()` skips historical instances that have left the scene tree. History identifies a concrete Rig instance, so an old scene entry is not incorrectly redirected to a same-ID Rig in a new scene.

The restore stack fits strictly nested temporary switches. When a long-running flow enters a new state, explicitly activate its base camera instead of assuming history contains the right target.

## 6. Understand switching failures

CameraService switches in this order:

1. Activate the target Rig; a failure preserves the current camera.
2. Deactivate the current Rig; a failure attempts to deactivate the new target as a rollback.
3. Update `ActivePrimary` and history only after both operations succeed.

The caller therefore does not receive a normal return with recorded and backend state only half switched. An unknown ID, invalid Rig, or backend activation/deactivation failure throws `CameraOperationException`; an invalid default `CameraId` throws `ArgumentException`. Handle these at the Procedure entry error boundary rather than silently ignoring them.

## 7. Write an adapter without Phantom Camera

If the project uses only Godot `Camera3D`, a thin `CameraRig` can connect it to the switching service:

```csharp
public sealed partial class GodotCameraRig : CameraRig
{
    [Export] public Camera3D BackendCamera { get; set; } = null!;

    protected override void OnRigReady()
    {
        if (!IsInstanceValid(BackendCamera))
            throw new InvalidOperationException("BackendCamera is not assigned.");
    }

    protected override void ActivateRig() => BackendCamera.MakeCurrent();

    protected override void DeactivateRig() => BackendCamera.ClearCurrent();
}
```

Attach the script to a scene node and assign `RigId` and `BackendCamera`. `CameraRig` registers automatically after `_Ready()` and unregisters the same instance when it leaves the tree; subclasses must not register it a second time. A more complex camera plugin uses the same boundary: validate backend references in `OnRigReady()`, then call only that plugin's switching API from the activate and deactivate methods.

## Scene-transition notes

Godot can put the new scene into the tree before freeing the old scene at the end of the frame. CameraService allows the same ID to exist briefly under different scene roots and resolves the latest registered valid Rig. A duplicate ID under the same scene root fails registration.

Use this order:

1. Complete the new scene transition through SceneService.
2. Let the new scene and CameraRig complete `_Ready()`.
3. Explicitly activate the new flow's base camera.
4. Do not access an old Rig or backend node that is about to be freed.

## Common failures

- Camera not found: the code ID differs from Inspector `RigId`, or activation ran before Rig `_Ready()`.
- Rig registration fails: the same scene root contains a duplicate ID.
- Phantom Rig initialization fails: `PhantomCameraNode` is missing or incompatible, or active priority is not greater than inactive priority.
- The inactive Pcam still renders: deactivation only lowers priority; Phantom Camera may still select it when it is the only candidate.
- Restore returns `false`: history is empty or every historical Rig has left the tree.
- Switching works but follow or collision does not: investigate Phantom Camera configuration or game orbit code, not CameraService switching.

For exact members, see <xref:GoDo.ICameraService>, <xref:GoDo.CameraId>, <xref:GoDo.CameraRig>, <xref:GoDo.CameraOperationException>, and <xref:GoDo.PhantomCameraRig>.
