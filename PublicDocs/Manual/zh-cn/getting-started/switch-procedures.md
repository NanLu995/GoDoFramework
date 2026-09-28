# 从主菜单进入游戏并返回

本教程把前面的主场景和 UI 连接成一个完整循环：启动进入主菜单，点击“开始游戏”切换到游戏流程，再从游戏界面返回主菜单。

UI 不会直接获取或强转当前 Procedure。按钮只通过 EventChannel 发布玩家意图，由当前 Procedure 决定是否以及何时切换流程。

## 完成后的结构

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

上一教程的 `WelcomeProcedure.cs` 将由职责更清楚的 `MainMenuProcedure.cs` 取代。

## 1. 定义业务事件

创建 `res://Shared/GameEvents.cs`：

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

事件名称表达“玩家已经提出了什么意图”，不代表切换一定成功。事件属于游戏业务，因此放在自己的 `MyGame` 命名空间，而不是 `GoDo.*`。

## 2. 让主菜单发送开始意图

在 `MainMenu.tscn` 的按钮列表中增加：

```text
StartButton (Button，文字为“开始游戏”)
```

在上一教程的 `MainMenu.cs` 中加入命名空间引用：

```csharp
using MyGame;
```

增加导出字段：

```csharp
[Export] private Button? _startButton;
```

将 `_startButton` 加入 `_Ready()` 的空引用检查，并订阅信号：

```csharp
_startButton.Pressed += OnStartPressed;
```

在 `_ExitTree()` 中对称解绑：

```csharp
if (_startButton is not null)
    _startButton.Pressed -= OnStartPressed;
```

最后增加处理方法：

```csharp
private void OnStartPressed()
{
    EventChannel.Emit<StartGameRequestedEvent>();
}
```

在检查器中把新增按钮拖到 **Start Button**。按钮不知道游戏流程类的名称，也不调用 `IProcedureService.ChangeAsync()`。

## 3. 创建主菜单流程

创建 `res://MainMenu/MainMenuProcedure.cs`：

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

        // 本入门示例的 View / Modal 返回栈由主菜单流程独占。
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

Procedure 是纯 C# 对象，没有 Node 的退出树生命周期，因此使用 `EventScope` 管理订阅。先成功完成场景和 UI 初始化，再创建订阅；退出时首先 `Dispose()`，防止旧流程继续响应按钮事件。

事件回调使用 `RequestChange<GameplayProcedure>()`。不要在当前 Procedure 的回调、`EnterAsync` 或 `ExitAsync` 中直接调用 `ChangeAsync()`，否则可能与正在执行的切换重入。

## 4. 创建游戏场景和 HUD

创建 `res://Gameplay/GameplayScene.tscn`：

```text
GameplayScene (Control)
└─ Message (Label，文字为“游戏进行中”)
```

根节点设为铺满矩形，将文字放在画面中央。

再创建 `res://Gameplay/GameplayHud.tscn`：

```text
GameplayHud (Control，挂载 GameplayHud.cs)
└─ ReturnButton (Button，文字为“返回主菜单”)
```

HUD 根节点同样设为铺满矩形。创建 `GameplayHud.cs`：

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
            GD.PushError("GameplayHud 缺少 ReturnButton 引用。");
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

把按钮拖到 **Return Button**。HUD 只表达玩家想返回，不决定返回到哪个具体流程。

## 5. 创建游戏流程

创建 `res://Gameplay/GameplayProcedure.cs`：

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

游戏流程进入后先切换主场景，再打开与该场景关联的 HUD。退出时先释放事件监听并关闭 HUD；随后主菜单流程再加载自己的场景和 UI。

## 6. 修改启动入口

将 `Boot.cs` 中的首次流程改为：

```csharp
IProcedureService procedures = Services.Get<IProcedureService>();
await procedures.ChangeAsync<MainMenuProcedure>();
```

成功后不要继续访问 `Boot` 节点，因为主菜单流程已经替换了启动场景。

## 7. 运行并验证循环

按顺序检查：

1. 启动后显示主菜单。
2. 点击“开始游戏”，主菜单和原主场景被清理。
3. 显示“游戏进行中”和“返回主菜单”按钮。
4. 点击“返回主菜单”，游戏 HUD 和游戏场景被清理。
5. 再次显示主菜单，并且按钮不会响应两次。

如果按钮触发两次，优先检查旧 Procedure 是否在 `ExitAsync` 中释放了 `EventScope`。如果切换失败，`RequestChange` 的处理错误会通过 ErrorHub 报告；不要在 UI 中静默忽略。

精确接口可查询 <xref:GoDo.EventChannel>、<xref:GoDo.EventScope>、<xref:GoDo.IEventMessage> 和 <xref:GoDo.ProcedureContext>。
