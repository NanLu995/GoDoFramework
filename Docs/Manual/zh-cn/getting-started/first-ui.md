# 打开主菜单与确认框

本教程接着“切换第一个主内容场景”，为 `MainScene` 打开一个可点击的主菜单。你将创建 Scene、View 和 Modal 三种界面，体验它们不同的显示层级和返回行为。

完成后可以打开设置页、返回主菜单、显示退出确认框，并取消确认框。所有界面都是业务项目自己的 Godot `Control` 场景，UiService 只负责加载、层级、关闭和返回顺序。

## 三种 UI 层分别做什么

- `Scene`：与当前主内容场景关联，例如 HUD、关卡提示或本教程的场景菜单；主场景成功切换后自动清理。
- `View`：设置、背包等完整页面；新 View 会隐藏前一个 View，返回时恢复。
- `Modal`：确认框等顶层弹窗；显示在其他游戏 UI 上方，并阻止鼠标事件落到下层 Control。

Modal 不会自动暂停游戏，也不会阻止业务节点处理键盘、手柄或 `_UnhandledInput`。

## 完成后的文件

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

## 1. 创建主菜单

新建场景并设置以下节点树：

```text
MainMenu (Control，挂载 MainMenu.cs)
└─ Menu (VBoxContainer)
   ├─ Title (Label，文字为“主菜单”)
   ├─ SettingsButton (Button，文字为“设置”)
   └─ QuitButton (Button，文字为“退出”)
```

将根节点设为铺满矩形，把 `Menu` 放在画面中央，保存为 `res://UI/MainMenu.tscn`。

创建 `MainMenu.cs`：

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
            GD.PushError("MainMenu 缺少按钮引用。");
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

在检查器中把两个 Button 分别拖到 **Settings Button** 和 **Quit Button**。使用具名方法订阅信号，确保界面退出场景树时能够对称解绑。

## 2. 创建设置 View

新建场景：

```text
SettingsView (Control，挂载 SettingsView.cs)
└─ Content (VBoxContainer)
   ├─ Title (Label，文字为“设置”)
   └─ BackButton (Button，文字为“返回”)
```

根节点必须是 `Control`，设置为铺满矩形。把内容放在画面中央，保存为 `res://UI/SettingsView.tscn`。

创建 `SettingsView.cs`：

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
            GD.PushError("SettingsView 缺少 BackButton 引用。");
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

将 `BackButton` 拖到导出的 **Back Button** 属性。设置页作为 View 打开；`TryGoBack()` 会关闭它，随后重新显示被覆盖的界面。

## 3. 创建退出 Modal

新建场景：

```text
ConfirmQuitModal (PanelContainer，挂载 ConfirmQuitModal.cs)
└─ Content (VBoxContainer)
   ├─ Message (Label，文字为“确定退出游戏吗？”)
   └─ Actions (HBoxContainer)
      ├─ ConfirmButton (Button，文字为“确定”)
      └─ CancelButton (Button，文字为“取消”)
```

`PanelContainer` 继承自 `Control`，可以作为 UiService 管理的根节点。把面板放在画面中央，保存为 `res://UI/ConfirmQuitModal.tscn`。

创建 `ConfirmQuitModal.cs`：

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
            GD.PushError("ConfirmQuitModal 缺少按钮引用。");
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

把两个按钮拖到对应导出属性。取消时，`TryGoBack()` 会优先关闭最上层 Modal，而不会关闭下面的 View。

## 4. 从 Procedure 打开主菜单

在上一教程的 `WelcomeProcedure` 中，先切换主内容场景，再打开 UI。将它修改为：

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
        // 本教程的流程独占 View / Modal 返回栈，先按层级逐一关闭。
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

必须先完成场景切换，再打开 Scene 层菜单。顺序反过来时，场景提交事件会把刚打开的 Scene 层 UI 清理掉。

退出流程时先关闭顶部 Modal 和 View，再关闭 Scene 层主菜单。本教程的 UI 返回栈只属于这个流程；大型项目应由明确的 UI 协调边界管理自己打开的界面，不要随意清空其他系统的页面。

## 5. 运行并验证交互

依次验证：

1. 启动后显示主内容场景和“主菜单”。
2. 点击“设置”，设置页显示，主菜单仍在下层。
3. 点击“返回”，设置页关闭并回到主菜单。
4. 点击“退出”，确认框显示在最上层。
5. 点击“取消”，只关闭确认框。
6. 再次打开确认框并点击“确定”，游戏退出。

在 Remote 场景树中，业务 UI 位于 `/root/GoDoUI` 的对应层，而不是 `MainScene` 子节点。不要对这些受管理界面直接调用 `QueueFree()`；使用 `Close()` 或 `TryGoBack()`，否则会破坏 UiService 的集合与返回栈。

精确接口可查询 <xref:GoDo.IUiService>、<xref:GoDo.UiLayer> 和 <xref:GoDo.UiOpenException>。
