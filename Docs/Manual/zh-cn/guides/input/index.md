# 读取语义输入与管理 Context

InputService 让业务代码读取“移动、跳跃、确认”这类语义 Action，而不是依赖空格键、手柄按钮或某个第三方插件类型。它提供当前渲染帧快照、可释放 Context、活动设备和可选的改键、持久化与提示查询接口；InputActionRouter 负责有优先级的离散命令。

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

当前验证组合是 GUIDE `0.13.0` 与 GUIDE-CSharp `0.3.7--0.14.0`。从 [官方 GitHub 源码](https://github.com/Phlegmlee/G.U.I.D.E-CSharp)取得该组合并确保最终路径为 `addons/guideCS/`；团队项目应固定实际提交，避免后续源码更新造成不可复现的升级。设置窗口的“打开 GitHub 源码...”只会在系统浏览器中打开此页面，不会自动下载、解压或覆盖第三方文件。

该组合已经通过 GoDo 功能回归，但 GUIDE-CSharp 当前仍使用 `Activator.CreateInstance` 创建泛型包装器，严格 Native AOT/Trimming 分析会产生 `IL2087`。因此不要把 GUIDE 可选集成视为 iOS Native AOT 已验证；不安装本集成不会影响 GoDo 核心运行时。

复制依赖后先让 Godot 完成文件扫描和全局脚本类型缓存，再完成一次 C# 编译。随后打开：

```text
GoDo Framework → 打开 GoDo Framework... → 编辑器扩展 → GUIDE Input
```

右侧会直接显示状态报告、提示和操作；点击“重新检查”后提示栏会明确显示完成结果。按照检查结果安装或修复，不会再打开第二个设置窗口。正常 Autoload 顺序为：

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
using InputContextLease pauseContext =
    input.PushContextScoped(GameInput.PauseMenu, InputContextMode.Exclusive);
```

`Exclusive` 屏蔽更低层 Context；`Overlay` 与更低层同时生效。同一个 Context 不能重复 Push。Lease 可随页面生命周期 Dispose，即使关闭顺序与压入顺序不同也只释放自己的 Context；`SetBaseContext()` 或服务关闭后旧 Lease 会安全失效。旧 `PushContext / PopContext` 保留一个迁移周期，但仍要求严格栈顶配对，且不能 Pop Lease 项。

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

        ApplyMovementIntent(move, delta);
    }

    private void ApplyMovementIntent(Vector2 move, double delta)
    {
        // 这里交给具体游戏的移动逻辑。
    }
}
```

每次渲染帧重新取得 `InputFrame`。它是当前快照的轻量句柄，保存到下一帧再读取会抛出过期 Frame 错误。

需要保存单个 Action 的完整状态时，复制值快照：

```csharp
InputActionFrameState jump = frame.GetState(GameInput.Jump);
```

它包含 `Idle / Ongoing / Performed` 状态、本采样窗口累计的 Started/Performed/Completed/Cancelled 迁移、持续时间、`[0,1]` 进度和采样序号，可以安全跨帧保存。连续移动和视角仍直接读取 Frame。

菜单确认、暂停、交互等离散命令使用 Router：

```csharp
IInputActionRouter router = Services.Get<IInputActionRouter>();
using InputRouteScope scope = router.PushScope("PauseMenu");
using InputRouteBinding binding = scope.Bind(
    GameInput.Confirm,
    InputActionTransitions.Performed,
    (state, matched) =>
    {
        ConfirmSelection();
        return InputRouteResult.Handled;
    });
```

后创建的 Scope 优先；`Handled` 只阻止当前 Action 传给更低 Scope，`Pass` 继续。Context 或路由结构变化后，仍未回到 Idle 的已绑定 Action 会被门禁，先释放再按下才会触发，避免按住确认键打开新页面后立刻二次执行。Router 是输入分派边界，不要再通过全局 EventChannel 广播同一玩家命令。

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
- Context Pop 失败：新代码应使用 Lease；旧 API 的关闭顺序与 Push 顺序必须一致。
- 输入执行两次：业务又直接读取 GUIDE Action，绕过了 GoDo 快照。

精确接口可查询 <xref:GoDo.IInputService>、<xref:GoDo.IInputActionRouter>、<xref:GoDo.InputFrame>、<xref:GoDo.InputActionFrameState>、<xref:GoDo.InputContextLease>、<xref:GoDo.InputOperationException> 和 <xref:GoDo.GuideInput.GuideInputProfile>。

## 能力全景图

<div class="godo-capability-list">
<section><h4>读取就绪、帧、设备与后端能力</h4><p>只在当前帧消费 Frame，不跨帧保存快照。</p><pre class="godo-capability-call"><code>input.IsReady
input.Frame
input.ActiveDevice
input.Capabilities</code></pre></section>
<section><h4>设置基础 Context</h4><p>切换长期玩法输入集合。</p><pre class="godo-capability-call"><code>input.SetBaseContext(GameInputContexts.Gameplay);</code></pre></section>
<section><h4>拥有与查询 Context</h4><p>菜单和 Modal 用 Lease 绑定临时输入层的所有权。</p><pre class="godo-capability-call"><code>using InputContextLease menu = input.PushContextScoped(GameInputContexts.Menu, InputContextMode.Exclusive);
input.IsContextActive(GameInputContexts.Menu);
menu.Dispose();</code></pre></section>
<section><h4>路由离散迁移</h4><p>高优先级 Scope 可以消费确认、返回等离散 Action。</p><pre class="godo-capability-call"><code>using InputRouteScope scope = router.PushScope("Menu");
using InputRouteBinding binding = scope.Bind(action, InputActionTransitions.Performed, handler);</code></pre></section>
<section><h4>获取可选输入扩展</h4><p>按后端能力取得改键、持久化和提示查询接口。</p><pre class="godo-capability-call"><code>input.TryGetRebinding(out IInputRebinding rebinding)
input.TryGetRebindingPersistence(out IInputRebindingPersistence persistence)
input.TryGetPromptQuery(out IInputPromptQuery prompts)</code></pre></section>
</div>
