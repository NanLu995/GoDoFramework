# 读取语义输入与管理 Context

InputService 让业务代码读取“移动、跳跃、确认”这类语义 Action，而不是依赖空格键、手柄按钮或某个第三方插件类型。它提供当前渲染帧快照、Context 栈、活动设备和可选的改键、持久化与提示查询接口。

核心 InputService 不自带按键映射后端。未安装后端时 `IsReady` 为 `false`，读取 Frame 或切换 Context 会明确失败。当前框架提供可选的 G.U.I.D.E-CSharp 适配。

## 什么时候使用 InputService

适合：

- 键鼠、手柄和触摸需要汇总为相同玩法动作。
- Gameplay、菜单、暂停和对话需要启用不同输入集合。
- 业务代码希望在更换输入插件后保持不变。

不适合：

- 本地多玩家设备分配。
- 格斗指令历史、节奏判定或网络预测。
- 角色移动速度、摄像机旋转等具体玩法规则。

## 1. 安装可选 GUIDE 后端

目标项目需要以下目录：

```text
addons/godo_framework/
addons/guideCS/
addons/godo_framework/Integrations/GuideInput/
```

复制依赖后先让 Godot 完成文件扫描和全局脚本类型缓存，再完成一次 C# 编译。随后打开：

```text
GoDo → GUIDE Input 设置...
```

按照检查结果安装或修复。正常 Autoload 顺序为：

```text
GUIDE
GuideCs
GoDoRuntime
```

不要手工修改第三方源码，也不要照抄框架工作台的 `project.godot`。设置工具只在用户确认后启用缺失插件并调整必要的 Autoload；全部检查通过时不会重复写入。

如果首次打开短暂提示找不到 `GUIDEActionMapping`，先等待文件扫描完成，重启编辑器并重新编译。导出前必须确认设置工具无错误，并完成一次无错误的编辑器启动。

## 2. 定义稳定的业务 ID

创建 `res://Input/GameInput.cs`：

```csharp
using GoDo;

namespace MyGame;

public static class GameInput
{
    public static readonly InputActionId Move =
        InputActionId.Create("gameplay.move");
    public static readonly InputActionId Jump =
        InputActionId.Create("gameplay.jump");
    public static readonly InputActionId Confirm =
        InputActionId.Create("ui.confirm");

    public static readonly InputContextId Gameplay =
        InputContextId.Create("gameplay");
    public static readonly InputContextId MainMenu =
        InputContextId.Create("main_menu");
    public static readonly InputContextId PauseMenu =
        InputContextId.Create("pause_menu");
}
```

ID 区分大小写，不能是空白或包含首尾空格。它们是游戏业务的稳定协议，不使用按键文字、资源路径或数组下标临时生成。

## 3. 创建 GUIDE Profile

先使用 G.U.I.D.E 编辑器创建对应的 Action 和 Mapping Context Resource：

- `gameplay.move` 对应 Axis2D Action。
- `gameplay.jump` 与 `ui.confirm` 对应 Bool Action。
- Gameplay、MainMenu 和 PauseMenu 分别使用自己的 Mapping Context。

然后在 Inspector 中创建 `GuideInputProfile` Resource：

1. 在 **Actions** 中填写 GoDo Action ID，并拖入对应 GUIDE Action。
2. 在 **Contexts** 中填写 GoDo Context ID，并拖入对应 GUIDE Mapping Context。
3. 需要运行时改键时，再在 **Bindings** 中登记稳定 Binding ID 和可重绑定槽位。

相同 ID、相同 GUIDE Resource 或相同可重绑定目标不能重复。Action 的 Bool、Axis1D、Axis2D、Axis3D 类型在后端安装后固定，不能运行中改变。

## 4. 在一次性启动场景安装后端

把 Installer 添加为 `Boot` 的子节点：

```text
Boot
└─ GuideInputBackendInstaller
   ├─ Profile = res://Input/GameInputProfile.tres
   └─ PersistenceSlot = godo-input-bindings
```

Godot 会先调用子节点的 `_Ready()`，因此 Installer 会在 `Boot._Ready()` 之前安装后端。后端随后由 GoDoRuntime 长期持有，即使 Boot 场景被替换也不会卸载。

Installer 只能存在于进入一次的启动场景。不要放在 Gameplay、关卡或菜单中；每个进程只允许安装一个后端。

如果后端支持绑定持久化，可以在 Boot 启动首个 Procedure 前加载：

```csharp
IInputService input = Services.Get<IInputService>();
if (!input.IsReady)
    throw new InvalidOperationException("输入后端没有完成安装。");

if (input.TryGetRebindingPersistence(
        out IInputRebindingPersistence? persistence))
{
    InputBindingLoadStatus status = persistence.LoadAndApply();
    if (status == InputBindingLoadStatus.RecoveredFromBackup)
        ErrorHub.Warn("输入绑定已从备份恢复。", "GameBoot");
}
```

没有保存配置时会应用默认绑定。磁盘或 Codec 失败会沿用 SaveService 的 `SaveException`；应由 Boot 的启动错误边界报告。

## 5. 由 Procedure 设置基础 Context

在 `MainMenuProcedure.EnterAsync()` 中：

```csharp
IInputService input = context.GetService<IInputService>();
input.SetBaseContext(GameInput.MainMenu);
```

在 `GameplayProcedure.EnterAsync()` 中：

```csharp
IInputService input = context.GetService<IInputService>();
input.SetBaseContext(GameInput.Gameplay);
```

`SetBaseContext()` 会清除所有临时 Context，因此适合顶层流程切换。不要让角色脚本和多个 UI 页面互相争抢基础 Context。

暂停菜单可以临时屏蔽 Gameplay：

```csharp
input.PushContext(GameInput.PauseMenu, InputContextMode.Exclusive);

// 关闭暂停菜单时，必须与栈顶 ID 严格匹配。
input.PopContext(GameInput.PauseMenu);
```

`Exclusive` 屏蔽更低层 Context；`Overlay` 与更低层同时生效。同一个 Context 不能重复 Push，Pop 顺序错误会抛出 `InputOperationException`。

## 6. 在玩法节点读取当前帧

示例角色控制器：

```csharp
using Godot;
using GoDo;
using MyGame;

public partial class PlayerController : Node
{
    private IInputService? _input;

    public override void _Ready()
    {
        _input = Services.Get<IInputService>();
    }

    public override void _Process(double delta)
    {
        if (_input?.IsReady != true)
            return;

        InputFrame frame = _input.Frame;
        Vector2 move = frame.Axis2(GameInput.Move);

        if (frame.JustPressed(GameInput.Jump))
            GD.Print("Jump requested");

        ApplyMovementIntent(move, delta);
    }

    private void ApplyMovementIntent(Vector2 move, double delta)
    {
        // 这里交给具体游戏的移动逻辑。
    }
}
```

每次渲染帧重新取得 `InputFrame`。它是当前快照的轻量句柄，保存到下一帧再读取会抛出过期 Frame 错误。

如果物理控制器运行在 `_PhysicsProcess()`，应在 `_Process()` 缓存连续轴并锁存 `JustPressed`，再由物理帧消费，避免渲染频率和物理频率不同造成一次性输入丢失。

## 7. 根据活动设备显示提示

支持提示查询的后端可以提供当前绑定的回退文字：

```csharp
if (input.ActiveDevice != InputDeviceKind.Unknown &&
    input.TryGetPromptQuery(out IInputPromptQuery? prompts))
{
    IReadOnlyList<InputPromptInfo> jumpPrompts = prompts.GetPrompts(
        GameInput.Gameplay,
        GameInput.Jump,
        input.ActiveDevice);
}
```

监听 `InputDeviceChangedEvent` 和 `InputBindingsChangedEvent` 后低频刷新提示，不要每帧查询。`DisplayText` 只是文字回退；键帽图标、手柄品牌图形、本地化和排版仍由游戏 UI 管理。

## 常见错误

- `IsReady == false`：Installer 未运行、Profile 无效或 GUIDE/GuideCs Autoload 不完整。
- 未知 Action：代码 ID 与 Profile 不一致。
- Axis 类型错误：代码调用了 `Axis2()`，但 GUIDE Action 不是 Axis2D。
- Frame 过期：把某帧的 `InputFrame` 保存到字段后跨帧读取。
- Context Pop 失败：关闭页面的顺序与 Push 顺序不一致。
- 输入执行两次：业务又直接读取 GUIDE Action，绕过了 GoDo 快照。

精确接口可查询 <xref:GoDo.IInputService>、<xref:GoDo.InputFrame>、<xref:GoDo.InputContextMode>、<xref:GoDo.InputOperationException> 和 <xref:GoDo.GuideInput.GuideInputProfile>。
