# Config 使用指南

## 定位与边界

Config 为 Godot `Resource` 配置资产补充统一内容校验与只读键索引。资源定位、类型检查和 Godot 缓存仍由 ResourceHub 负责；Config 不建立第二套缓存，也不接入 GoDoRuntime 或 Services。

首版不包含 CSV/JSON 导入、目录扫描、热重载、自动代码生成、远程配置和编辑器导入器。

## 定义配置资源

```csharp
[GlobalClass]
public sealed partial class EnemyCatalog : Resource, IConfigResource
{
    [Export]
    public Godot.Collections.Array<EnemyDefinition> Entries { get; set; } = new();

    public void Validate()
    {
        if (Entries.Count == 0)
            throw new InvalidOperationException("敌人配置不能为空。");
    }
}
```

配置类型与字段属于具体游戏，不应放入 `GoDo.*` 命名空间。`Validate()` 应检查资源内部完整性并抛出带具体原因的异常，不应在其中加载其他资源、修改运行时状态或调用 ErrorHub 重复上报。

## 加载与校验

```csharp
ResourceKey key = ResourceKey.Create("res://Config/EnemyCatalog.tres");
EnemyCatalog catalog = ConfigHub.Load<EnemyCatalog>(key);
```

`ConfigHub.Load<T>()` 先调用 `ResourceHub.Load<T>()`，再调用配置的 `Validate()`：

- 路径不存在、加载失败或类型不匹配时抛出 `ResourceLoadException`。
- 内容校验失败时抛出 `ConfigValidationException`，并保留资源键、配置类型与原始异常。
- 方法不返回 null，也不在抛出前自行上报错误。

首版只提供同步加载，必须在 ResourceHub 已由 GoDoRuntime 初始化后从 Godot 主线程调用。大型配置确实出现异步加载需求后，再基于 ResourceHub 的异步操作补充，不使用 `Task.Run` 绕开 Godot 生命周期。

## 唯一键配置表

```csharp
var table = new ConfigTable<string, EnemyDefinition>(
    catalog.Entries,
    entry => entry.Id,
    StringComparer.Ordinal);

EnemyDefinition boss = table.Get("boss");
if (table.TryGet("slime", out EnemyDefinition? slime))
{
    // 使用配置
}
```

- 构造时一次性建立索引；空项、空键或重复键抛出 `ArgumentException`。
- `Get()` 对缺失键抛出 `KeyNotFoundException`；`TryGet()` 对缺失键返回 false。
- 表结构创建后不可增删，但不会深拷贝配置对象；调用方仍应把配置项视为只读数据。

## 生命周期与性能

- ConfigHub 不持有配置引用；调用方决定 Resource 和 ConfigTable 的持有周期。
- ConfigTable 构建为 O(n) 时间和 O(n) 额外索引内存，查询平均为 O(1)。
- 不要在 `_Process` 或 `_PhysicsProcess` 中重复加载配置或重建 ConfigTable，应在初始化阶段加载并缓存业务所需引用。

## 实现与验证状态

首版稳定基线完成。已在 Godot 运行时通过有效/无效 Resource、缺失资源、正常/缺失键查询和重复键验证；对应临时验证入口已在验收后移除。
