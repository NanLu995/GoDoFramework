# GoDo GUIDE Input 使用指南

## 定位

`godo_guide_input` 是 GoDo InputService 与 G.U.I.D.E-CSharp 之间的可选适配包。它依赖两端，
但 `addons/godo_framework/` 不反向依赖本包或 GUIDE。

业务代码只读取 `IInputService`、`InputFrame`、`InputActionId` 和 `InputContextId`，不持有
`GuideAction` 或 `GuideMappingContext`。更换后端时，业务语义 ID 可以保持不变。

## 当前能力

- 从 `GuideInputProfile` 建立 GoDo Action / Context ID 到 GUIDE Resource 的固定映射。
- 从 GUIDE Action 自动推断 Bool、Axis1D、Axis2D、Axis3D 类型。
- 把 GoDo Context 栈的最终有效集合一次性应用到 GUIDE。
- 订阅 GUIDE Action 的 `Triggered / Ongoing / Completed` 信号并更新适配器缓存。
- GoDo 每帧只把适配器缓存复制到 InputFrame，不再轮询每个 GUIDE Action。
- 从原始 Godot InputEvent 识别键鼠、实体手柄与触摸，并声明 `DeviceTracking` 能力。
- 使用 GUIDE Remapper / InputDetector 提供查询、捕获、冲突检查、应用和恢复默认，并声明 `Rebinding` 能力。
- 由 GoDoRuntime 统一更新和关闭后端。

当前不包含改键配置持久化、输入提示图标和震动。`ActiveDevice` 在首次有效输入前为 `Unknown`；
改键只在本次运行内有效，配置导入/导出在独立批次完成后再开放。

## 依赖与顺序

需要同时存在：

```text
addons/godo_framework/
addons/guideCS/
addons/godo_guide_input/
```

项目 Autoload 顺序必须是：

```text
GUIDE
GuideCs
GoDoRuntime
```

适配器不会自动修改 `project.godot`。GUIDE 与 GuideCs 缺失或位于错误生命周期时，后端初始化失败。

## 创建 Profile

1. 使用 G.U.I.D.E 编辑器创建 Action 和 Mapping Context Resource。
2. 在 Godot Inspector 新建 `GuideInputProfile` Resource。
3. 向 `Actions` 添加 `GuideInputActionBinding`，填写稳定的 GoDo Action ID，并拖入对应 GUIDEAction Resource。
4. 向 `Contexts` 添加 `GuideInputContextBinding`，填写稳定的 GoDo Context ID，并拖入对应 GUIDEMappingContext Resource。
5. 向 `Bindings` 添加全部可重绑定槽位的 `GuideInputBindingDefinition`，填写稳定 Binding ID、已有 Context / Action ID 与 GUIDE Mapping 原始索引。

ID 示例：

```text
gameplay.move
gameplay.look
gameplay.jump
ui.confirm

gameplay
pause_menu
```

同一个 ID 或同一个 GUIDE Resource 不能重复映射。GUIDE Action 类型在初始化后固定，运行中不得更改。
Binding Definition 只能指向已标记为可重绑定的 GUIDE 槽位；多个 Binding ID 不能指向同一个 Context / Action / Index。Profile 必须覆盖全部可重绑定槽位，否则适配器会拒绝初始化，避免冲突查询静默漏项。

## 场景接入

在只进入一次的启动场景中添加 Installer：

```text
Bootstrap
└── GuideInputBackendInstaller
    └── Profile = res://Game/Input/GameInputProfile.tres
```

Installer 的 `_Ready()` 在 GoDoRuntime 已注册 InputService 后安装后端。Installer 不是后端生命周期所有者；
即使启动场景随后被替换，后端仍由 GoDoRuntime 持有和关闭。

第一版每个进程只允许安装一次后端。不要把 Installer 放入会重复进入的 Gameplay、关卡或菜单场景。

## 业务使用

游戏自己定义语义 ID：

```csharp
internal static class GameInput
{
    internal static readonly InputActionId Move = InputActionId.Create("gameplay.move");
    internal static readonly InputActionId Jump = InputActionId.Create("gameplay.jump");
    internal static readonly InputContextId Gameplay = InputContextId.Create("gameplay");
}
```

业务只访问 GoDo：

```csharp
IInputService input = Services.Get<IInputService>();
input.SetBaseContext(GameInput.Gameplay);

InputFrame frame = input.Frame;
Vector2 move = frame.Axis2(GameInput.Move);
bool jump = frame.JustPressed(GameInput.Jump);
```

不要在业务代码中再次读取 `GuideAction`，否则会绕过信号缓存并重新产生跨语言分配。

## 运行时重绑定

业务层仍只使用 GoDo 类型：

```csharp
InputBindingId jumpPrimary = InputBindingId.Create("gameplay.jump.primary");
if (input.TryGetRebinding(out IInputRebinding? rebinding))
{
    InputBindingCandidate? candidate = await rebinding.CaptureAsync(jumpPrimary);
    if (candidate != null && rebinding.FindConflicts(jumpPrimary, candidate).Count == 0)
        rebinding.Apply(jumpPrimary, candidate);
}
```

- 捕获节点常驻 `/root/GoDoRuntime/GoDoGuideInputDetector`，使用 GUIDE 原生 InputDetector。
- 捕获开始前等待 0.2 秒，避免启动按钮的鼠标释放被误识别；轴最小幅度为 0.5。
- Esc 是取消输入；捕获成功、主动取消或后端关闭都会结束等待任务。
- GUIDE 输入对象封装在适配器候选中，业务层不会获得插件类型。
- GUIDE 当前版本会复用旧的有效 Mapping 缓存。应用或恢复绑定时，适配器保存有效 Context、清空缓存、应用配置后恢复 Context，强制从新绑定重建；失败时恢复旧配置。
- 应用改键会短暂重建 GUIDE Context，属于设置界面低频操作，不应在每帧或连续滑动操作中调用。

## 设备检测

后端初始化时在常驻的 `/root/GoDoRuntime` 下添加内部 `GoDoGuideInputDeviceTracker`，因此启动场景被替换后仍能继续检测；
后端关闭时节点进入释放队列。它不修改 GUIDE 源码，也不进入 GoDo 核心依赖。

- 非 Echo 键盘按下、实体鼠标按钮和至少 1 像素的移动归为 `KeyboardMouse`。
- 实体手柄按钮和绝对值至少 `0.25` 的轴输入归为 `Gamepad`，低于阈值的摇杆漂移不切换提示。
- 屏幕触摸、至少 1 像素的拖动和 GUIDE 虚拟摇杆归为 `Touch`。
- Godot `device == -1` 的鼠标/触摸模拟事件被忽略，避免一次操作连续切换两种设备。
- 松开事件不改变活动设备；多个事件发生在一次 GoDo 采样前时，以最后一个有效类别为准。

## 失败语义

以下情况在安装阶段失败，InputService 保持未就绪：

- GUIDE / GuideCs Autoload 缺失。
- Profile 包含空 Binding、空 ID、空 Resource、重复 ID 或重复 Resource。
- Binding Definition 使用未知 ID、重复目标、负索引，或目标 GUIDE 槽位不存在/不可重绑定。
- Resource 不是预期的 GUIDE Action / Mapping Context。
- GUIDE Action 类型为 UNKNOWN。

Context 应用调用 GUIDE 的整组替换 API；失败时适配器尝试恢复此前集合，InputService 自身不提交新 Context 栈。
采样异常由 InputService 包装为 `InputOperationException`，上一帧保持不变。

## 生命周期与线程

- Installer 在启动场景 `_Ready()` 安装一次。
- GoDoRuntime 在 GUIDE `_Process` 之后集中采样。
- 设备跟踪节点使用 `ProcessMode.Always`，暂停树时仍能更新输入提示。
- 捕获节点跟随后端生命周期；关闭时取消未完成捕获并恢复空的 GUIDE RemappingConfig。
- 所有 API 仅允许 Godot 主线程调用。
- GoDoRuntime 退出时先清空 GUIDE Context，再释放包装引用。
- `Shutdown()` 幂等；InputService 负责保证正常生命周期只关闭一次。

## 性能

- Action 和 Context 的 Resource 包装、类型检查、Dictionary 与 List 创建只发生在初始化或 Context 变化时。
- 每帧不搜索场景树、不加载 Resource、不创建 Action 集合，也不轮询 GUIDE Action。
- 回归实测 3 个已配置 Action 连续执行 1,000 次 GoDo 样本复制产生 0 bytes 托管分配。
- GUIDE 更新 Action 时，信号桥接和活动 Axis 的一次属性读取仍会跨越 C# / GDScript 边界；上述 0 bytes
  只代表 GoDo 缓存采样，不代表 G.U.I.D.E 整体处理零分配。
- 任意数量的业务消费者随后读取 InputFrame 都是零分配缓存访问。
- Context 切换属于低频操作，允许 GUIDE 包装层创建临时集合。
- 设备分类仅在原始事件到来时执行；每帧不轮询按键、鼠标或摇杆。
- 重绑定查询、格式化、冲突检查与缓存重建会分配临时对象，但只允许在设置界面低频调用。

## 验证

自动回归入口：

```text
Verification/Automated/GuideInputBackendRegression.tscn
```

使用独立 GUIDE Fixture 验证 Profile 安装、Gameplay/Menu 隔离、键盘按钮、鼠标 Axis2D、设备阈值、
模拟事件过滤、虚拟摇杆归类、绑定查询、捕获、冲突、应用、恢复、取消和关闭清理，并验证
3 Actions × 1,000 次缓存采样为零托管分配。真实手柄、窗口失焦、编辑器 Profile 制作流程和渲染/物理帧延迟仍需手动验证。
