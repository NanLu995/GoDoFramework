# 集成与扩展

GoDoFramework 的可选集成通过适配层连接第三方插件：框架保留自己的稳定业务接口，第三方插件仍由项目自行安装、升级和验收。不要修改第三方源码来适配框架；升级风险与平台兼容性应在项目侧验证。

Friflo ECS 集成是例外：GoDo 只提供场景级生命周期宿主，不抽象 Friflo 的 Entity、Component、Query 或 System API，因此使用该能力的业务代码会直接依赖 Friflo。

## G.U.I.D.E-CSharp：输入后端

使用 G.U.I.D.E-CSharp 时，GoDo 的 `InputService` 可以读取语义 Action、维护 Context、显示设备提示，并提供运行时改键与持久化能力。安装、Profile 和启动边界见[Input](../guides/input/index.md)，改键界面见[运行时改键](../guides/input-rebinding/index.md)。

没有安装该后端时，不应假设改键或设备提示能力存在；先通过接口的能力检测决定是否显示相关 UI。

## Phantom Camera：相机后端

使用 Phantom Camera 时，通过 `PhantomCameraRig` 将其注册到 `CameraService`，业务流程始终按稳定的 `CameraId` 激活或恢复镜头。具体节点配置、切换和失败边界见[Camera](../guides/camera/index.md)。

不使用 Phantom Camera 时，派生 `CameraRig` 并实现项目自己的后端适配器即可；业务代码不应直接依赖第三方镜头节点。

## Friflo ECS：场景级数据处理

Friflo ECS 适合大量同构实体的批量模拟，例如单位移动、投射物或状态效果；菜单、存档、场景切换、音频和普通 UI 继续使用 GoDo 服务或 Godot Node。核心包不依赖 Friflo，只有选择该能力的项目才需要安装独立适配包。

在目标项目的 `.csproj` 中添加 `Friflo.Engine.ECS` 3.6.0，再叠加 `GoDoFramework-FrifloEcs-<version>.zip` 并重新 restore/build。适配包不携带 NuGet 程序集，但包含上游 MIT 许可证。将 `EcsWorldHost` 放到需要 ECS 的业务场景，注册业务 System 和实体后显式设置 `IsRunning = true`；场景退出时 World 随宿主释放，不会自动跨场景保留。

业务组件和 System 应放在游戏自己的命名空间。`GoDo.Integrations.FrifloEcs` 只表示当前后端适配层，不承诺业务 ECS 代码可以无成本替换到另一套框架。

从组件、System 到业务场景的完整可运行步骤见[用 Friflo ECS 批量更新场景数据](../guides/friflo-ecs/index.md)。

## 接入前检查

1. 固定并记录第三方插件版本；不要把未验证的最新版当作框架前提。
2. 在 Godot 编辑器中确认插件、资源和节点类型可被识别，再安装对应 GoDo 后端。
3. 分别验证 Debug 与目标导出平台；第三方插件的编辑器可用不代表导出可用。
4. 升级任一方后，重新验证对应输入、相机或 ECS 最小场景，并查阅[故障排查](../troubleshooting/index.md)。

## 集成能力全景图

<div class="godo-capability-list">
<section><h4>安装 GUIDE Input 后端</h4><p>在 Runtime 中配置 Profile 与持久化槽位；业务继续依赖 IInputService。</p><pre class="godo-capability-call"><code>installer.Profile = inputProfile;
installer.PersistenceSlot = "input-bindings";</code></pre></section>
<section><h4>使用 GUIDE 扩展能力</h4><p>按能力检测取得改键、持久化和提示查询，不直接读取第三方 Action。</p><pre class="godo-capability-call"><code>input.TryGetRebinding(out IInputRebinding rebinding);
input.TryGetPromptQuery(out IInputPromptQuery prompts);</code></pre></section>
<section><h4>配置 Phantom Camera Rig</h4><p>设置后端节点和激活/停用优先级，由 CameraService 按 CameraId 切换。</p><pre class="godo-capability-call"><code>rig.PhantomCameraNode = pcam;
rig.ActivePriority = 20;
rig.InactivePriority = 0;</code></pre></section>
<section><h4>启动场景级 ECS</h4><p>注册业务 System 和实体后再开始更新；World 生命周期跟随宿主场景。</p><pre class="godo-capability-call"><code>host.Systems.Add(new MovementSystem());
host.Store.CreateEntity(new Position(), new Velocity());
host.IsRunning = true;</code></pre></section>
</div>

精确配置成员见 [GuideInputBackendInstaller API](xref:GoDo.GuideInput.GuideInputBackendInstaller)、[PhantomCameraRig API](xref:GoDo.PhantomCameraRig) 与 [EcsWorldHost API](xref:GoDo.Integrations.FrifloEcs.EcsWorldHost)。
