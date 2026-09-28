# 新增 UI View

> 文档类型：工作流
> 状态：当前
> 权威范围：新增由 UiService 管理的业务 UI 的操作步骤
> 最近核对：2026-09-28
> 相关实现与契约：`addons/godo_framework/Runtime/UI/USAGE.md`

本文档说明如何新增一个由 UiService 管理的 UI。

## 选择 UI 层

- `UiLayer.Scene`：HUD、准星、关卡提示，随主内容场景切换清理。
- `UiLayer.View`：菜单、背包、设置、结算页，进入返回栈。
- `UiLayer.Modal`：确认框、阻挡点击的弹窗。
- `UiLayer.Overlay`：Toast、加载提示、引导遮罩，不进入返回栈。

## 步骤

1. 在功能目录创建 `.tscn`，根节点必须继承 `Control`。

```text
Settings/
├── SettingsView.tscn
└── SettingsView.cs
```

2. 使用 `[Export] NodePath` 绑定节点引用。

```csharp
using System;
using Godot;
using GoDo;

#nullable enable

namespace MyGame;

public sealed partial class SettingsView : Control
{
    private Button? _closeButton;

    [Export] public NodePath CloseButtonPath { get; set; } = null!;

    public override void _Ready()
    {
        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        if (!IsInstanceValid(_closeButton))
            throw new InvalidOperationException("SettingsView 缺少关闭按钮。");

        _closeButton.Pressed += OnClosePressed;
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_closeButton))
            _closeButton.Pressed -= OnClosePressed;

        _closeButton = null;
    }

    private void OnClosePressed()
    {
        EventChannel.Emit(new SettingsCloseSelectedEvent());
    }
}
```

3. 定义业务事件。

```csharp
public readonly struct SettingsCloseSelectedEvent : IGameEvent { }
```

4. 当前 Procedure 监听事件并决定行为。

```csharp
private ProcedureContext? _context;

public Task EnterAsync(ProcedureContext context)
{
    _context = context;
    UiScope<SettingsView> view = context.GetService<IUiService>()
        .OpenScoped<SettingsView>(GameKeys.SettingsView, UiLayer.View);
    context.RegisterCleanup(view);
    context.Events.On<SettingsCloseSelectedEvent>(OnCloseSelected);
    return Task.CompletedTask;
}

public Task ExitAsync(ProcedureContext context)
{
    _context = null;
    return Task.CompletedTask;
}

private void OnCloseSelected(SettingsCloseSelectedEvent _)
{
    _context?.RequestChange(new MainMenuProcedure());
}
```

## 验证

- `dotnet build GoDoFramework.sln`
- 在 Godot 中打开对应流程。
- 检查 UI 根节点是 `Control`。
- 检查按钮 Signal 在 `_ExitTree` 对称解绑。
- 检查 View / Modal 由 `UiScope` 清理，或关闭路径经过 `IUiService.Close` / `TryGoBack`。

## 常见错误

- UI 根节点使用 `Node2D` 或 `Node`，导致 UiService 打开失败。
- 直接 `QueueFree()` 受管理 UI。
- 用匿名 lambda 订阅需要解绑的 Signal。
- UI 内部直接修改流程状态，而不是发布玩家意图。
