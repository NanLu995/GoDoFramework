# 创建第一个游戏流程

完成框架安装后，本教程会创建一个由业务项目自己控制的启动场景，并进入第一个 Procedure。运行项目后，画面中央会显示当前流程名称，Godot 输出面板也会出现进入流程的消息。

Procedure 表示主菜单、游戏中、暂停或结算这类顶层阶段。它适合组织游戏的大流程，不用于角色移动、敌人 AI 等每帧状态。

## 完成后的文件

在业务项目中创建以下文件，不要放进 `addons/godo_framework/`：

```text
res://
├─ Boot.cs
├─ Boot.tscn
└─ WelcomeProcedure.cs
```

## 1. 创建第一个 Procedure

新建 `WelcomeProcedure.cs`：

```csharp
using System.Threading.Tasks;
using Godot;
using GoDo;

public sealed class WelcomeProcedure : IProcedure
{
    public string Name => "Welcome";

    public Task EnterAsync(ProcedureContext context)
    {
        GD.Print("已进入 Welcome Procedure");
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context)
    {
        GD.Print("已离开 Welcome Procedure");
        return Task.CompletedTask;
    }
}
```

`EnterAsync` 在流程进入时调用，`ExitAsync` 在切换到下一个流程前调用。示例暂时只输出消息；以后可以在这里协调场景、UI、音频和存档等框架服务。

## 2. 创建启动脚本

新建 `Boot.cs`：

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
            GD.PushError("Boot 缺少 StatusLabel 引用。");
            return;
        }

        try
        {
            IProcedureService procedures = Services.Get<IProcedureService>();
            await procedures.ChangeAsync<WelcomeProcedure>();
            _statusLabel.Text = $"当前流程：{procedures.Current?.Name}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "启动失败，请查看 Godot 输出面板。";
            ErrorHub.Report(exception, "GameBoot", nameof(_Ready));
        }
    }
}
```

GoDoRuntime 只负责注册框架服务，不会替游戏选择第一个流程。因此，`Boot` 是业务入口，它在场景准备好后显式进入 `WelcomeProcedure`。

这里保留 `await`，确保流程真正进入后才更新画面。异常在业务启动边界被显示并上报，避免启动失败后只留下空白画面。

## 3. 创建启动场景

在 Godot 中新建场景并设置以下节点树：

```text
Boot (Control，挂载 Boot.cs)
└─ StatusLabel (Label)
```

然后完成以下设置：

1. 将 `Boot` 的布局设为铺满矩形。
2. 将 `StatusLabel` 放在画面中央。
3. 在检查器中把 `StatusLabel` 节点拖到 `Boot.cs` 的 **Status Label** 属性。
4. 保存为 `res://Boot.tscn`，并设为项目主场景。

不要在这个场景中再次创建 GoDoRuntime。框架的唯一 Runtime 已经由快速开始中安装的 Autoload 提供。

## 4. 运行并确认结果

运行项目后，应同时看到：

- 游戏画面中央显示“当前流程：Welcome”。
- Godot 输出面板显示“已进入 Welcome Procedure”。
- Remote 场景树中仍只有一个 Autoload `GoDoRuntime`。

如果画面提示启动失败，先检查：

- `GoDoRuntime` 是否存在于 Autoload 列表。
- `Status Label` 导出属性是否已经赋值。
- 项目是否在复制框架后完成过一次 C# Debug 编译。

## 这一步建立了什么边界

现在，启动场景只负责进入第一个业务流程；Procedure 负责决定当前顶层阶段要做什么。以后增加主菜单和游戏场景时，继续创建新的 Procedure，并通过流程切换连接它们，不要把所有阶段都塞进 `Boot.cs`。

精确接口可查询 <xref:GoDo.IProcedure>、<xref:GoDo.IProcedureService> 和 <xref:GoDo.ProcedureContext>。

下一篇指南将使用 SceneService 切换第一个主内容场景。
