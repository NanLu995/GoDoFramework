# 新增 Procedure

本文档说明如何新增一个顶层游戏流程，例如 `SettingsProcedure`、`PauseProcedure`、`LevelSelectProcedure`。

## 适用场景

使用 Procedure 表达全局阶段：

- 主菜单。
- 关卡选择。
- 加载。
- 游戏中。
- 暂停。
- 结算。

不要用 Procedure 表达角色 Idle / Run / Attack、AI 状态或技能阶段。

## 步骤

1. 在对应功能目录创建 Procedure。

```text
Settings/
└── SettingsProcedure.cs
```

2. 实现 `IProcedure`。

```csharp
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace MyGame;

public sealed class SettingsProcedure : IProcedure
{
    private Control? _view;
    private ProcedureContext? _context;

    public string Name => "Settings";

    public Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        _view = context.GetService<IUiService>().Open(GameKeys.SettingsView, UiLayer.View);
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context)
    {
        if (GodotObject.IsInstanceValid(_view))
            context.GetService<IUiService>().Close(_view!);

        _view = null;
        _context = null;
        return Task.CompletedTask;
    }
}
```

3. 在 `Shared/GameKeys.cs` 增加资源键。

```csharp
public static readonly ResourceKey SettingsView =
    ResourceKey.Create("res://Settings/SettingsView.tscn");
```

4. 从当前流程请求切换。

```csharp
context.RequestChange(new SettingsProcedure());
```

如果请求来自 UI，优先让 UI 发业务事件，当前 Procedure 监听后调用 `RequestChange`。

## 验证

- `dotnet build GoDoFramework.sln`
- 在 Godot 中运行入口场景，进入新流程。
- 验证重复点击不会导致并发切换。
- 验证离开流程后 UI、场景或事件监听被清理。

## 常见错误

- 在 `EnterAsync` 或 `ExitAsync` 里直接调用 `ChangeAsync` 递归切换，应使用 `RequestChange`。
- UI 直接强转 `IProcedureService.Current`。
- 退出流程时忘记关闭 View 或释放 `EventScope`。
- 把局部玩法状态写成 Procedure。
