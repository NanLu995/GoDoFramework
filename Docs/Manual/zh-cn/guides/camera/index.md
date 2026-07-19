# 配置、切换与恢复主镜头

CameraService 用稳定的业务 ID 管理当前主镜头。流程代码只表达“切到 Gameplay 镜头”或“恢复上一镜头”，不直接依赖 Phantom Camera 类型。具体的跟随、阻尼、碰撞和构图仍由实际摄像机后端负责。

核心 CameraService 不提供可直接拍摄的 Camera3D。你需要使用框架的 Phantom Camera 可选集成，或自行编写一个 `CameraRig` 适配器。

## 什么时候使用 CameraService

适合：

- Gameplay、过场、瞄准和检查物品等主镜头需要显式切换。
- 临时镜头结束后，需要恢复最近一个仍然存在的镜头。
- 希望 Procedure 不依赖具体摄像机插件。

不适合：

- 小地图、监控画面和分屏等多个同时输出的视口。
- 角色移动、鼠标输入或镜头环绕规则。
- 替代 Godot `Camera3D` 或 Phantom Camera 自身的跟随与避障能力。

## 1. 安装 Phantom Camera 可选集成

目标项目需要：

```text
addons/godo_framework/
addons/phantom_camera/
addons/godo_framework/Integrations/PhantomCamera/
```

启用唯一的 **GoDo Framework** 插件，然后打开：

```text
GoDo → Phantom Camera 设置...
```

设置窗口会检查第三方插件文件与版本。当前适配器按 Phantom Camera 0.11 验证；版本不一致不代表一定不可用，但升级后必须重新编译并验证真实镜头场景。只有第三方 Phantom Camera 需要在 Godot 插件列表中启用，GoDo 适配包不是第二个 EditorPlugin。

## 2. 把第三人称 Rig 放入场景

将以下预设实例化到 Gameplay 3D 场景：

```text
res://addons/godo_framework/Integrations/PhantomCamera/ThirdPerson/GoDoPhantomThirdPersonRig.tscn
```

预设结构为：

```text
GoDoPhantomThirdPersonRig（PhantomCameraRig）
├─ MainCamera3D
│  └─ PhantomCameraHost
└─ ThirdPersonPcam
```

在 Inspector 中完成这些设置：

1. 将根节点 `RigId` 设为 `camera/gameplay`。
2. 在 `ThirdPersonPcam` 中设置 `Follow Target`。
3. 按游戏需求配置距离、阻尼、碰撞层与 `SpringArm3D` 边距。
4. 保持 `ActivePriority` 大于 `InactivePriority`；默认值分别为 20 和 0。

Rig 进入场景树时会验证后端、先写入停用优先级，然后自动注册。不要另写脚本重复注册，也不要通过手工开关 `Camera3D` 来模拟 CameraService 状态。

## 3. 定义稳定的镜头 ID

创建 `res://Camera/GameCameraIds.cs`：

```csharp
using GoDo;

namespace MyGame;

public static class GameCameraIds
{
    public static readonly CameraId Gameplay =
        CameraId.Create("camera/gameplay");
    public static readonly CameraId Intro =
        CameraId.Create("camera/intro");
    public static readonly CameraId Inspect =
        CameraId.Create("camera/inspect");
}
```

ID 区分大小写，不能是空白或带有首尾空格。场景中的 `RigId` 必须与代码完全一致。ID 表达业务含义，不使用节点路径、场景文件名或数组下标。

## 4. 从 Procedure 激活主镜头

Gameplay 场景加载完成、Rig 已进入场景树后再激活：

```csharp
ICameraService cameras = context.GetService<ICameraService>();
cameras.ActivatePrimary(GameCameraIds.Gameplay);
```

过场开始时切换：

```csharp
cameras.ActivatePrimary(GameCameraIds.Intro);
```

重复激活当前同一个 Rig 是无操作，不会污染恢复历史。`ActivePrimary` 表示业务层认定的逻辑主镜头，不等同于 Phantom Camera 内部当前挑选的候选节点。

## 5. 结束临时镜头并恢复

检查物品或短过场结束时：

```csharp
if (!cameras.RestorePreviousPrimary())
    cameras.ActivatePrimary(GameCameraIds.Gameplay);
```

`RestorePreviousPrimary()` 会跳过已经退出场景树的历史实例。它按具体 Rig 实例恢复，不会因为新场景中出现同名 ID，就把旧场景历史错误映射到新 Rig。

恢复栈适合严格嵌套的临时切换。长流程进入新状态时，仍应显式激活该流程的基础镜头，不要假设历史中一定存在正确目标。

## 6. 理解切换失败时会发生什么

CameraService 按以下顺序切换：

1. 先激活目标 Rig；失败时保留当前镜头。
2. 再停用当前 Rig；失败时尝试停用新目标以回滚。
3. 两步成功后才更新 `ActivePrimary` 和恢复历史。

因此调用方不会看到“记录已经切换，但实际后端只完成一半”的正常返回。未知 ID、失效 Rig 或后端激活/停用失败会抛出 `CameraOperationException`；无效的默认 `CameraId` 会抛出 `ArgumentException`。在 Procedure 的进入错误边界统一处理这些失败，不要静默吞掉。

## 7. 不使用 Phantom Camera 时编写适配器

如果项目只使用 Godot `Camera3D`，可以用一个很薄的 `CameraRig` 把它接入切换服务：

```csharp
public sealed partial class GodotCameraRig : CameraRig
{
    [Export] public Camera3D BackendCamera { get; set; } = null!;

    protected override void OnRigReady()
    {
        if (!IsInstanceValid(BackendCamera))
            throw new InvalidOperationException("BackendCamera 未配置。");
    }

    protected override void ActivateRig() => BackendCamera.MakeCurrent();

    protected override void DeactivateRig() => BackendCamera.ClearCurrent();
}
```

把脚本挂到场景节点，配置 `RigId` 和 `BackendCamera`。`CameraRig` 会在 `_Ready()` 后自动注册，并在退出场景树时按实例注销；派生类不要自行重复注册。更复杂的摄像机插件也采用相同边界：在 `OnRigReady()` 验证后端引用，在激活和停用方法中只调用该插件的切换 API。

## 场景切换时的注意事项

Godot 可能先让新场景进入树，再在帧末释放旧场景。CameraService 允许不同场景根短暂注册相同 ID，并优先解析最后注册且仍有效的 Rig；同一场景根中重复 ID 则会失败。

推荐顺序是：

1. 通过 SceneService 完成新场景切换。
2. 等新场景和 CameraRig 完成 `_Ready()`。
3. 显式激活新流程的基础镜头。
4. 不再访问即将释放的旧 Rig 或后端节点。

## 常见错误

- 找不到镜头：代码 ID 与 Inspector 的 `RigId` 不一致，或激活发生在 Rig `_Ready()` 之前。
- Rig 注册失败：同一场景根存在重复 ID。
- Phantom Rig 初始化失败：`PhantomCameraNode` 缺失、节点不兼容，或激活优先级不大于停用优先级。
- 停用后画面仍由该 Pcam 输出：停用只是降低优先级；只有一个候选时，Phantom Camera 仍可能选择它。
- 恢复返回 `false`：历史为空，或历史中的 Rig 都已经离开场景树。
- 镜头能切换但跟随/避障异常：这是 Phantom Camera 配置或业务环绕逻辑，不是 CameraService 的切换职责。

精确接口可查询 <xref:GoDo.ICameraService>、<xref:GoDo.CameraId>、<xref:GoDo.CameraRig>、<xref:GoDo.CameraOperationException> 和 <xref:GoDo.PhantomCameraRig>。
