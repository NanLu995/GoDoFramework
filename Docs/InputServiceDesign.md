# InputService 设计草案

> 状态：架构升级完成。核心 ID、完整 Action 状态/迁移、InputFrame、Context Lease、离散 Action Router、重触发门禁、GoDoRuntime 确定更新顺序、GUIDE 六信号适配、设备检测、运行时改键、持久化、提示查询与 Debug 诊断均有自动回归；Windows 真实设备的既有验收已通过，升级后的真实项目手感、暂停与场景切换仍待 Godot 人工复核。本文不代表稳定基线。

## 1. 要解决的问题

Godot 原生输入足以读取按键和轴，但具体游戏仍需重复处理以下工程问题：

- 业务代码直接依赖键盘键值、`InputMap` Action 或第三方插件对象，后续改键或更换输入方案时改动扩散。
- Gameplay、Menu、对话和调试界面需要切换输入上下文，容易出现暂停菜单打开后角色仍移动的问题。
- 键鼠与手柄需要汇总成同一语义 Action，并提供一致的按下、刚按下、刚释放和轴值。
- 多个消费者在同一帧重复跨 C# / GDScript 边界读取输入。当前 InputLab 中，G.U.I.D.E-CSharp 的
  `ValueAxis2d` 连续读取 10,000 次约产生 1,880,224 bytes 线程分配。
- 改键需要冲突查询、保存、恢复默认值和失败反馈，不应散落在设置界面业务代码中。

`InputService` 的价值不是替代 Godot 或 G.U.I.D.E，而是为游戏提供稳定的语义输入边界，并把后端采样集中到每帧一次。

## 2. 已确认的方案

采用“Action ID + `InputFrame` 连续快照 + Context Lease + 离散 Router”：

```text
Godot 输入事件
    ↓
输入后端（首个候选为 G.U.I.D.E）
    ↓ 每帧集中采样一次
GoDo InputService / InputFrame（连续状态）
    ↓ 同一采样序号
InputActionRouter（离散迁移）
    ↓
角色、摄像机协调代码、UI 和游戏流程
```

业务层不持有 `GuideAction`、`GuideMappingContext` 或其他插件类型。可选适配包可以依赖 GoDo 与插件，
`addons/godo_framework/` 不反向依赖适配包。

## 3. 第一版职责

### 3.1 负责

- 使用 `InputActionId`、`InputContextId` 表达跨游戏可定义的语义标识。
- 在固定位置每帧采样一次后端，形成只读 `InputFrame`。
- 读取 Bool、Axis1D、Axis2D 和 Axis3D Action。
- 提供 `Pressed`、`JustPressed`、`JustReleased` 状态。
- 提供 `Idle / Ongoing / Performed` 完整状态、同窗迁移集合、持续时间与归一化进度。
- 使用可释放 Lease 管理 Gameplay、Menu 等 Context 所有权，并保留旧严格 LIFO API 一个迁移周期。
- 通过优先 Scope 路由离散迁移，支持 Handled/Pass、遍历期延迟变更、异常隔离和重触发门禁。
- 暴露当前主要输入设备类别及设备变化通知。
- 在后端支持时提供改键项查询、冲突查询、应用、恢复默认值和配置导入/导出边界。
- 在后端支持时按 Context、Action 与具体设备提供当前文本提示，并在绑定应用后发布失效通知。
- 对后端缺失、Action 缺失、类型不匹配、Context 栈误用和配置失败给出可见错误。

### 3.2 不负责

- 角色移动、跳跃、攻击、摄像机旋转或 UI 导航规则。
- 跳跃宽限、连招窗口、蓄力取消等玩法级输入缓冲。
- 鼠标灵敏度、Y 轴反转等设置数据的持久化；业务可把设置值传给控制器或后端配置。
- 输入提示图标的具体 UI 样式。
- 第一版中的本地多人、分屏设备分配、录制回放、网络输入预测和震动编排。
- 运行时热更换后端。第一版每个进程只安装一个后端，重复安装直接失败。

## 4. 公共模型草案

下面的签名用于确定语义，名称与细节仍需在实现阶段通过编译和测试校正。

```csharp
public readonly struct InputActionId : IEquatable<InputActionId>
{
    public string Value { get; }
    public bool IsEmpty { get; }
    public static InputActionId Create(string value);
}

public readonly struct InputContextId : IEquatable<InputContextId>
{
    public string Value { get; }
    public bool IsEmpty { get; }
    public static InputContextId Create(string value);
}

public enum InputActionValueType
{
    Bool,
    Axis1D,
    Axis2D,
    Axis3D,
}

public enum InputContextMode
{
    Overlay,
    Exclusive,
}

public enum InputActionStatus { Idle, Ongoing, Performed }

[Flags]
public enum InputActionTransitions
{
    None = 0,
    Started = 1,
    Performed = 2,
    Completed = 4,
    Cancelled = 8,
}

public enum InputDeviceKind
{
    Unknown,
    KeyboardMouse,
    Gamepad,
    Touch,
}
```

ID 的验证和比较遵循现有 `CameraId`：拒绝 null、空白和首尾空白，使用区分大小写的序号比较。

## 5. InputFrame

`InputFrame` 是当前渲染帧的轻量只读句柄，不复制 Action 字典，也不允许跨帧长期保存：

```csharp
public readonly struct InputFrame
{
    public ulong Sequence { get; }
    public bool Pressed(InputActionId action);
    public bool JustPressed(InputActionId action);
    public bool JustReleased(InputActionId action);
    public float Axis1(InputActionId action);
    public Vector2 Axis2(InputActionId action);
    public Vector3 Axis3(InputActionId action);
    public InputActionFrameState GetState(InputActionId action);
}
```

- `IInputService.Frame` 返回 struct，不产生每帧堆分配。
- 后端 Action 在初始化时映射为连续槽位；热路径只读取数组，不每帧创建 Dictionary、List 或 LINQ 结果。
- 同一个 `InputFrame` 可以在本帧被多个消费者重复读取，不再次调用后端。
- 读取不存在的 Action 或使用错误的 Axis 类型时抛出 `InputOperationException`，不静默返回零值。
- 保存旧 Frame 并在后续帧读取属于误用；实现应通过 `Sequence` 检测并在 Debug、Release 中一致地明确失败。
- `GetState` 复制单个 Action 的值、状态、迁移、持续时间、`[0,1]` 进度和序号，返回值可以安全跨帧保存。
- 后端在两次 Sample 间累积全部迁移；提交后只清迁移位，不清当前状态和值。取消支配同窗完成。

## 6. Context 所有权与离散路由

已采用接口：

```csharp
public interface IInputService
{
    bool IsReady { get; }
    InputFrame Frame { get; }
    InputDeviceKind ActiveDevice { get; }
    InputBackendCapabilities Capabilities { get; }

    void SetBaseContext(InputContextId context);
    void PushContext(InputContextId context, InputContextMode mode = InputContextMode.Exclusive);
    InputContextLease PushContextScoped(InputContextId context, InputContextMode mode = InputContextMode.Exclusive);
    void PopContext(InputContextId expectedContext);
    bool IsContextActive(InputContextId context);
}
```

语义：

- `SetBaseContext` 设置栈底流程上下文，例如 MainMenu 或 Gameplay；替换时清空所有临时 Context。
- `PushContext(..., Exclusive)` 暂停更低层 Context，适合 PauseMenu、对话或改键捕获。
- `PushContext(..., Overlay)` 与更低层 Context 同时生效，适合不阻断玩法的快捷栏。
- `PopContext(expectedContext)` 只允许弹出栈顶且必须匹配预期 ID；不匹配时抛异常，避免错误恢复输入状态。
- 重复 Push 同一个 ID 第一版不支持，直接失败，避免引用计数和嵌套所有权复杂化。
- Lease Dispose 幂等并按唯一 Token 释放自己的栈项，不要求位于栈顶；后端失败时不提交栈变更，Lease 可重试。
- `SetBaseContext` 与服务关闭使旧 Lease 安全失效。旧 Push/Pop 不能释放 Lease 项，避免两套所有权混淆。
- 窗口失焦时后端必须清理按下状态，防止恢复焦点后出现“卡键”。

Context 变更属于低频操作，可以更新后端映射缓存；不得放进 `_Process` 或 `_PhysicsProcess` 每帧调用。

### 6.1 离散 Router

`IInputActionRouter` 只处理离散迁移；连续轴继续直接读取 Frame。Scope 后入先处理，Action 按后端布局顺序，迁移固定按 Started、Performed、Cancelled、Completed 匹配。一个 Binding 每序号至多调用一次并收到全部匹配位；Handled 只截断当前 Action。

Router 在分派期间把新增/释放排队到本轮结束，避免集合快照和遍历失效。Handler 异常由 ErrorHub 携带 Scope、Action、迁移和序号报告，并视为已消费。Context 或路由修订变化时，当前或上一帧仍非 Idle 的已绑定 Action 被门禁，观察到 Idle 后恢复；这防止按住确认键打开新界面后立即再次触发。Router 不把玩家命令复制到 EventChannel。

## 7. 后端边界

`IInputBackend` 是适配包实现的公开扩展边界，但不是游戏业务入口：

```csharp
public interface IInputBackend
{
    InputBackendCapabilities Capabilities { get; }
    IReadOnlyList<InputActionDescriptor> Actions { get; }

    void Initialize();
    void ApplyContexts(ReadOnlySpan<InputContextActivation> contexts);
    void Sample(Span<InputActionSample> destination);
    void Shutdown();
}
```

约束：

- `InputService` 只依赖 `IInputBackend`，不引用 G.U.I.D.E 类型。
- 后端在 `Initialize` 时声明固定 Action 集合；运行中不得改变 Action 数量和类型。
- `Sample` 由 GoDo 每帧调用一次，写入预分配 Span，不返回新集合。
- Context 变化时才允许后端重建映射。
- 所有 Godot 对象访问限制在主线程。
- `Shutdown` 必须可重复调用，并对称取消信号订阅和释放后端状态。

首个适配包位于 `addons/godo_framework/Integrations/GuideInput/`。它依赖 GoDo Input 与 G.U.I.D.E-CSharp，负责：

- `InputActionId` / `InputContextId` 到 GUIDE Resource 的映射。
- GUIDE Context 优先级和 GoDo Context 栈之间的转换。
- 订阅 GUIDE Action 状态信号并缓存值；每帧只把缓存写入预分配样本数组。
- 设备变化、重绑定和映射变化的桥接。

GoDo 核心保持可单独安装。没有后端时 `IInputService` 可以存在但 `IsReady == false`；第一次读取 Frame、
切换 Context 或改键时抛出明确的 `InputOperationException`。第一版不额外实现第二套 Godot InputMap 后端。

## 8. 改键边界

改键是 InputService 的可选能力，不混入每帧热路径：

```csharp
public interface IInputRebinding
{
    bool IsCapturing { get; }
    IReadOnlyList<InputBindingInfo> GetBindings(InputContextId context);
    InputBindingInfo GetBinding(InputBindingId binding);
    Task<InputBindingCandidate?> CaptureAsync(InputBindingId binding);
    void CancelCapture();
    IReadOnlyList<InputBindingInfo> FindConflicts(InputBindingId binding, InputBindingCandidate candidate);
    void Apply(InputBindingId binding, InputBindingCandidate candidate);
    void RestoreDefault(InputBindingId binding);
}
```

`IInputService.TryGetRebinding(out IInputRebinding? rebinding)` 用于查询后端是否支持；不支持时返回 false，
避免业务代码猜测插件类型。

- 冲突如何处理由游戏 UI 决定，服务只返回事实，不擅自覆盖另一绑定。
- `InputBindingId` 由 Profile 显式映射到后端槽位，不能依赖显示名或运行时数组顺序。
- 候选输入只公开设备类别和显示文本；实际 GUIDE 输入对象保留在适配器内部。
- `IInputRebindingPersistence` 是独立可选能力；加载返回默认、正式文件或备份来源，保存时机由游戏 UI 决定。
- Input 核心不直接依赖 Save；GUIDE 适配层显式接收 SaveService，并用独立槽位保存数据版本 1 的原生配置 Payload。
- 保存失败不撤销已生效的运行时绑定；加载或应用失败保持此前绑定，异常由调用方上报。
- GUIDE Resource 反序列化后必须移除插件的运行时使用标记，再由 GUIDE 重新建立输入状态。
- GUIDE 应用配置时需要清空并恢复当前 Context，规避当前版本复用旧有效 Mapping 的缓存行为；应用失败必须回滚旧配置。

## 9. 设备体验

第一版记录最后产生有效输入的设备类别，并在类别变化时发布事实通知。设备切换只在输入超过有效阈值时发生，
避免手柄摇杆噪声使键鼠提示来回闪烁。

GUIDE 适配采用长期原始事件监听节点：手柄轴阈值为 `0.25`，指针/触摸位移阈值为 1 像素；忽略 Godot
`device == -1` 的模拟事件，GUIDE 虚拟摇杆归为触摸。核心在成功采样提交后比较类别，并通过
`InputDeviceChangedEvent` 仅发布实际变化；同类连续输入不重复通知。

输入提示 UI 通过可选 `IInputPromptQuery` 按 Context、Action 与 `ActiveDevice` 查询稳定顺序的文本结果，并结合
`InputDeviceChangedEvent` 与 `InputBindingsChangedEvent` 低频刷新。图标资源、手柄品牌图形、排版和本地化属于业务 UI。
震动能力后置；后续可基于 `InputBackendCapabilities.Rumble` 增加独立、可停止的轻量 API，不在第一版预留复杂效果系统。

## 10. 生命周期与更新顺序

- `GoDoRuntime` 创建并依次注册 `IInputService`、`IInputActionRouter`；退出时先关闭/注销 Router，再关闭后端和 InputService。
- 可选适配包在业务场景创建前安装一次后端；安装失败必须阻止进入依赖输入的业务流程。
- G.U.I.D.E 在 `_process` 中计算 Action。适配器验证三个 Autoload 为 Always，并把 GUIDE Process Priority 显式设为 `GoDoRuntime - 1`；运行顺序为 GUIDE 更新、InputService 采样、Router 分派，关闭时恢复原 Priority。
- `_Process` 中的业务消费者读取本渲染帧快照。需要驱动物理的业务控制器在 `_Process` 缓存连续量，
  并锁存 `JustPressed` 等一次性命令，再由 `_PhysicsProcess` 消费；不得假设渲染帧和物理帧一一对应。
- 第一版不增加第二套 `PhysicsFrame`。物理帧是否产生可感知延迟、业务锁存是否形成重复样板，必须在
  Demo3D 中测试低渲染帧率、高渲染帧率和不同物理 Tick 组合；只有出现重复痛点才扩展框架。
  没有这项证据前，模块不得标为稳定基线。
- 暂停时服务和后端保持处理，以保证 PauseMenu 与设备切换仍可用。

## 11. 失败与诊断

以下属于调用或配置错误，抛出 `InputOperationException`：

- 未安装后端却读取 Frame 或切换 Context。
- Action / Context 未注册。
- Axis 读取类型与 Action 声明不匹配。
- Context 栈 Pop 顺序错误或重复 Push。
- 后端初始化、Context 应用、采样、改键导入或应用失败。

异常只在调用边界抛出，不先重复上报 ErrorHub。设备连接变化、后端降级等非异常事实可通过 EventChannel 通知；
输入模块不直接引用 UI、Camera、Scene、Settings 或其他横向 Runtime Service。

Debug 快照暴露后端、设备、采样/Context/路由修订号、Context 所有权与有效性、Scope 优先级、Action 状态/迁移/时间和门禁；不记录原始逐键历史，也不提供修改入口。

## 12. Demo3D 使用流程

游戏定义自己的语义 ID，GoDo 不内置 Move、Jump 等玩法概念：

```csharp
internal static class Demo3DInput
{
    internal static readonly InputActionId Move = InputActionId.Create("gameplay.move");
    internal static readonly InputActionId Look = InputActionId.Create("gameplay.look");
    internal static readonly InputActionId Jump = InputActionId.Create("gameplay.jump");
    internal static readonly InputActionId ReleasePointer = InputActionId.Create("gameplay.release_pointer");

    internal static readonly InputContextId Gameplay = InputContextId.Create("gameplay");
    internal static readonly InputContextId Result = InputContextId.Create("result");
}
```

进入 Gameplay：

```csharp
IInputService input = Services.Get<IInputService>();
input.SetBaseContext(Demo3DInput.Gameplay);
```

角色在渲染帧读取输入并锁存一次性命令：

```csharp
private Vector2 _movementInput;
private bool _jumpRequested;

public override void _Process(double delta)
{
    InputFrame frame = input.Frame;
    _movementInput = frame.Axis2(Demo3DInput.Move);
    _jumpRequested |= frame.JustPressed(Demo3DInput.Jump);
}
```

物理帧消费缓存，避免渲染帧与物理帧频率不同时漏掉跳跃：

```csharp
public override void _PhysicsProcess(double delta)
{
    if (_jumpRequested && IsOnFloor())
        Jump();

    _jumpRequested = false;
    Move(_movementInput, delta);
}
```

视角由业务协调层把输入交给 Camera Rig；InputService 与 CameraService 不直接依赖：

```csharp
Vector2 look = input.Frame.Axis2(Demo3DInput.Look);
cameraController.RotateLook(look);
```

进入结算页时切换到不含 Gameplay 映射的 Context：

```csharp
input.SetBaseContext(Demo3DInput.Result);
```

## 13. 第一版验收标准

自动验证：

- ID 正常值、默认值、空白和比较语义。
- Frame Bool / Axis 各类型读取、缺失 Action 和类型错误。
- 同帧重复读取不再次调用后端，热路径 10,000 次读取不产生托管堆分配。
- Context Base、Overlay、Exclusive、严格 Pop、重复 Push 和后端失败回滚。
- Lease 乱序释放、幂等、失败重试、SetBase/关闭失效以及与旧 API 的边界。
- Router 优先级、传播、固定顺序、同窗多迁移、异常隔离、遍历期变更、序号去重、重触发门禁和句柄关闭。
- InputService Update 与 Router 空/有 Handler 各 1,000 次稳态零托管分配。
- 后端缺失、重复安装、初始化失败和 Shutdown 幂等。
- 改键查询、捕获、冲突、应用、恢复、取消及应用失败回滚。
- 设备切换阈值与窗口失焦清理。

Godot 手动验证：

- Demo3D 键鼠和手柄移动、视角、跳跃及 Gameplay / Result 恢复。
- Demo3D 跳跃主绑定查询、键盘改绑、Esc 取消、重启后持久化和恢复默认。
- 30 FPS 渲染 / 60 Hz 物理及 120 FPS 渲染 / 60 Hz 物理下的输入延迟和 JustPressed 不丢失。
- 窗口失焦、手柄拔插、暂停树和场景切换后的状态清理。
- 改键后重启游戏仍生效，冲突提示由 UI 决策后再应用。

## 14. 实现拆分建议

每一步单独确认、实现和验证：

1. 只实现核心 ID、Frame、Context 栈和假后端自动测试。（已完成，旧 Context API 现处于迁移周期）
2. 接入 GoDoRuntime 生命周期，但尚不改 Demo3D。（已完成）
3. 新建 `Integrations/GuideInput` 可选适配包，复用 InputLab 证据。（已完成）
4. 将 Demo3D 的移动、视角、跳跃、鼠标释放和 Result 隔离迁移为业务使用示例。（已完成）
5. 完成设备检测、变化通知与 Demo3D 状态展示。（已完成；Windows 真实设备手动验收通过）
6. 完成改键与输入提示查询。（已完成；自动回归与 Windows 真实设备人工验收通过）
7. 完成完整 Action 状态、Context Lease、离散 Router、重触发门禁与扩展诊断。（已完成自动回归；待真实项目人工验收）
