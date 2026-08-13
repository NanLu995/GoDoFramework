# 用 Friflo ECS 批量更新场景数据

本教程从一个空业务场景开始，创建 1,000 个只存在于 ECS World 中的移动实体，并由 `EcsWorldHost` 在 Godot 的 Process 阶段更新它们。运行后输出窗口会出现一次 `[ECS] 首次更新 1000 个实体`，用来确认 World、System 和帧驱动已经连通。

这条线路适合单位移动、投射物、状态效果等大量同构数据。菜单、存档、场景切换、音频、普通 UI，以及数量很少且主要依赖节点树行为的对象，继续使用 GoDo 服务或 Godot Node 更直接。

## 1. 安装可选集成

先安装 GoDo 核心包，再把 `GoDoFramework-FrifloEcs-<version>.zip` 叠加到项目根目录。压缩包保留 `addons/godo_framework/Integrations/FrifloEcs/` 路径，只包含适配源码和上游 MIT 许可证，不包含 NuGet 程序集。

在目标项目的 `.csproj` 中加入已验证版本：

```xml
<ItemGroup>
  <PackageReference Include="Friflo.Engine.ECS" Version="3.6.0" />
</ItemGroup>
```

可在 [NuGet 官方版本页](https://www.nuget.org/packages/Friflo.Engine.ECS/3.6.0)核对 `3.6.0` 与精确 `PackageReference`。依赖检查窗口的“查看 NuGet 包...”只打开该页面；工具本身不会下载程序集，程序包仍由后续 restore/build 获取。

也可以先在 `GoDo Framework → 打开 GoDo Framework... → 编辑器扩展` 中运行“Friflo ECS 依赖检查...”。当根目录只有一个普通 `.csproj`、依赖缺失且没有 `Directory.Packages.props` 时，“添加依赖...”按钮可用。确认窗口会显示实际项目文件和上述精确 `PackageReference`；确认后先在同目录创建不覆盖旧文件的 `.godo-backup` 备份，再写入并重新检查。已有备份时文件名会递增编号。

无论手工还是由工具写入，之后都要自行执行 restore/build 并重新打开 Godot；工具不会启动构建或下载依赖。

如果项目已经集中管理 NuGet 版本，应遵循项目自己的 `Directory.Packages.props` 或构建约定，不要重复声明版本。多个 `.csproj`、中央包管理、MSBuild 变量版本、条件引用和损坏 XML 都保持只读；检查器会显示原因，不会猜测或改写复杂项目结构。

## 2. 定义纯数据组件

在业务目录创建 `Position.cs` 和 `Velocity.cs`。组件属于游戏，不属于 `GoDo.*`：

```csharp
using Friflo.Engine.ECS;

namespace MyGame.Simulation.Ecs;

public struct Position : IComponent
{
    public Position(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X;
    public float Y;
}

public readonly struct Velocity : IComponent
{
    public Velocity(float x, float y)
    {
        X = x;
        Y = y;
    }

    public readonly float X;
    public readonly float Y;
}
```

组件只保存批量计算需要的数据，不持有 Godot Node、Resource 或场景对象。需要把结果显示到节点时，在明确的主线程同步边界读取 ECS 数据；不要让并行 Query 操作 Godot 对象。

## 3. 编写批量更新 System

创建 `MovementSystem.cs`：

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

namespace MyGame.Simulation.Ecs;

public sealed class MovementSystem : QuerySystem<Position, Velocity>
{
    private bool _reportedFirstUpdate;

    protected override void OnUpdate()
    {
        Query.ForEachEntity(static (
            ref Position position,
            ref Velocity velocity,
            Entity entity) =>
        {
            position.X += velocity.X;
            position.Y += velocity.Y;
        });

        if (_reportedFirstUpdate)
            return;

        _reportedFirstUpdate = true;
        GD.Print($"[ECS] 首次更新 {EntityCount} 个实体");
    }
}
```

示例按每次更新增加一个速度单位，重点是验证批量 Query。真实游戏如果需要按秒速度，可在 System 中使用 `Tick.deltaTime`；物理相关模拟应把宿主配置为 Physics 阶段。

Query 遍历期间不要直接创建、删除 Entity 或增删组件。需要结构变更时使用 Friflo `CommandBuffer`，在安全边界统一回放。

## 4. 创建场景并启动 World

创建 `BatchMovementDemo.tscn`：

```text
BatchMovementDemo (Node，挂载 BatchMovementDemo.cs)
└── EcsWorldHost (EcsWorldHost)
```

在 Inspector 中把 `EcsWorldHost.UpdatePhase` 保持为 `Process`。然后给根节点挂载：

```csharp
using Friflo.Engine.ECS;
using Godot;
using GoDo.Integrations.FrifloEcs;

namespace MyGame.Simulation.Ecs;

public partial class BatchMovementDemo : Node
{
    private EcsWorldHost? _host;

    public override void _Ready()
    {
        _host = GetNode<EcsWorldHost>("EcsWorldHost");
        _host.Systems.Add(new MovementSystem());

        for (int index = 0; index < 1_000; index++)
        {
            _host.Store.CreateEntity(
                new Position(index, 0f),
                new Velocity(1f, 0f));
        }

        _host.IsRunning = true;
    }
}
```

运行场景。输出一次 `[ECS] 首次更新 1000 个实体` 即表示：宿主已经创建 World、System 查询匹配全部实体，并由 Godot 帧阶段开始驱动。

`IsRunning` 默认是 `false`。先注册 System 和初始实体，再显式启动，避免空 World 每帧更新。宿主进入树后不能更改 `UpdatePhase`；需要 Physics 时应在场景资源或 `AddChild()` 前配置。

### 查看 Demo3D 的可见同步实例

仓库内 `Templates/Demo3D/Gameplay/EcsSwarmDemo.cs` 展示了 ECS 与 Godot 渲染的完整边界：Friflo World 固定管理 512 个位置/速度实体，一个 `MultiMeshInstance3D` 负责全部可见实例，不为每个 Entity 创建 Node。宿主以更早的 Process 优先级先更新数据，业务控制器随后在主线程把位置同步到 MultiMesh。

运行 `Templates/Demo3D/Boot/Boot.tscn` 并进入 Gameplay，可在场地右侧看到蓝色群体和运行状态标签。暂停会同时停止 ECS System 与可视同步；退出 Gameplay 后 World 销毁，再次进入时创建新 World。512 是普通展示的固定预算，1 万/10 万实体规模仍由独立性能基准验证。

## 5. 暂停、恢复与退出场景

暂停和恢复不会替换 World：

```csharp
public void SetSimulationPaused(bool paused)
{
    if (_host is not null)
        _host.IsRunning = !paused;
}
```

设置为 `false` 后，对应的 Godot Process 会关闭，不再产生空转；已有 Entity 仍保留。恢复后从同一个 World 继续更新。

当宿主退出场景树时，它会停止更新并释放自己持有的 `EntityStore` 和 `SystemRoot` 引用；再次进入树会创建全新的 World。因此：

- 不要把旧 `Entity` 或 `EntityStore` 缓存在跨场景服务中。
- 不要把 `EcsWorldHost` 注册进 `Services` 形成隐藏的长期所有权。
- 需要跨场景保留的数据应转换为明确的存档或业务状态，再在新 World 中重建。
- 手工提前终止时可以调用幂等的 `Shutdown()`；关闭后继续访问 `Store` 或 `Systems` 会抛出 `InvalidOperationException`。

## 6. 选择 Process 还是 Physics

- `Process`：视觉模拟、非物理单位逻辑，以及不要求固定步长的批处理。
- `Physics`：需要固定步长并与 Godot 物理时序协作的模拟。

一个宿主只运行一个阶段。不要为了同时使用两种回调而让同一个 World 每帧更新两次。确实需要两种阶段时，先划清数据所有权，再使用两个互不共享 World 的宿主。

## 常见问题

### 场景运行但 System 没有执行

确认已经在注册 System 和实体后设置 `host.IsRunning = true`，并确认当前场景树没有因暂停策略阻止该节点处理。

### 在 `_Ready()` 中修改 `UpdatePhase` 抛出异常

`_Ready()` 时宿主已经进入树并完成初始化。请在 Inspector 设置，或在代码创建宿主时先赋值 `UpdatePhase`，再调用 `AddChild()`。

### 是否应该把角色 Node 全部替换成 Entity

不应该默认替换。先用 ECS 承担被性能测量证明适合批处理的数据；动画、输入、碰撞节点和 UI 可以保留在 Godot 一侧，通过少量、明确的同步边界连接。

### 将来更换 ECS 框架是否无需修改业务代码

核心 GoDo 模块和场景宿主边界不会反向依赖 Friflo，但组件、System、Query 和 Entity 操作直接使用 Friflo API。保持这些代码集中在业务 ECS 命名空间可以控制迁移范围，不能消除迁移成本。

安装与后端边界见[集成与扩展](../../integrations/index.md)；宿主精确签名见 [EcsWorldHost API](xref:GoDo.Integrations.FrifloEcs.EcsWorldHost)。
