# GoDo Friflo ECS 可选集成

## 定位

`Integrations/FrifloEcs` 为需要数据导向批量处理的业务场景提供 Friflo ECS 场景级宿主。它不属于 GoDo 核心，不注册为长期服务，也不改变 `GoDoRuntime`。菜单、存档、场景切换、音频等不适合 ECS 的能力继续使用现有 GoDo 模块或 Godot Node。

本集成只负责 World 生命周期与 Godot 帧阶段驱动，不包装 Friflo 的 Entity、Component、Query、System 或序列化 API。游戏组件和系统必须使用业务命名空间，例如 `MyGame.Combat.Ecs`；适配层使用 `GoDo.Integrations.FrifloEcs`。

## 依赖与安装

当前按稳定版 `Friflo.Engine.ECS 3.6.0` 验证。目标 Godot C# 项目必须显式添加：

```xml
<ItemGroup>
  <PackageReference Include="Friflo.Engine.ECS" Version="3.6.0" />
</ItemGroup>
```

依赖的官方版本页是 [NuGet：Friflo.Engine.ECS 3.6.0](https://www.nuget.org/packages/Friflo.Engine.ECS/3.6.0)。编辑器检查页提供该入口，但不会自行下载程序包；无论手工还是由工具确认写入 `PackageReference`，程序集都由开发者随后执行的 restore/build 获取。

然后叠加 `GoDoFramework-FrifloEcs-<version>.zip`（或复制 `addons/godo_framework/Integrations/FrifloEcs/`），再重新执行 restore/build。独立发布包包含适配源码与上游 MIT `LICENSE`，不内置 NuGet 程序集。

启用 GoDo EditorPlugin 后，可在统一窗口的“编辑器扩展”页打开“Friflo ECS 依赖检查...”。检查器读取项目根目录的 `.csproj` 与可选 `Directory.Packages.props`，识别直接 `PackageReference` 和中央 `PackageVersion`；缺少引用、版本不符、MSBuild 变量、多个项目或损坏 XML 都会明确报告。

仅当根目录恰好存在一个普通 `.csproj`、依赖确实缺失且未检测到中央包管理时，“添加依赖...”才可用。确认窗口会显示目标文件与待写入的精确 `PackageReference`；写入前在同目录创建不覆盖旧文件的 `.godo-backup` 备份，已有备份时递增编号，并在写入后重新检查。工具不执行 restore/build，也不自动处理中央包管理、多个项目、变量版本、条件引用或损坏 XML；这些情况保持只读并交由项目维护者处理。

Friflo ECS 采用 MIT License。发布含该程序集的游戏或插件时，应随分发产物保留对应许可证文本。GoDo 不复制或修改 Friflo 源码。

## 场景接入

将 `EcsWorldHost` 添加到实际需要 ECS 的业务场景。节点进入场景树时创建独立 `EntityStore` 和 `SystemRoot`，退出场景树时停止更新并释放宿主持有的引用。

```csharp
using GoDo.Integrations.FrifloEcs;

public override void _Ready()
{
    EcsWorldHost host = GetNode<EcsWorldHost>("EcsWorldHost");
    host.Systems.Add(new MovementSystem());
    host.Store.CreateEntity(new Position(), new Velocity());
    host.IsRunning = true;
}
```

`UpdatePhase` 必须在节点进入树前配置：

- `Process`：在 `_Process()` 更新，适合非物理模拟；
- `Physics`：在 `_PhysicsProcess()` 更新，适合固定步长模拟。

一个宿主只驱动一个阶段，避免同一系统在同一帧被重复执行。需要不同阶段时，应使用两个边界清晰、互不共享 World 的宿主；首版不提供跨阶段调度器。

## Public API

```csharp
public sealed partial class EcsWorldHost : Node
{
    [Export] public EcsUpdatePhase UpdatePhase { get; set; }
    [Export] public bool IsRunning { get; set; }
    public EntityStore Store { get; }
    public SystemRoot Systems { get; }
    public bool IsInitialized { get; }
    public void Shutdown();
}
```

- `Store` 和 `Systems` 只在节点位于场景树且尚未关闭时可用；否则抛出 `InvalidOperationException`。
- `IsRunning` 默认是 `false`，避免尚未注册业务 System 时产生空转；添加完 System 后必须显式设为 `true`。
- `IsRunning = false` 会关闭对应 Godot Process，只暂停系统更新，不删除实体或替换 World。
- `Shutdown()` 幂等，停止两种 Godot Process 并释放宿主持有的 World 与 SystemRoot 引用。
- 节点退出后重新进入场景树会创建全新的 World，旧 Entity 不得继续使用。

## 生命周期、线程与失败语义

- World 由宿主节点拥有，不通过 `Services` 暴露，也不自动跨场景保留。
- Friflo `SystemRoot` 3.6.0 不实现 `IDisposable`；关闭依靠停止帧驱动并释放宿主持有的引用。
- Godot Node、Resource、PhysicsServer 等引擎对象只能在 Godot 主线程适配层访问。并行 Query 只处理线程安全的纯 ECS 数据。
- Query 中的结构变更必须使用 Friflo `CommandBuffer`，不能直接增加、删除实体或组件。
- System 抛出的异常不会被宿主吞掉，由 Godot 调用边界继续暴露，避免形成不可见的半更新状态。
- 场景树暂停遵循 Godot Node 的 `ProcessMode`；本集成不覆盖业务节点的暂停策略。

## 性能与当前边界

宿主只在 `IsRunning = true` 时启用所选 Godot Process；每个活动帧创建一个值类型 `UpdateTick` 并调用一次 `SystemRoot.Update()`。实际分配和耗时主要由业务 System、Query、结构变更及实体规模决定。

2026-08-13 Windows、Godot 4.7.1 Mono Headless、.NET 8、Debug、20 个逻辑处理器的单线程 Position/Velocity 查询基准中，1 万实体连续更新 1,000 次平均约 0.044 ms/次，10 万实体连续更新 100 次平均约 0.338 ms/次，两档稳态当前线程托管分配均为 0 B。该数据只代表当前机器的简单纯数据系统，不包含 Godot Node 同步、物理查询、结构变更或渲染，不能作为跨设备性能保证。

Debug 构建启用本集成后，GoDo Debugger 会增加 `运行时 / ECS` 页面。页面汇总有效 `EcsWorldHost`、运行状态、Entity、Archetype 和容量，并显示 System 层级、启用状态、Query Entity 数量，以及可用的最近耗时、更新次数和托管分配。页面只读，不遍历 Entity，不保留历史，最多显示 32 个 World 和 128 个 System。

Friflo 的 System 性能监控有自身运行成本，因此 Debugger 不会调用 `SetMonitorPerf(true)`。需要查看性能列时，由业务在初始化 System 后显式调用 `host.Systems.SetMonitorPerf(true)`；未开启时页面用 `—` 表示，而不是改变运行状态。

`Friflo.EcGui` 主要面向 ImGui/.NET 桌面工具链中的深度 Entity/Component 检查。Godot 要嵌入它还需要额外 GUI 后端、渲染与输入桥接，并承担第三方升级兼容，当前不作为 GoDo 集成依赖。日常运行状态使用 GoDo Debugger；需要逐 Entity/Component 深挖时使用 IDE 调试器或独立 Friflo 工具。两者职责有交集但不等价，本集成不重复实现实体编辑器。

当前首版已完成。暂不提供多 World 管理、存档封装、编辑器实体检查器、自动 System 发现或复杂 `.csproj` 修改。

## 验证

- Friflo 开启：`dotnet build GoDoFramework.csproj -c Debug -p:GoDoIncludeFrifloEcs=true`。
- 核心隔离：使用 `-p:GoDoIncludeFrifloEcs=false` 编译，确认核心不依赖 Friflo。
- 生命周期与诊断：运行 `Verification/Automated/FrifloEcsRegression.tscn`，覆盖 Process、Physics、暂停、幂等关闭、重新进入场景树、Debug 注册与快照、性能读取和 Debugger ECS 页面渲染。
- 性能：运行 `Verification/Performance/FrifloEcsBenchmark.tscn`，覆盖 1 万/10 万实体稳态更新与当前线程托管分配。
- 项目依赖：运行 `Verification/Automated/FrifloEcsProjectDependencyRegression.gd`，覆盖直接引用、中央包版本、缺失/错误/变量版本、多个项目、条件引用、损坏 XML、确认安装、非覆盖备份、重复安装拒绝与中央管理写入拦截；`EditorExtensionUiRegression.gd` 验证统一窗口入口、安装按钮和确认预览。
- Android、其他桌面平台、导出包和大规模实体性能仍待后续验证。
