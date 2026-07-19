---
translation_of: Docs/Manual/zh-cn/getting-started/switch-procedures.md
translation_source_hash: sha256:30dd6c9754df3f592f0a1ed2b6a1161897ecf4a79d48971ccd277518f1c7ca77
---

# Enter Gameplay from the Main Menu and Return

This tutorial connects the earlier main scenes and UI into a complete loop: startup enters the main menu, “Start Game” changes to the gameplay flow, and the gameplay interface can return to the menu.

The UI does not obtain or cast the current Procedure. Buttons only publish player intent through EventChannel. The current Procedure decides whether and when to change flows.

## Files after this tutorial

```text
res://
├─ Boot.cs
├─ Shared/
│  └─ GameEvents.cs
├─ MainMenu/
│  └─ MainMenuProcedure.cs
├─ Gameplay/
│  ├─ GameplayProcedure.cs
│  ├─ GameplayScene.tscn
│  ├─ GameplayHud.cs
│  └─ GameplayHud.tscn
└─ UI/
   ├─ MainMenu.cs
   └─ MainMenu.tscn
```

The previous `WelcomeProcedure.cs` is replaced by the more clearly named `MainMenuProcedure.cs`.

## 1. Define game events

Create `res://Shared/GameEvents.cs`:

```csharp
using GoDo;

namespace MyGame;

public interface IGameEvent : IEventMessage
{
}

public readonly struct StartGameRequestedEvent : IGameEvent
{
}

public readonly struct ReturnToMenuRequestedEvent : IGameEvent
{
}
```

The event names describe intent that the player has already expressed; they do not promise that a flow change will succeed. These are game events, so they belong to the game's `MyGame` namespace instead of `GoDo.*`.

## 2. Publish start intent from the main menu

Add this button to the list in `MainMenu.tscn`:

```text
StartButton (Button, text “Start Game”)
```

Add the game namespace to the previous tutorial's `MainMenu.cs`:

```csharp
using MyGame;
```

Add an exported field:

```csharp
[Export] private Button? _startButton;
```

Include `_startButton` in the null check in `_Ready()`, then subscribe:

```csharp
_startButton.Pressed += OnStartPressed;
```

Unsubscribe symmetrically in `_ExitTree()`:

```csharp
if (_startButton is not null)
    _startButton.Pressed -= OnStartPressed;
```

Finally, add the handler:

```csharp
private void OnStartPressed()
{
    EventChannel.Emit<StartGameRequestedEvent>();
}
```

Assign the new Button to **Start Button** in the inspector. The button does not know a flow class name and does not call `IProcedureService.ChangeAsync()`.

## 3. Create the main-menu flow

Create `res://MainMenu/MainMenuProcedure.cs`:

```csharp
using System.Threading.Tasks;
using Godot;
using GoDo;
using MyGame;

public sealed class MainMenuProcedure : IProcedure
{
    private static readonly ResourceKey MainSceneKey =
        ResourceKey.FromPath("res://Main/MainScene.tscn");
    private static readonly ResourceKey MainMenuKey =
        ResourceKey.FromPath("res://UI/MainMenu.tscn");

    private EventScope? _events;
    private ProcedureContext? _context;
    private IUiService? _ui;
    private Control? _mainMenu;

    public string Name => "MainMenu";

    public async Task EnterAsync(ProcedureContext context)
    {
        ISceneService scenes = context.GetService<ISceneService>();
        await scenes.ChangeAsync(MainSceneKey);

        _ui = context.GetService<IUiService>();
        _mainMenu = _ui.Open(MainMenuKey, UiLayer.Scene);

        _context = context;
        _events = new EventScope()
            .On<StartGameRequestedEvent>(OnStartGameRequested);
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _events?.Dispose();
        _events = null;
        _context = null;

        // The main-menu flow exclusively owns the View and Modal stack here.
        while (_ui?.TryGoBack() == true)
        {
        }

        if (_ui is not null &&
            _mainMenu is not null &&
            GodotObject.IsInstanceValid(_mainMenu))
        {
            _ui.Close(_mainMenu);
        }

        _mainMenu = null;
        _ui = null;
        return Task.CompletedTask;
    }

    private void OnStartGameRequested(StartGameRequestedEvent _)
    {
        _context!.RequestChange<GameplayProcedure>();
    }
}
```

A Procedure is a plain C# object without a Node tree-exit lifecycle, so it uses `EventScope` to manage subscriptions. Create the subscription only after scene and UI initialization succeeds. Dispose it first during exit so the old flow can no longer react to button events.

The event handler uses `RequestChange<GameplayProcedure>()`. Do not call `ChangeAsync()` directly from a current Procedure callback, `EnterAsync`, or `ExitAsync`, because that can re-enter an active change.

## 4. Create the gameplay scene and HUD

Create `res://Gameplay/GameplayScene.tscn`:

```text
GameplayScene (Control)
└─ Message (Label, text “Gameplay running”)
```

Set the root to Full Rect and place the message in the center.

Then create `res://Gameplay/GameplayHud.tscn`:

```text
GameplayHud (Control, with GameplayHud.cs attached)
└─ ReturnButton (Button, text “Return to Main Menu”)
```

Set the HUD root to Full Rect. Create `GameplayHud.cs`:

```csharp
using Godot;
using GoDo;
using MyGame;

public partial class GameplayHud : Control
{
    [Export] private Button? _returnButton;

    public override void _Ready()
    {
        if (_returnButton is null)
        {
            GD.PushError("GameplayHud is missing its ReturnButton reference.");
            return;
        }

        _returnButton.Pressed += OnReturnPressed;
    }

    public override void _ExitTree()
    {
        if (_returnButton is not null)
            _returnButton.Pressed -= OnReturnPressed;
    }

    private void OnReturnPressed()
    {
        EventChannel.Emit<ReturnToMenuRequestedEvent>();
    }
}
```

Assign the Button to **Return Button**. The HUD only says that the player wants to return; it does not decide which concrete flow is the destination.

## 5. Create the gameplay flow

Create `res://Gameplay/GameplayProcedure.cs`:

```csharp
using System.Threading.Tasks;
using Godot;
using GoDo;
using MyGame;

public sealed class GameplayProcedure : IProcedure
{
    private static readonly ResourceKey GameplaySceneKey =
        ResourceKey.FromPath("res://Gameplay/GameplayScene.tscn");
    private static readonly ResourceKey GameplayHudKey =
        ResourceKey.FromPath("res://Gameplay/GameplayHud.tscn");

    private EventScope? _events;
    private ProcedureContext? _context;
    private IUiService? _ui;
    private Control? _hud;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        ISceneService scenes = context.GetService<ISceneService>();
        await scenes.ChangeAsync(GameplaySceneKey);

        _ui = context.GetService<IUiService>();
        _hud = _ui.Open(GameplayHudKey, UiLayer.Scene);

        _context = context;
        _events = new EventScope()
            .On<ReturnToMenuRequestedEvent>(OnReturnToMenuRequested);
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _events?.Dispose();
        _events = null;
        _context = null;

        if (_ui is not null &&
            _hud is not null &&
            GodotObject.IsInstanceValid(_hud))
        {
            _ui.Close(_hud);
        }

        _hud = null;
        _ui = null;
        return Task.CompletedTask;
    }

    private void OnReturnToMenuRequested(ReturnToMenuRequestedEvent _)
    {
        _context!.RequestChange<MainMenuProcedure>();
    }
}
```

The gameplay flow changes its main scene first, then opens the associated HUD. On exit it disposes event listeners and closes the HUD before the menu flow loads its own scene and UI.

## 6. Update the startup entry point

Change the first flow in `Boot.cs` to:

```csharp
IProcedureService procedures = Services.Get<IProcedureService>();
await procedures.ChangeAsync<MainMenuProcedure>();
```

Do not access `Boot` after success because the main-menu flow has replaced the startup scene.

## 7. Run and verify the loop

Check these steps in order:

1. Startup displays the main menu.
2. Select “Start Game.” The menu and previous main scene are cleared.
3. “Gameplay running” and “Return to Main Menu” appear.
4. Select “Return to Main Menu.” The gameplay HUD and scene are cleared.
5. The main menu appears again, and its button does not react twice.

If a button fires twice, first check whether the old Procedure disposes its `EventScope` in `ExitAsync`. If a requested change fails, the RequestChange processor reports the failure through ErrorHub; do not silently ignore it in the UI.

For exact members, see <xref:GoDo.EventChannel>, <xref:GoDo.EventScope>, <xref:GoDo.IEventMessage>, and <xref:GoDo.ProcedureContext>.
