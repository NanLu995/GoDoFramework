# Demo3D

一个使用 GoDoFramework 的第三人称 3D 收集 Demo。

## 运行

确认项目已安装 `GoDoRuntime`，并通过顶部 `GoDo → GUIDE Input 设置...` 完成 GUIDE / GuideCs 安装和顺序检查后，在 Godot 中打开并运行：

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
- `DataTableService`：由 `BootProcedure` 显式加载 Base 数据集并报告逐表进度；框架启动本身不会自动读取业务数据。
- `Integrations/GuideInput`：把 GUIDE Action / Mapping Context 转换为 InputService 快照。
- Gameplay HUD：监听 `InputDeviceChangedEvent`，显示当前键鼠、手柄或触摸类别。
- Gameplay HUD：通过 `IInputRebinding` 查询、捕获、检查冲突、应用或恢复跳跃主绑定，不接触 GUIDE 类型。
- Gameplay HUD：右上角 Localization 验收面板通过 Settings 切换/保存语言，通过 Localization 查询动态文本和复数，并展示 Control 自动翻译、上下文、伪本地化与 RTL 状态。

角色控制、视角协调和收集判定属于具体玩法，保留在 `Demo3D` 业务层。`PlayerController` 只从 `IInputService` 读取输入，仍通过 Phantom C# Wrapper 修改第三人称旋转；InputService 与 CameraService 彼此不依赖。

## DataTable 用法

Demo3D 在 `BootProcedure.EnterAsync()` 中显式等待 `BaseDataTables.LoadAsync()`。运行 `Boot.tscn` 后，Godot Output 应依次显示 `0/3`、`1/3`、`2/3`、`3/3`。加载完成后，示例通过生成的 `ItemCategories`、`Items` 和 `Rewards` 表分别读取一条记录，输出表总数、名称、枚举、数值和可空外键等部分字段，再进入 Gameplay。

这段代码演示“业务决定何时加载，Service 负责校验、进度、缓存与失败语义”。Demo3D 不手动拼接 `.gdtb` 路径，也不直接调用底层 `DataTableLoader`。Base 数据集在整个 Demo 生命周期内保留，由 `GoDoRuntime` 退出清理。

## 输入链路

```text
键盘 / 鼠标 / 手柄
    ↓
G.U.I.D.E Action + GameplayContext.tres
    ↓
Integrations/GuideInput（可选适配包）
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

## Input 人工验收

进入 Gameplay 后按实际拥有的设备验证；没有对应硬件的项目不记为通过：

1. 使用键盘鼠标移动、旋转视角和跳跃，HUD 的输入设备应显示“键盘鼠标”，角色不应出现持续移动或重复跳跃。
2. 如有手柄，使用左右摇杆和 A 键完成相同操作；首次有效手柄输入后 HUD 应切换为“手柄”，再次操作键盘鼠标后应切回“键盘鼠标”。
3. 按住移动输入时切出窗口，松开按键或摇杆后再返回；角色不应保持失焦前的输入，也不应补发一次跳跃。
4. 快速点击和长按跳跃，确认一次按下只产生一次跳跃；移动输入应在渲染帧采样后由物理帧稳定消费，没有明显丢帧、重复边沿或方向滞留。
5. 释放鼠标后打开 Debugger 的 `运行时 / Input` 页面，确认后端已就绪、Frame 序号持续推进、Gameplay Context 有效，Move / Look / Jump 值与当前真实操作一致。
6. 完成 5 个能量核心并进入 Result 后，确认 Gameplay Context 已停用，角色不再响应移动或跳跃；点击“再玩一次”后输入恢复。
7. 使用 HUD 改绑跳跃键并重启 Demo3D，确认新绑定仍有效；恢复默认后再次重启，确认默认绑定恢复。

记录验收结果时应写明操作系统、键盘鼠标、手柄型号和是否覆盖窗口失焦；未实际连接的设备与平台继续保留为待验证。

## Localization 人工验收

进入 Gameplay 后使用右上角面板：

1. 点击 `English`、`Français`、`العربية`，确认标题、自动文本、动态文本、上下文按钮、复数和长文本立即切换。
2. 使用 `-` / `+` 检查 0、1、2 和更大数量的复数；阿拉伯语应覆盖多种复数形式。
3. 切换阿拉伯语后，状态中的 `RTL` 应为 `True`，语言按钮与复数行顺序应镜像，文字应正确塑形且无缺字方框。
4. 点击伪本地化按钮，检查自动文本和动态文本的扩展、重音与换行；再次点击关闭。
5. 点击 `Save locale`，完全退出并重新运行 `Boot.tscn`，确认进入 Gameplay 时恢复已保存语言。
6. 在 1280×720 及更小窗口检查右上面板没有遮挡、裁切或把操作按钮推出边界。

语言切换只更新内存，只有 `Save locale` 写入 Settings 固定槽位。伪本地化只影响当前进程，不持久化。

## Scheduler 人工验收

进入 Gameplay 后使用左下角面板：

1. 点击 `开始计数`，正常速度下三个计数应以接近相同节奏增长。
2. 点击 `慢动作 x0.25`，`游戏时间` 应明显变慢，`非缩放时间` 与 `真实时间` 保持原节奏；恢复后游戏速度应回到 `x1.00`。
3. 点击 `暂停场景`，角色和 `游戏时间` / `非缩放时间` 应暂停，只有 `真实时间` 继续；面板仍可点击 `恢复场景`。
4. 计数运行时切换窗口焦点、最小化数秒再恢复，确认界面可继续操作，三种计数符合各自时间语义。
5. 点击 `验证 Owner 清理`，状态应显示“Owner 清理通过”，被移出场景树的 Owner 不应触发回调。
6. 点击 `模拟卡顿 750 毫秒`，窗口会按预期短暂卡住；恢复后确认回调没有持续爆发，角色与面板仍可操作。
7. 打开 Debugger 的 `运行时 / Scheduler` 页面，确认活动数、三种时钟分布和取消计数与面板操作一致。

离开 Gameplay 时面板会取消自身任务，并恢复场景暂停和 `Engine.TimeScale`，不把验证状态带入后续流程。

## 插件边界

Demo3D 当前选择 GUIDE 和 Phantom Camera，但两者都位于 GoDo 核心之外：

- 换输入插件时，实现另一个 `IInputBackend`，`PlayerController` 的语义 ID 和读取方式可以保留。
- 换摄像机插件时，实现另一个 Camera Rig 适配，Gameplay Procedure 的镜头 ID 可以保留。
- `addons/godo_framework/` 不引用 GUIDE、GuideCs 或 Phantom Camera。
