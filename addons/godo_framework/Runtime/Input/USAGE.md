# InputService 使用指南

## 定位

InputService 为业务层提供语义 Action 的当前帧只读快照和 Context 栈。它集中采样一个可替换后端，
使角色、摄像机协调代码和 UI 不直接依赖具体按键、Godot InputMap 或第三方输入插件类型。

当前已完成核心 ID、Frame、Context、后端边界和 GoDoRuntime 生命周期接入。可选包
`addons/godo_guide_input/` 已提供首版 GUIDE 后端；核心模块不反向依赖它。
业务可以通过 `Services.Get<IInputService>()` 获取服务；未安装后端时 `IsReady == false`，读取 Frame 或切换 Context 会明确失败。

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
```

- `Pressed`、`JustPressed`、`JustReleased` 可读取任意 Action 的状态。
- `Axis1`、`Axis2`、`Axis3` 必须与后端初始化时声明的固定类型一致。
- Frame 是当前渲染帧的轻量句柄，不复制 Action 集合；跨帧保存后再次读取会失败。
- 后端首次成功采样前不能读取 Frame。

### Context 栈

```csharp
input.SetBaseContext(GameInput.Gameplay);
input.PushContext(GameInput.PauseMenu, InputContextMode.Exclusive);
input.PopContext(GameInput.PauseMenu);
```

- `Overlay`：与更低层有效 Context 同时生效。
- `Exclusive`：屏蔽所有更低层 Context；其上方仍可继续叠加 Overlay。
- `SetBaseContext` 会移除所有临时 Context。
- 同一个 ID 不能重复入栈；Pop 必须与栈顶 ID 严格匹配。
- 后端原子应用失败时，GoDo Context 栈保持不变。

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
- 后端重复安装、布局重复、初始化或采样失败。

默认 ID 和无效枚举属于参数错误，抛出 `ArgumentException` / `ArgumentOutOfRangeException`。
采样失败不会推进 Frame 序号或覆盖上一帧；调用边界不先重复上报 ErrorHub。

## 生命周期与线程

- InputService 由 GoDoRuntime 创建并按 `IInputService` 注册；后端就绪后由 GoDoRuntime 每帧调用一次采样。
- 所有服务 API、后端初始化、采样、Context 和关闭操作仅允许 Godot 主线程调用。
- 每个服务实例第一版只允许安装一个后端，不支持运行时替换。
- `Shutdown()` 清理后端、Action、Context 和快照状态，并允许重复调用。

## 渲染帧与物理帧

InputFrame 表示最近完成的渲染帧采样。需要驱动物理的控制器应在 `_Process` 缓存连续输入并锁存
`JustPressed`，再由 `_PhysicsProcess` 消费，避免渲染和物理频率不一致时漏掉一次性命令。
第一版不维护第二套 PhysicsFrame。

## 性能

- 后端安装时建立 Action ID 到连续槽位的 Dictionary。
- 每帧使用预分配样本和状态数组，成功采样后原子提交。
- 同帧重复读取只访问缓存，不再次调用后端。
- 当前假后端回归要求 10,000 次 `Axis2` 读取产生 0 bytes 托管分配。
- Context 变化属于低频路径，允许创建小型临时数组以保证提交前状态不变。

## 验证

自动回归入口：

```text
Verification/Automated/InputServiceRegression.tscn
```

覆盖 ID、后端缺失、首次采样、Bool/Axis 状态、Frame 过期、Context 组合与误用、失败原子性、
重复后端/布局拒绝、热读取分配和关闭幂等。`InputRuntimeRegression.tscn` 额外验证 GoDoRuntime 注册、自动采样与关闭。
GUIDE、真实设备、窗口失焦以及渲染/物理时序将在后续批次手动验证。
