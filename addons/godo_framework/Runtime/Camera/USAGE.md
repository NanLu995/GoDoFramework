# CameraService 使用指南

## 定位

CameraService 为业务层提供主镜头 Rig 的语义注册、激活与恢复机制。业务流程只使用 `CameraId`，不直接持有 Phantom Camera 等具体后端类型。

核心只实现主镜头编排，不依赖具体插件。可选包 `addons/godo_framework/Integrations/PhantomCamera/` 已提供 Phantom Camera 优先级适配；小地图 `SubViewport` 输出、镜头能力查询与跟随目标设置仍属于后续分批交付，不属于当前 public API。

## 适用场景

- Gameplay、过场、瞄准等主镜头之间需要显式切换。
- 临时镜头结束后需要恢复最近一个仍然有效的主镜头。
- 主场景替换期间，新旧场景可能短暂存在相同语义 ID 的镜头 Rig。
- 希望业务流程与具体摄像机插件解耦。

## 非适用场景

- 角色移动和鼠标输入。
- 镜头阻尼、碰撞、构图与插件参数配置。
- 当前尚未实现的小地图、监控画面和分屏输出。
- 用于替代 Godot `Camera3D`、`SubViewport` 或第三方摄像机插件。

## 快速上手

业务项目定义稳定的镜头 ID：

```csharp
internal static class GameCameraIds
{
    public static readonly CameraId Gameplay = CameraId.Create("camera/gameplay");
    public static readonly CameraId Intro = CameraId.Create("camera/intro");
}
```

具体后端适配器继承 `CameraRig`，并在场景中配置 `RigId`。Rig 进入场景树后自动注册，退出场景树时按实例身份注销。

业务流程通过服务切换：

```csharp
ICameraService cameras = Services.Get<ICameraService>();
cameras.ActivatePrimary(GameCameraIds.Gameplay);
cameras.ActivatePrimary(GameCameraIds.Intro);
cameras.RestorePreviousPrimary();
```

## Public API

### CameraId

```csharp
public readonly struct CameraId
{
    public string Value { get; }
    public bool IsEmpty { get; }
    public static CameraId Create(string value);
}
```

ID 区分大小写，拒绝 null、空白和首尾空白。`default(CameraId)` 是无效值，不能用于服务调用。

### ICameraService

```csharp
public interface ICameraService
{
    CameraId? ActivePrimary { get; }
    void ActivatePrimary(CameraId id);
    bool RestorePreviousPrimary();
}
```

- `ActivatePrimary`：解析指定 ID 最后注册且仍有效的 Rig；目标激活成功、当前镜头停用成功后才提交状态。
- `RestorePreviousPrimary`：恢复最近一个仍注册且有效的具体 Rig 实例；不会把旧场景历史错误映射到新场景的同名 Rig。
- 重复激活当前具体 Rig 是无操作，不写入恢复历史。

### CameraRig

```csharp
public abstract partial class CameraRig : Node
{
    [Export] public string RigId { get; set; }
    protected virtual void OnRigReady();
    protected virtual void OnRigExitTree();
    protected abstract void ActivateRig();
    protected abstract void DeactivateRig();
}
```

派生适配器通过钩子解析后端节点，在 `ActivateRig` / `DeactivateRig` 中调用实际摄像机后端。不要重写 `_Ready()` 或 `_ExitTree()`；基类已固定注册与注销顺序。

## 切换与失败语义

1. 先激活目标 Rig；失败时保留当前镜头。
2. 再停用当前 Rig；失败时尝试停用目标以回滚，当前记录保持不变。
3. 两步成功后才更新当前镜头，并在 ID 不同时记录恢复历史。

- 未注册、失效、重复注册或后端激活/停用失败：抛出 `CameraOperationException`。
- 默认 `CameraId`：抛出 `ArgumentException`。
- 同一场景范围内重复 ID：注册失败。
- 场景替换时不同场景根短暂存在相同 ID：允许共存，最后注册的有效 Rig 优先。
- 回滚过程自身失败不会覆盖原始停用异常；适配器必须让停用操作可重复调用。

## 生命周期与线程

- CameraService 由 GoDoRuntime 创建并按 `ICameraService` 注册。
- 所有 public API 只能在 GoDoRuntime 记录的 Godot 主线程调用。
- Rig 在 `_Ready()` 注册，在 `_ExitTree()` 注销。
- 服务不在主场景切换事件后统一清空注册表，因为新场景会先进入树、旧场景才在帧末释放。
- GoDoRuntime 退出时清空当前镜头、恢复历史和注册表，不调用业务 Rig 的停用逻辑。

## 性能

CameraService 不包含 `_Process()`，不会每帧扫描镜头。注册、注销和切换属于低频操作；集合分配只发生在这些边界。具体后端的每帧跟随、碰撞与渲染成本不由本服务隐藏。

## 验证

自动回归入口：

```text
Verification/Automated/CameraServiceRegression.tscn
```

覆盖初始状态、首次激活、切换与恢复、重复激活、激活失败保持、停用失败回滚、同场景重复 ID、跨场景同 ID 替换、失效历史跳过和未知镜头失败。

Phantom Camera 优先级适配的自动回归入口是 `Verification/Automated/PhantomCameraRigRegression.tscn`。实际过渡、避障和鼠标环绕仍需在编辑器中手动验证；小地图输出尚未接入。
