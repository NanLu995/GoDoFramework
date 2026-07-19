# 创建、校验与查询强类型配置

ConfigHub 用 C# 类型表达游戏配置，并在资源加载后立即执行内容校验。策划和开发者仍在 Godot Inspector 中编辑 `.tres`，业务代码得到明确类型，不需要在运行过程中到处解析字典、字符串字段或 JSON 节点。

Config 只补充“内容是否完整”和“如何按唯一键查询”。资源路径、类型检查和 Godot Resource 缓存仍由 ResourceHub 负责，不会产生第二套缓存。

## 什么时候使用 Config

适合：

- 敌人、物品、关卡规则、数值曲线等随构建发布的静态配置。
- 希望在游戏启动或进入玩法前一次性发现缺字段、重复 ID 和非法数值。
- 运行时需要按稳定 ID 快速查询配置项。

不适合：

- 玩家存档和运行时设置；使用 SaveService 或 SettingsService。
- CSV/JSON 导入、在线配置、热更新或远程开关。
- 每帧变化的玩法状态。

## 1. 定义一项配置

```csharp
using Godot;

namespace MyGame.Config;

[GlobalClass]
public sealed partial class EnemyDefinition : Resource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export(PropertyHint.Range, "1,100000,1")]
    public int MaxHealth { get; set; } = 100;

    [Export(PropertyHint.Range, "0,10000,0.1")]
    public float MoveSpeed { get; set; } = 100f;
}
```

配置类型属于具体游戏，放在游戏命名空间，不放入 `GoDo.*`。ID 是存档、关卡和业务代码共享的稳定协议；不要从数组位置或显示名称临时生成。

## 2. 定义目录资源并实现校验

```csharp
using System;
using System.Collections.Generic;
using Godot;
using GoDo;

namespace MyGame.Config;

[GlobalClass]
public sealed partial class EnemyCatalog : Resource, IConfigResource
{
    [Export]
    public Godot.Collections.Array<EnemyDefinition> Entries { get; set; } = new();

    public void Validate()
    {
        if (Entries.Count == 0)
            throw new InvalidOperationException("敌人目录不能为空。");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < Entries.Count; i++)
        {
            EnemyDefinition? entry = Entries[i];
            if (entry == null)
                throw new InvalidOperationException($"第 {i} 项不能为空。");
            if (string.IsNullOrWhiteSpace(entry.Id))
                throw new InvalidOperationException($"第 {i} 项缺少 Id。");
            if (!string.Equals(entry.Id, entry.Id.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException($"Id '{entry.Id}' 包含首尾空格。");
            if (!ids.Add(entry.Id))
                throw new InvalidOperationException($"Id '{entry.Id}' 重复。");
            if (entry.MaxHealth <= 0)
                throw new InvalidOperationException($"'{entry.Id}' 的生命值必须大于 0。");
            if (entry.MoveSpeed < 0)
                throw new InvalidOperationException($"'{entry.Id}' 的移动速度不能为负数。");
        }
    }
}
```

`Validate()` 应检查资源内部能够判断的全部不变量，并给出能直接定位内容的原因。推荐检查：

- 必填项、空元素和首尾空格。
- 数值范围及字段之间的关系。
- 稳定 ID 唯一性。
- 业务枚举、资源引用和数组结构是否有效。

不要在 Validate 中修改配置、创建场景节点、发送业务事件或写存档。也不要在捕获后调用 ErrorHub；ConfigHub 会保留原始异常并交给调用边界处理。

## 3. 在 Inspector 创建配置资产

Godot 完成 C# 编译后，在 FileSystem 中创建 `EnemyCatalog` Resource，例如：

```text
res://Config/EnemyCatalog.tres
```

在 `Entries` 中添加 `EnemyDefinition` 子资源并填写稳定 ID。版本控制提交 `.tres` 和对应 `.uid`，让资源引用在移动文件时保持稳定。

Inspector 的 Range、枚举和资源类型限制可以减少输入错误，但它们不能代替运行时 `Validate()`。文本合并、脚本修改或资源迁移仍可能产生 Inspector 无法预防的无效组合。

## 4. 加载并在启动边界处理失败

```csharp
private static readonly ResourceKey EnemyCatalogKey =
    ResourceKey.FromPath("res://Config/EnemyCatalog.tres");

EnemyCatalog catalog;
try
{
    catalog = ConfigHub.Load<EnemyCatalog>(EnemyCatalogKey);
}
catch (ResourceLoadException exception)
{
    ErrorHub.Fatal(exception, "Game.Config", context: "EnemyCatalog load");
    ShowStartupFailure();
    return;
}
catch (ConfigValidationException exception)
{
    ErrorHub.Fatal(
        exception,
        "Game.Config",
        context: $"type={exception.ConfigType.Name} key={exception.Key.Value}");
    ShowStartupFailure();
    return;
}
```

两类失败表示不同问题：

- `ResourceLoadException`：路径不存在、资源无法加载或实际类型不匹配。
- `ConfigValidationException`：Resource 已加载，但 `Validate()` 拒绝内容；`InnerException` 保留具体原因。

ConfigHub 不返回 null，也不在抛出前重复上报。只在知道如何回退或停止启动的边界记录一次。

当前只有同步加载，必须在 ResourceHub 已初始化后从 Godot 主线程调用。不要用 `Task.Run` 包装 Resource 加载。

## 5. 建立唯一键查询表

在加载并校验后一次性建立索引：

```csharp
var enemies = new ConfigTable<string, EnemyDefinition>(
    catalog.Entries,
    entry => entry.Id,
    StringComparer.Ordinal);
```

必需条目直接查询：

```csharp
EnemyDefinition boss = enemies.Get("boss");
```

确实允许缺失时：

```csharp
if (enemies.TryGet("tutorial_dummy", out EnemyDefinition? dummy))
    Spawn(dummy);
```

构造 ConfigTable 时，空元素、null 键和重复键会立即抛出 `ArgumentException`。`Get()` 缺失时抛 `KeyNotFoundException`；`TryGet()` 返回 `false`。

字符串 ID 通常使用 `StringComparer.Ordinal`，保持大小写敏感且不受系统语言影响。只有业务协议明确规定忽略大小写时才使用其他比较器，并确保存档和工具链采用同一规则。

## 6. 持有配置而不是每帧重载

```csharp
public sealed class GameConfig
{
    public EnemyCatalog EnemyCatalog { get; }
    public ConfigTable<string, EnemyDefinition> Enemies { get; }

    public GameConfig(EnemyCatalog catalog)
    {
        EnemyCatalog = catalog;
        Enemies = new ConfigTable<string, EnemyDefinition>(
            catalog.Entries,
            entry => entry.Id,
            StringComparer.Ordinal);
    }
}
```

ConfigHub 不持有第二份引用，ConfigTable 也不会深拷贝条目。由游戏启动层决定持有周期，并把加载后的配置视为只读数据。

ConfigTable 构建需要 O(n) 时间和 O(n) 索引内存，平均查询为 O(1)。不要在 `_Process()`、每次生成敌人或每次打开面板时重新加载配置和构建索引。

## 7. 修改配置后的工作流

当前版本不支持热重载。修改 `.tres` 或配置 C# 类型后：

1. 等待 Godot 完成资源扫描和 C# 编译。
2. 重新启动相关测试或游戏会话。
3. 让启动加载重新执行 Validate。
4. 检查引用该 ID 的场景、存档迁移和业务代码。

删除或重命名稳定 ID 属于游戏数据兼容性变更。ConfigHub 能发现当前文件内部错误，但不会自动迁移旧存档或其他资源中的引用。

## 常见错误

- 配置加载失败：路径错误、文件未提交、实际 Resource 类型不匹配或 ResourceHub 尚未初始化。
- Validation 只提示“无效”：异常缺少条目索引、ID 和字段原因，导致内容人员无法定位。
- 重复错误日志：Validate 或底层先上报，启动边界又上报；只在最终处理边界记录一次。
- 查询偶尔失败：ID 大小写或首尾空格规则不一致。
- 修改配置后运行中没有变化：首版不支持热重载，需要重新加载会话。
- ConfigTable 创建后条目仍被修改：索引不深拷贝对象，业务必须把配置视为只读。
- 每帧出现分配：在热路径重复构造 ConfigTable，应在初始化阶段缓存。

精确接口可查询 <xref:GoDo.ConfigHub>、<xref:GoDo.IConfigResource>、<xref:GoDo.ConfigTable%602> 和 <xref:GoDo.ConfigValidationException>。
