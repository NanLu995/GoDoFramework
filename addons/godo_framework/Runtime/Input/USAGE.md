# InputService 使用指南

## 定位

InputService 为业务层提供语义 Action 的当前帧只读快照、可释放的 Context 所有权，以及可选运行时重绑定和输入提示查询边界。`IInputActionRouter` 在同一采样之后分派离散 Action 迁移。它们集中采样一个可替换后端，
使角色、摄像机协调代码和 UI 不直接依赖具体按键、Godot InputMap 或第三方输入插件类型。

当前已完成核心 ID、Frame、Context、后端边界和 GoDoRuntime 生命周期接入。可选包
`addons/godo_framework/Integrations/GuideInput/` 已提供首版 GUIDE 后端；核心模块不反向依赖它。
业务可以通过 `Services.Get<IInputService>()` 与 `Services.Get<IInputActionRouter>()` 获取服务；未安装后端时 `IsReady == false`，读取 Frame 或切换 Context 会明确失败。

## 适用场景

- 单人动作、射击、RPG、平台跳跃、策略、模拟、解谜和常规 UI 游戏。
- 键鼠与手柄需要汇总为 Move、Look、Jump 等相同语义 Action。
- Gameplay、Menu、对话等状态需要明确屏蔽或叠加输入集合。
- 希望更换输入插件时不修改角色和 UI 业务代码。

## 非适用场景

- 本地多人设备分配和分屏输入。
- 格斗游戏的指令历史、节奏游戏的音频级判定。
- 网络输入预测、录制回放和确定性重演。
- 角色移动、摄像机旋转、连招窗口等具体玩法规则。

## Public API

```csharp
IInputService input = Services.Get<IInputService>();
if (!input.IsReady)
    return;
```

### InputActionId 与 InputContextId

两种 ID 均区分大小写，拒绝 null、空白和首尾空白。默认值为空，不能用于服务调用。

```csharp
InputActionId move = InputActionId.Create("gameplay.move");
InputContextId gameplay = InputContextId.Create("gameplay");
```

### InputFrame

```csharp
InputFrame frame = input.Frame;
bool jump = frame.JustPressed(GameInput.Jump);
Vector2 move = frame.Axis2(GameInput.Move);
InputActionFrameState jumpState = frame.GetState(GameInput.Jump);
```

- `Pressed`、`JustPressed`、`JustReleased` 可读取任意 Action 的状态。
- `Axis1`、`Axis2`、`Axis3` 必须与后端初始化时声明的固定类型一致。
- Frame 是当前渲染帧的轻量句柄，不复制 Action 集合；跨帧保存后再次读取会失败。
- `GetState` 返回可安全跨帧保存的值快照，包含 `Status`、本次采样累积的 `Transitions`、持续时间、归一化进度和 `Sequence`。
- 状态为 `Idle / Ongoing / Performed`；迁移位为 `Started / Performed / Completed / Cancelled`。同一采样窗口可以同时包含多个迁移位，取消优先于完成。
- `ElapsedRatio` 在核心边界保证位于 `[0, 1]`；后端给出的非有限值、负持续时间或越界进度会使本次采样失败且不覆盖上一帧。
- 后端首次成功采样前不能读取 Frame。

### 离散 Action Router

连续移动、视角和当前按压状态直接读取 `InputFrame`。菜单确认、暂停、交互等离散命令通过 Router 绑定：

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

- 后压入的 Scope 优先；同一 Action 内按 `Started → Performed → Cancelled → Completed` 顺序匹配。一个 Binding 在同一采样序号最多调用一次，并一次收到全部匹配位。
- `Handled` 只停止当前 Action 向较低 Scope 传播；`Pass` 继续。不同 Action 仍按后端固定布局顺序分派。
- 同一 Scope 的同一 Action 不能绑定重叠迁移位，避免同优先级歧义。空迁移掩码和未知 Action 会立即失败。
- Handler 抛出异常时 Router 通过 `ErrorHub` 报告作用域、Action、迁移和序号，并把该 Action 视为已消费；异常不会中断其他 Action。
- Handler 内新增、释放 Binding 或 Scope 会排队到本轮分派结束后生效，当前遍历保持稳定。
- Context 或路由结构发生实际变化时，当前或上一帧非 `Idle` 的已绑定 Action 会进入重触发门禁；必须观察到 `Idle` 后才恢复分派，避免打开菜单时继承仍按住的确认键。未绑定连续轴不参与门禁扫描。
- Router 只分派输入事实。返回、关闭、玩家命令等业务语义不要再复制为全局 `EventChannel` 输入事件；设备和绑定变化仍使用已有事实事件。

### 活动设备

`ActiveDevice` 返回后端最近识别到的键鼠、手柄或触摸类别；尚未观察到有效输入时为 `Unknown`。
支持可靠跟踪的后端会声明 `InputBackendCapabilities.DeviceTracking`。

```csharp
EventChannel.Bind<InputDeviceChangedEvent>(this, OnInputDeviceChanged);
```

`InputDeviceChangedEvent` 只在一次成功采样提交后且类别确实变化时发布，携带 `Previous` 与 `Current`；
相同设备的连续输入不会重复通知。具体提示文字、图标和排版属于游戏 UI。

### 输入提示查询

支持提示查询的后端声明 `InputBackendCapabilities.PromptQuery`：

```csharp
if (input.ActiveDevice != InputDeviceKind.Unknown &&
    input.TryGetPromptQuery(out IInputPromptQuery? prompts))
{
    IReadOnlyList<InputPromptInfo> jump = prompts.GetPrompts(
        GameInput.Gameplay,
        GameInput.Jump,
        input.ActiveDevice);
}
```

- 查询必须显式指定 Context、Action 与具体设备；`Unknown` 不是可查询设备。
- 一个 Action 可以按后端配置顺序返回多个提示，例如移动动作的多个键位；业务决定显示第一个、全部或组合形式。
- `IsBound == false` 表示槽位当前未绑定，此时 `DisplayText` 为空；指定设备没有槽位时返回空集合。
- `DisplayText` 是后端提供的简短文本回退。键帽图标、具体手柄图形、本地化和排版仍由游戏 UI 管理。
- `InputBindingsChangedEvent` 在绑定或整组配置成功应用后发布；UI 结合该事件与 `InputDeviceChangedEvent` 低频刷新，不应每帧查询。

### Context 栈

```csharp
input.SetBaseContext(GameInput.Gameplay);
using InputContextLease pauseContext =
    input.PushContextScoped(GameInput.PauseMenu, InputContextMode.Exclusive);
```

- `Overlay`：与更低层有效 Context 同时生效。
- `Exclusive`：屏蔽所有更低层 Context；其上方仍可继续叠加 Overlay。
- `SetBaseContext` 会移除所有临时 Context。
- `PushContextScoped` 返回 `InputContextLease`；Dispose 幂等，可以按所有者生命周期从栈中任意位置释放，适合页面、Modal 和临时流程。
- 同一个 ID 不能重复入栈。`SetBaseContext` 和服务关闭会使现有 Lease 失效，之后 Dispose 安全无操作。
- 旧 `PushContext / PopContext` 保留一个迁移周期；它们仍要求严格 LIFO，且不能 Pop 由 Lease 拥有的栈项。新代码优先使用 Lease，避免异常退出或乱序关闭遗留 Context。
- 后端原子应用失败时，GoDo Context 栈保持不变。
- Lease 释放若因后端失败而抛出，栈和 Lease 所有权保持不变，可以在恢复后再次 Dispose。

### 运行时重绑定

支持改键的后端声明 `InputBackendCapabilities.Rebinding`，并通过可选接口暴露能力：

```csharp
if (!input.TryGetRebinding(out IInputRebinding? rebinding))
    return;

InputBindingId jumpPrimary = InputBindingId.Create("gameplay.jump.primary");
InputBindingInfo current = rebinding.GetBinding(jumpPrimary);
InputBindingCandidate? candidate = await rebinding.CaptureAsync(jumpPrimary);
if (candidate != null && rebinding.FindConflicts(jumpPrimary, candidate).Count == 0)
    rebinding.Apply(jumpPrimary, candidate);
```

- `InputBindingId` 是业务稳定 ID，不应由插件资源路径、显示文字或数组位置临时拼接。
- `GetBindings` / `GetBinding` 返回当前值、默认值、设备类别和玩家显示文字。
- 同一后端同时只允许一个 `CaptureAsync`；`CancelCapture` 结束当前捕获并使任务返回 null。
- `InputBindingCandidate` 隐藏具体后端对象，只能交回创建它的后端。
- `FindConflicts` 只返回事实；`Apply` 和 `RestoreDefault` 不自动覆盖其他槽位。

支持可靠存储的后端额外声明 `RebindingPersistence`：

```csharp
if (input.TryGetRebindingPersistence(out IInputRebindingPersistence? persistence))
{
    InputBindingLoadStatus status = persistence.LoadAndApply();
    // Apply / RestoreDefault 成功后，由设置界面在合适时机调用：
    persistence.Save();
}
```

- `LoadAndApply` 在没有配置时应用默认绑定并返回 `DefaultsApplied`。
- 正式配置与备份的具体可靠性由后端存储实现负责；GUIDE 适配使用 SaveService。
- `Save` 失败不会撤销本次运行内已经应用的绑定，调用方应明确提示玩家。
- 保存时机由游戏决定，InputService 不在每次 Apply 后隐式写盘。

### IInputBackend

`IInputBackend` 是可选适配包的扩展边界，不是业务 API。后端必须：

- 初始化后保持 Action、Context 数量及 Action 类型不变。
- 按固定 Action 顺序写满调用方提供的 `Span<InputActionSample>`。
- 原子应用最终有效 Context，失败时保持原后端映射。
- 允许重复调用 `Shutdown()`，并对称取消订阅和释放状态。
- 不在每帧采样中创建集合或执行资源查找。

## 失败语义

以下情况抛出 `InputOperationException`：

- 未安装后端或首次采样前读取 Frame。
- 读取未知 Action、错误 Axis 类型或过期 Frame。
- 使用未知 Context、重复 Push、错误 Pop 或后端应用失败。
- Router 未注册 Action、空/未知迁移、同 Scope 重叠绑定或失效句柄操作。
- 后端重复安装、布局重复、初始化或采样失败。
- 未注册 Binding、重复捕获、候选来自其他后端或重绑定应用失败。
- 后端声明重绑定能力却未实现对应接口，或声明持久化但未同时支持重绑定。
- 后端的 `PromptQuery` 能力标志与 `IInputPromptBackend` 实现不一致。

默认 ID 和无效枚举属于参数错误，抛出 `ArgumentException` / `ArgumentOutOfRangeException`。
采样失败不会推进 Frame 序号或覆盖上一帧；调用边界不先重复上报 ErrorHub。
持久化的编解码或磁盘失败沿用具体存储实现的异常；GUIDE 适配会抛出 `SaveException`。

## 生命周期与线程

- InputService 与 InputActionRouter 由 GoDoRuntime 依次创建并注册；后端就绪后每帧严格执行 `InputService.Update → InputActionRouter.Dispatch → 业务节点`。
- 所有服务 API、后端初始化、采样、Context 和关闭操作仅允许 Godot 主线程调用。
- 重绑定捕获任务由后端信号在主线程完成；服务关闭前必须取消未完成捕获。
- 绑定加载应在后端安装完成后、进入依赖输入的游戏流程前执行；保存只在玩家确认设置时调用。
- 每个服务实例第一版只允许安装一个后端，不支持运行时替换。
- 退出时先关闭并注销 Router，使 Scope/Binding 句柄安全失效，再关闭 InputService；两者关闭均允许重复调用。

## 渲染帧与物理帧

InputFrame 表示最近完成的渲染帧采样。需要驱动物理的控制器应在 `_Process` 缓存连续输入并锁存
`JustPressed`，再由 `_PhysicsProcess` 消费，避免渲染和物理频率不一致时漏掉一次性命令。
第一版不维护第二套 PhysicsFrame。

## 性能

- 后端安装时建立 Action ID 到连续槽位的 Dictionary。
- 每帧使用预分配样本和状态数组，成功采样后原子提交。
- Action 迁移位在后端信号到达与下一次采样之间累计，采样提交后清空；状态和值继续保留。
- Router 稳态分派不复制 Binding 集合；无处理器和有处理器的 1,000 次回归均要求 0 bytes 托管分配。
- 同帧重复读取只访问缓存，不再次调用后端。
- 当前假后端回归要求 10,000 次 `Axis2` 读取产生 0 bytes 托管分配。
- 设备类别只在已有采样提交时比较；事件只在类别变化时派发，不增加输入热路径集合分配。
- Context 变化属于低频路径，允许创建小型临时数组以保证提交前状态不变。
- 查询、捕获、冲突检查和应用绑定属于设置界面低频路径，允许创建结果数组和异步完成对象，不进入每帧采样。
- 提示查询只扫描指定 Context 的绑定，并只在有结果时创建返回数组；UI 应在设备或绑定变化时刷新并缓存显示结果。
- 配置 Resource 编解码和磁盘 I/O 是同步低频路径，不得从每帧更新或滑块连续变化中调用。

## Debug 诊断

Debug 构建中的 `InputService` 提供 internal 只读快照，包含后端类型、活动设备、能力、采样与 Context 修订号、完整 Context 栈的所有权/Token/有效性，以及固定顺序的 Action 值、状态、迁移、时间和门禁。Router 快照另外列出路由修订号、最近分派序号、Scope 优先级和 Binding 数量。类型和入口都位于 `#if DEBUG`，不扩大业务服务接口，Release 不包含。

GoDo Debugger 的 `运行时 / Input` 页面每 0.25 秒按需读取当前快照，以状态卡、Context 表和 Action 表显示。Action 可按名称或值类型搜索，搜索扫描完整快照但最多排版前 32 个匹配项；Frame 状态独立更新，Context 或 Action 显示内容未变化时不重建对应表格。折叠或查看其他页面时不创建快照；快照只用于观察，不可修改 Context、Action 或绑定。

## 验证

自动回归入口：

```text
Verification/Automated/InputServiceRegression.tscn
```

覆盖 ID、后端缺失、首次采样、Bool/Axis 状态、完整状态/迁移累积、活动设备变化、可选重绑定、持久化与提示查询能力、Frame 过期、Lease 与旧 Context API 混用、
失败原子性、重复后端/布局拒绝、Debug-only 快照、采样热路径分配和关闭幂等。`InputActionRouterRegression.tscn` 覆盖优先级、传播、顺序、异常隔离、遍历期变更、重触发门禁、句柄生命周期和零分配；`InputRuntimeRegression.tscn` 验证 GoDoRuntime 的注册、采样后分派、暂停树和关闭顺序。
GUIDE 回归覆盖键鼠/手柄提示筛选、绑定变化通知、捕获、冲突、应用、恢复、取消、保存加载、备份恢复、未知版本、设备阈值和跟踪节点清理。Windows Demo3D 已使用真实手柄完成人工验收，覆盖设备切换、拔插后键盘切换、改键即时提示、重启持久化、恢复默认和窗口失焦；其他平台以及真实项目长期渲染/物理时序仍需验证。
