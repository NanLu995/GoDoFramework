# GoDo Phantom Camera 可选集成

## 定位

`godo_phantom_camera` 是 GoDo 与 Phantom Camera 的可选运行时适配包。它提供 `PhantomCameraRig` 和第三人称镜头预设。`CameraService` 仍由 GoDo 核心提供，本包不参与 `GoDoRuntime` 初始化，也不封装 Phantom Camera 的完整 API。

业务流程只通过 `ICameraService` 选择主镜头，`PhantomCameraRig` 将激活和停用转换为 Phantom Camera 优先级。镜头目标、鼠标环绕和其他玩法参数仍由业务场景或 Phantom Camera 自身负责。

## 依赖与安装

1. 安装并启用第三方 Phantom Camera；本适配包当前按 0.11 验证。
2. 复制 `addons/godo_phantom_camera/` 到项目。
3. 直接在业务场景中使用运行时 Rig 或预设。

本适配包与 `godo_guide_input` 一样不提供 `plugin.cfg`，因此不会出现在 Godot 插件列表，也不需要额外启用。只有第三方 Phantom Camera 自身需要启用。

## 第三人称 Rig 预设

预设位置：

```text
res://addons/godo_phantom_camera/ThirdPerson/GoDoPhantomThirdPersonRig.tscn
```

它包含：

```text
GoDoPhantomThirdPersonRig
（PhantomCameraRig，RigId = "gameplay"）
├─ MainCamera3D
│  └─ PhantomCameraHost
└─ ThirdPersonPcam
```

将其实例化到业务 3D 场景后，在 `ThirdPersonPcam` 的 Inspector 中指定 `Follow Target`，并为不同语义镜头设置不同 `RigId`。第三人称模式使用 Phantom 的 `SpringArm3D` 参数处理镜头距离、碰撞层与边距。

场景进入树后，Rig 自动注册并先写入停用优先级。业务流程显式激活：

```csharp
ICameraService cameras = Services.Get<ICameraService>();
cameras.ActivatePrimary(CameraId.Create("gameplay"));
```

业务 C# 如需驱动鼠标绕转，应直接使用 Phantom 自带的 C# Wrapper；不要在本集成包中复制其 API。

## Public API

```csharp
public sealed partial class PhantomCameraRig : CameraRig
{
    [Export] public Node3D PhantomCameraNode { get; set; }
    [Export] public int ActivePriority { get; set; }
    [Export] public int InactivePriority { get; set; }
}
```

- `PhantomCameraNode`：必须引用兼容 Phantom Camera 0.11 的 3D 节点。
- `ActivePriority`：CameraService 激活 Rig 时写入，必须大于停用值。
- `InactivePriority`：初始化和停用时写入，默认是 `0`。

停用优先级表示“让位给更高优先级 Rig”，不表示关闭 `Camera3D` 或 Phantom Camera Host。只有一个 Pcam 时，Phantom 仍可能把它当作自身当前候选；GoDo 的 `ActivePrimary` 才是业务层的逻辑激活状态。

## 失败语义

- 缺少 Phantom Camera C# API 时项目编译失败；第三方插件未启用或资源不完整时 Phantom 场景自身不可用。
- 本适配包不在编辑器启动时读取或比较 Phantom 版本；升级插件后应通过编译、自动回归和真实镜头场景重新验证。
- `PhantomCameraNode` 缺失、节点不兼容或激活优先级不大于停用优先级：Rig 在注册前抛出 `InvalidOperationException`。
- 运行期间 Phantom 优先级读写失败：由 CameraService 包装为 `CameraOperationException`，并遵循主镜头切换回滚语义。
- GoDo 核心与业务运行时不依赖本包；禁用或删除本包不会改变 `GoDoRuntime`。

## 生命周期、线程与性能

- Rig 在 `_Ready()` 验证后端、写入停用优先级并注册，在 `_ExitTree()` 注销。
- 激活、停用和配置访问都必须发生在 Godot 主线程。
- 适配器没有 `_Process()`，只在初始化和镜头切换时读写优先级，不增加每帧分配。
- Phantom Camera 自身的跟随、阻尼、碰撞和渲染成本不由适配器隐藏。

## 验证

1. 确认第三方 Phantom Camera 已启用；GoDo Phantom 适配包不需要启用项。
2. 运行 `Verification/Automated/PhantomCameraRigRegression.tscn`，确认 6/6 通过。
3. 实例化 Rig，设置跟随目标并通过 `ICameraService` 激活。
4. 确认镜头跟随、鼠标绕转和 `SpringArm3D` 避障正常。
