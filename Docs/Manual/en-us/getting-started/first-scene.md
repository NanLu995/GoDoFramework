---
translation_of: Docs/Manual/zh-cn/getting-started/first-scene.md
translation_source_hash: sha256:126f6a0d14f8186d449fc7d23ad07a74a9e24158a98b1f2b721c821139562c95
---

# Change to Your First Main Scene

This tutorial continues from “Create Your First Game Flow.” It uses SceneService from `WelcomeProcedure` to load and change to a new main content scene. When the project runs, the original `Boot` scene is replaced and the game window displays “Main content scene loaded.”

SceneService only manages `SceneTree.CurrentScene`. Menu dialogs, HUDs, and pause screens are UI and should not be implemented as main-scene changes.

## Files after this tutorial

Add a `Main` directory to the files from the previous tutorial:

```text
res://
├─ Boot.cs
├─ Boot.tscn
├─ WelcomeProcedure.cs
└─ Main/
   └─ MainScene.tscn
```

## 1. Create the target scene

Create this scene tree in Godot:

```text
MainScene (Control)
└─ Message (Label)
```

Set `MainScene` to the Full Rect layout preset, place `Message` in the center, and set its text to:

```text
Main content scene loaded
```

Save the scene as `res://Main/MainScene.tscn`. Do not make it the project main scene; the project still starts from `Boot.tscn`.

## 2. Change scenes from the Procedure

Replace `WelcomeProcedure.cs` with:

```csharp
using System.Threading.Tasks;
using GoDo;

public sealed class WelcomeProcedure : IProcedure
{
    private static readonly ResourceKey MainSceneKey =
        ResourceKey.FromPath("res://Main/MainScene.tscn");

    public string Name => "Welcome";

    public async Task EnterAsync(ProcedureContext context)
    {
        ISceneService scenes = context.GetService<ISceneService>();
        await scenes.ChangeAsync(MainSceneKey);
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
}
```

`ResourceKey` first verifies that the locator is a normalized `res://` or `uid://` resource address. SceneService then loads the `PackedScene` asynchronously. It only sets the new CurrentScene after the resource has been instantiated and added to the scene tree successfully.

This tutorial uses a path so the complete process remains visible. As the project grows, a ResourceManifest can provide stable game-owned IDs for resources.

## 3. Update the startup code

The previous tutorial updates a label in `Boot` after entering the Procedure. The Procedure now replaces the entire main scene, so a successful change queues the old `Boot` scene for deletion. Its nodes must not be accessed afterward.

Replace `Boot.cs` with:

```csharp
using System;
using Godot;
using GoDo;

public partial class Boot : Control
{
    public override async void _Ready()
    {
        try
        {
            IProcedureService procedures = Services.Get<IProcedureService>();
            await procedures.ChangeAsync<WelcomeProcedure>();

            // Boot is being freed after a successful change. Do not access it here.
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "GameBoot", nameof(_Ready));
        }
    }
}
```

You may delete the previous `StatusLabel` from `Boot.tscn` or keep it as a loading message. In either case, do not modify it after the successful `await`.

## 4. Run and verify the result

Run the project and confirm that:

- The game window displays “Main content scene loaded.”
- The Remote scene tree shows `MainScene` as CurrentScene, and the old `Boot` has been freed.
- `GoDoRuntime` remains the only Autoload and was not freed with the main scene.

You can also deliberately change the path to `res://Main/Missing.tscn` and run once more. You should see that:

- Godot's Output panel contains the startup error reported through ErrorHub.
- The target scene was not committed, and the old `Boot` scene remains active.

Restore the path to `res://Main/MainScene.tscn` after this check.

## Common problems

### The resource cannot be found

Confirm that the directory and filename casing exactly match the `ResourceKey`. On case-sensitive export platforms, `MainScene.tscn` and `mainscene.tscn` are different paths.

### A scene change is already in progress

Only one `ChangeAsync` can run at a time. Do not call it from `_Process`, and do not let several buttons start independent changes. Let the current Procedure serialize top-level changes.

### Accessing a freed object after the change

Check for code that accesses old scene nodes after `await scenes.ChangeAsync(...)` returns. To work with the new scene, use the `Node` returned by `ChangeAsync`, or let the new scene initialize itself in `_Ready()`.

For exact members, see <xref:GoDo.ISceneService>, <xref:GoDo.ResourceKey>, and <xref:GoDo.SceneChangeException>.

The next step is to build the main menu as UI and let the Procedure coordinate the main scene and interface instead of adding game logic to `Boot.cs`.
