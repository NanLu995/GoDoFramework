# Demo3D

一个使用 GoDoFramework 的第三人称 3D 收集 Demo。

## 运行

确认项目已安装 `GoDoRuntime` Autoload 后，在 Godot 中打开并运行：

```text
Templates/Demo3D/Boot/Boot.tscn
```

## 操作

- `WASD`：移动
- `空格`：默认跳跃；可在 HUD 中运行时改绑
- 鼠标：旋转视角
- `Esc`：释放鼠标；改键捕获中用于取消
- 手柄左摇杆：移动
- 手柄右摇杆：旋转视角
- 手柄 A：跳跃

收集场景内 5 个蓝色能量核心后，会显示结算界面；点击“再玩一次”可重新开始。

## 框架接入

- `Procedure`：`Boot → Gameplay → Result` 顶层流程。
- `SceneService`：加载和重开 3D 主内容场景。
- `UiService`：在 Scene 层打开 HUD、在 View 层打开结算界面。
- `EventChannel`：收集物、流程和 HUD 之间传递业务事件。
- `ResourceKey`：集中维护业务场景和 UI 的资源定位。
- `CameraService`：Gameplay 流程通过语义 ID 激活主镜头，不负责解析 Phantom 节点。
- `PhantomCameraRig`：把 CameraService 的激活/停用转换为 Phantom Camera 优先级。
- `InputService`：向业务代码提供 Move、Look、Jump 等语义输入，不暴露 GUIDE 类型。
- `godo_guide_input`：把 GUIDE Action / Mapping Context 转换为 InputService 快照。
- Gameplay HUD：监听 `InputDeviceChangedEvent`，显示当前键鼠、手柄或触摸类别。
- Gameplay HUD：通过 `IInputRebinding` 查询、捕获、检查冲突、应用或恢复跳跃主绑定，不接触 GUIDE 类型。

角色控制、视角协调和收集判定属于具体玩法，保留在 `Demo3D` 业务层。`PlayerController` 只从 `IInputService` 读取输入，仍通过 Phantom C# Wrapper 修改第三人称旋转；InputService 与 CameraService 彼此不依赖。

## 输入链路

```text
键盘 / 鼠标 / 手柄
    ↓
G.U.I.D.E Action + GameplayContext.tres
    ↓
godo_guide_input（可选适配包）
    ↓
GoDo IInputService / InputFrame
    ↓
Demo3D PlayerController
    ↓
CharacterBody3D + Phantom Camera
```

沿着下面几个文件就能掌握完整流程：

1. `Input/Demo3DInput.cs`：游戏自己定义稳定的 Action / Context ID。
2. `Input/GameplayContext.tres`：配置 WASD、鼠标、摇杆和按钮的 GUIDE 映射、死区及灵敏度。
3. `Input/Demo3DInputProfile.tres`：把 GoDo Action / Context / Binding ID 对应到 GUIDE Resource 与可改键槽位。
4. `Boot/Boot.tscn`：启动时用 `GuideInputBackendInstaller` 安装一次后端，并在进入流程前加载已保存绑定。
5. `Gameplay/GameplayProcedure.cs`：进入玩法时启用 Gameplay Context。
6. `Gameplay/PlayerController.cs`：在 `_Process` 读取快照，在 `_PhysicsProcess` 消费移动与跳跃。
7. `Gameplay/GameplayHud.cs`：使用 GoDo 重绑定与持久化接口完成跳跃键查询、捕获、冲突提示、保存和恢复默认。
8. `Result/ResultProcedure.cs`：切到空的 Result Context，停用角色输入并释放鼠标。

要增加一个输入，顺序是：先在 `Demo3DInput.cs` 增加语义 ID，再创建 GUIDE Action 并加入 Context，随后加入 Profile，最后由业务脚本读取。不要让业务脚本直接读取 GUIDE Action，否则会绕过 GoDo 的后端边界。

改键与恢复默认都会立即写入独立的 `godo-input-bindings` SaveService 槽位；重启 Demo 后会恢复上次保存的结果。正式配置损坏时会尝试备份；正式配置与备份都不可用时上报错误并继续使用当前默认绑定。

## 插件边界

Demo3D 当前选择 GUIDE 和 Phantom Camera，但两者都位于 GoDo 核心之外：

- 换输入插件时，实现另一个 `IInputBackend`，`PlayerController` 的语义 ID 和读取方式可以保留。
- 换摄像机插件时，实现另一个 Camera Rig 适配，Gameplay Procedure 的镜头 ID 可以保留。
- `addons/godo_framework/` 不引用 GUIDE、GuideCs 或 Phantom Camera。
