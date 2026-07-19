---
translation_of: Docs/Manual/zh-cn/getting-started/first-ui.md
translation_source_hash: sha256:676775c66394107a4128144a1c3a0f840bf00c9cd6924dc0778c8ae916c810b7
---

# Open a Main Menu and Confirmation Dialog

This tutorial continues from “Change to Your First Main Scene.” It opens an interactive main menu over `MainScene`. You will create Scene, View, and Modal interfaces and observe their different display layers and back behavior.

When finished, you can open a settings page, return to the main menu, display a quit confirmation, and cancel it. Each interface is a game-owned Godot `Control` scene. UiService only manages loading, layering, closing, and back order.

## What the three UI layers do

- `Scene`: UI associated with the current main content scene, such as a HUD, level prompt, or this tutorial's scene menu. It is cleared after a successful main-scene change.
- `View`: Full pages such as settings or inventory. A new View hides the previous View, which is restored when navigating back.
- `Modal`: Top-level dialogs such as confirmations. A Modal appears above other game UI and prevents mouse events from reaching Controls underneath it.

A Modal does not automatically pause the game or prevent game nodes from handling keyboard, gamepad, or `_UnhandledInput` events.

## Files after this tutorial

```text
res://
├─ WelcomeProcedure.cs
└─ UI/
   ├─ MainMenu.cs
   ├─ MainMenu.tscn
   ├─ SettingsView.cs
   ├─ SettingsView.tscn
   ├─ ConfirmQuitModal.cs
   └─ ConfirmQuitModal.tscn
```

## 1. Create the main menu

Create this scene tree:

```text
MainMenu (Control, with MainMenu.cs attached)
└─ Menu (VBoxContainer)
   ├─ Title (Label, text “Main Menu”)
   ├─ SettingsButton (Button, text “Settings”)
   └─ QuitButton (Button, text “Quit”)
```

Set the root to Full Rect, center `Menu`, and save the scene as `res://UI/MainMenu.tscn`.

Create `MainMenu.cs`:

```csharp
using System;
using Godot;
using GoDo;

public partial class MainMenu : Control
{
    private static readonly ResourceKey SettingsKey =
        ResourceKey.FromPath("res://UI/SettingsView.tscn");
    private static readonly ResourceKey ConfirmQuitKey =
        ResourceKey.FromPath("res://UI/ConfirmQuitModal.tscn");

    [Export] private Button? _settingsButton;
    [Export] private Button? _quitButton;

    private IUiService? _ui;

    public override void _Ready()
    {
        if (_settingsButton is null || _quitButton is null)
        {
            GD.PushError("MainMenu is missing button references.");
            return;
        }

        _ui = Services.Get<IUiService>();
        _settingsButton.Pressed += OnSettingsPressed;
        _quitButton.Pressed += OnQuitPressed;
    }

    public override void _ExitTree()
    {
        if (_settingsButton is not null)
            _settingsButton.Pressed -= OnSettingsPressed;
        if (_quitButton is not null)
            _quitButton.Pressed -= OnQuitPressed;
    }

    private void OnSettingsPressed() => Open(SettingsKey, UiLayer.View);

    private void OnQuitPressed() => Open(ConfirmQuitKey, UiLayer.Modal);

    private void Open(ResourceKey key, UiLayer layer)
    {
        try
        {
            _ui!.Open(key, layer);
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "MainMenu", key.Value);
        }
    }
}
```

Drag the two Buttons into the exported **Settings Button** and **Quit Button** properties. Named signal handlers allow the subscriptions to be removed symmetrically when the interface leaves the scene tree.

## 2. Create the Settings View

Create this scene:

```text
SettingsView (Control, with SettingsView.cs attached)
└─ Content (VBoxContainer)
   ├─ Title (Label, text “Settings”)
   └─ BackButton (Button, text “Back”)
```

The root must inherit `Control`. Set it to Full Rect, center the content, and save it as `res://UI/SettingsView.tscn`.

Create `SettingsView.cs`:

```csharp
using Godot;
using GoDo;

public partial class SettingsView : Control
{
    [Export] private Button? _backButton;

    private IUiService? _ui;

    public override void _Ready()
    {
        if (_backButton is null)
        {
            GD.PushError("SettingsView is missing its BackButton reference.");
            return;
        }

        _ui = Services.Get<IUiService>();
        _backButton.Pressed += OnBackPressed;
    }

    public override void _ExitTree()
    {
        if (_backButton is not null)
            _backButton.Pressed -= OnBackPressed;
    }

    private void OnBackPressed()
    {
        _ui!.TryGoBack();
    }
}
```

Assign `BackButton` to the exported **Back Button** property. The settings page opens as a View. `TryGoBack()` closes it and reveals the interface underneath.

## 3. Create the quit Modal

Create this scene:

```text
ConfirmQuitModal (PanelContainer, with ConfirmQuitModal.cs attached)
└─ Content (VBoxContainer)
   ├─ Message (Label, text “Quit the game?”)
   └─ Actions (HBoxContainer)
      ├─ ConfirmButton (Button, text “Confirm”)
      └─ CancelButton (Button, text “Cancel”)
```

`PanelContainer` inherits `Control`, so UiService can manage it as the root. Center the panel and save it as `res://UI/ConfirmQuitModal.tscn`.

Create `ConfirmQuitModal.cs`:

```csharp
using Godot;
using GoDo;

public partial class ConfirmQuitModal : PanelContainer
{
    [Export] private Button? _confirmButton;
    [Export] private Button? _cancelButton;

    private IUiService? _ui;

    public override void _Ready()
    {
        if (_confirmButton is null || _cancelButton is null)
        {
            GD.PushError("ConfirmQuitModal is missing button references.");
            return;
        }

        _ui = Services.Get<IUiService>();
        _confirmButton.Pressed += OnConfirmPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    public override void _ExitTree()
    {
        if (_confirmButton is not null)
            _confirmButton.Pressed -= OnConfirmPressed;
        if (_cancelButton is not null)
            _cancelButton.Pressed -= OnCancelPressed;
    }

    private void OnConfirmPressed()
    {
        GetTree().Quit();
    }

    private void OnCancelPressed()
    {
        _ui!.TryGoBack();
    }
}
```

Assign both exported button properties. On cancel, `TryGoBack()` closes the top Modal before it considers any View below it.

## 4. Open the main menu from the Procedure

In the previous tutorial's `WelcomeProcedure`, change the main content scene first and then open the UI. Replace it with:

```csharp
using System.Threading.Tasks;
using Godot;
using GoDo;

public sealed class WelcomeProcedure : IProcedure
{
    private static readonly ResourceKey MainSceneKey =
        ResourceKey.FromPath("res://Main/MainScene.tscn");
    private static readonly ResourceKey MainMenuKey =
        ResourceKey.FromPath("res://UI/MainMenu.tscn");

    private IUiService? _ui;
    private Control? _mainMenu;

    public string Name => "Welcome";

    public async Task EnterAsync(ProcedureContext context)
    {
        ISceneService scenes = context.GetService<ISceneService>();
        await scenes.ChangeAsync(MainSceneKey);

        _ui = context.GetService<IUiService>();
        _mainMenu = _ui.Open(MainMenuKey, UiLayer.Scene);
    }

    public Task ExitAsync(ProcedureContext context)
    {
        // This tutorial's flow exclusively owns the View and Modal back stack.
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
}
```

The scene change must finish before the Scene-layer menu opens. If the order is reversed, the successful scene-change event clears the menu that was just opened.

When the flow exits, it closes top Modals and Views before closing the Scene-layer main menu. The back stack belongs exclusively to this tutorial's flow. In a larger project, use an explicit UI coordination boundary to manage the interfaces it owns instead of clearing pages belonging to another system.

## 5. Run and verify the interaction

Verify these steps in order:

1. Startup displays the main content scene and “Main Menu.”
2. Select “Settings.” The settings page appears over the main menu.
3. Select “Back.” The settings page closes and the main menu returns.
4. Select “Quit.” The confirmation appears on top.
5. Select “Cancel.” Only the confirmation closes.
6. Open the confirmation again and select “Confirm.” The game exits.

In the Remote scene tree, game UI appears under the matching layers in `/root/GoDoUI`, not under `MainScene`. Do not call `QueueFree()` directly on managed interfaces. Use `Close()` or `TryGoBack()` so UiService can maintain its collections and back stack.

For exact members, see <xref:GoDo.IUiService>, <xref:GoDo.UiLayer>, and <xref:GoDo.UiOpenException>.
