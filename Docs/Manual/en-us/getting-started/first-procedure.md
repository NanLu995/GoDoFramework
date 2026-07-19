---
translation_of: Docs/Manual/zh-cn/getting-started/first-procedure.md
translation_source_hash: sha256:2864c5c78f7487837d3cc912bc729a501bad3998617efcaf8f75092be370b7e3
---

# Create Your First Game Flow

After installing the framework, this tutorial creates a game-owned startup scene and enters its first Procedure. When the project runs, the current flow name appears in the center of the game window and Godot's Output panel reports that the flow was entered.

A Procedure represents a top-level phase such as the main menu, gameplay, pause, or results. It organizes the overall game flow; it is not intended for per-frame states such as character movement or enemy AI.

## Files you will create

Create these files in the game project, outside `addons/godo_framework/`:

```text
res://
├─ Boot.cs
├─ Boot.tscn
└─ WelcomeProcedure.cs
```

## 1. Create the first Procedure

Create `WelcomeProcedure.cs`:

```csharp
using System.Threading.Tasks;
using Godot;
using GoDo;

public sealed class WelcomeProcedure : IProcedure
{
    public string Name => "Welcome";

    public Task EnterAsync(ProcedureContext context)
    {
        GD.Print("Entered the Welcome Procedure");
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context)
    {
        GD.Print("Exited the Welcome Procedure");
        return Task.CompletedTask;
    }
}
```

`EnterAsync` runs when the flow is entered, and `ExitAsync` runs before changing to the next flow. This first example only prints messages. Later, these methods can coordinate framework services for scenes, UI, audio, and saves.

## 2. Create the startup script

Create `Boot.cs`:

```csharp
using System;
using Godot;
using GoDo;

public partial class Boot : Control
{
    [Export] private Label? _statusLabel;

    public override async void _Ready()
    {
        if (_statusLabel is null)
        {
            GD.PushError("Boot is missing its StatusLabel reference.");
            return;
        }

        try
        {
            IProcedureService procedures = Services.Get<IProcedureService>();
            await procedures.ChangeAsync<WelcomeProcedure>();
            _statusLabel.Text = $"Current flow: {procedures.Current?.Name}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "Startup failed. Check Godot's Output panel.";
            ErrorHub.Report(exception, "GameBoot", nameof(_Ready));
        }
    }
}
```

GoDoRuntime registers framework services but does not choose the game's first flow. `Boot` is the game-owned entry point, so it explicitly enters `WelcomeProcedure` after its scene is ready.

The code awaits the change before updating the screen. It also displays and reports failures at the game startup boundary instead of leaving the player with a blank window.

## 3. Create the startup scene

Create this scene tree in Godot:

```text
Boot (Control, with Boot.cs attached)
└─ StatusLabel (Label)
```

Then configure it:

1. Set `Boot` to the Full Rect layout preset.
2. Place `StatusLabel` in the center of the window.
3. Drag the `StatusLabel` node into the **Status Label** property exported by `Boot.cs`.
4. Save the scene as `res://Boot.tscn` and make it the project main scene.

Do not create another GoDoRuntime in this scene. The only Runtime is the Autoload installed during the quick start.

## 4. Run and verify the result

Run the project. You should see all of the following:

- The game window displays “Current flow: Welcome”.
- Godot's Output panel displays “Entered the Welcome Procedure”.
- The Remote scene tree still contains exactly one `GoDoRuntime` Autoload.

If the game window reports a startup failure, check that:

- `GoDoRuntime` is present in the Autoload list.
- The exported **Status Label** property has been assigned.
- The project completed a C# Debug build after the framework was copied.

## The boundary established by this tutorial

The startup scene now does only one job: it enters the first game-owned flow. The Procedure decides what the current top-level phase should do. As you add a main menu and gameplay, create separate Procedures and connect them with flow changes instead of placing every phase in `Boot.cs`.

For exact members, see <xref:GoDo.IProcedure>, <xref:GoDo.IProcedureService>, and <xref:GoDo.ProcedureContext>.

The next guide will use SceneService to change to the first main content scene.
