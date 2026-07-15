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
- 由 GoDoRuntime 统一更新和关闭后端。

当前不包含设备类别检测、运行时改键、输入提示格式化和震动，因此 `Capabilities == None`、
`ActiveDevice == Unknown`。这些能力在独立批次验证后再开放，不虚报 GUIDE 已有但适配器尚未暴露的能力。

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

## 失败语义

以下情况在安装阶段失败，InputService 保持未就绪：

- GUIDE / GuideCs Autoload 缺失。
- Profile 包含空 Binding、空 ID、空 Resource、重复 ID 或重复 Resource。
- Resource 不是预期的 GUIDE Action / Mapping Context。
- GUIDE Action 类型为 UNKNOWN。

Context 应用调用 GUIDE 的整组替换 API；失败时适配器尝试恢复此前集合，InputService 自身不提交新 Context 栈。
采样异常由 InputService 包装为 `InputOperationException`，上一帧保持不变。

## 生命周期与线程

- Installer 在启动场景 `_Ready()` 安装一次。
- GoDoRuntime 在 GUIDE `_Process` 之后集中采样。
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

## 验证

自动回归入口：

```text
Verification/Automated/GuideInputBackendRegression.tscn
```

使用独立 GUIDE Fixture 验证 Profile 安装、Gameplay/Menu 隔离、键盘按钮、鼠标 Axis2D、关闭清理，
并验证 3 Actions × 1,000 次缓存采样为零托管分配。真实手柄、窗口失焦、编辑器 Profile 制作流程和渲染/物理帧延迟仍需手动验证。
