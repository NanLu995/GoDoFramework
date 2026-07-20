# 从 CSV 生成可校验的数据表

DataTable 是开发期编译工具：它读取 UTF-8 CSV 和 DataTable Schema，检查类型、主键、范围与跨表外键，然后生成二进制 `.gdtb`、Manifest 和强类型 C# 读取代码。游戏运行时读取生成物，不解析原始 CSV 或 Schema。

> [!IMPORTANT]
> DataTable 当前仍是实验功能，尚未标记为稳定基线。生成格式、生成代码名称和工作流可能继续调整。适合试用和验证，不建议在未锁定版本与回归流程前承载不可迁移的正式数据生产线。

## 什么时候使用 DataTable

适合：

- 大量同结构行数据更适合在表格软件中编辑。
- 需要主键、数值范围、枚举和跨表引用校验。
- Client 和 Server 需要从同一源数据生成不同发布子集。
- 希望 CI 检查生成物是否与 CSV 一致。

不适合：

- 少量、层级复杂且依赖 Godot Resource 引用的配置；优先使用 ConfigHub。
- 运行时编辑、远程配置、热更新和电子表格在线同步。
- 加密、防篡改或网络协议；Manifest 摘要只用于发现不一致。

## 1. 建立目录

推荐结构：

```text
DataTables/
└─ Base/
   ├─ .datatable.schema.json
   ├─ .datafiles/
   │  ├─ .gdignore
   │  ├─ ItemCategories.csv
   │  └─ Items.csv
   ├─ Runtime/
   │  ├─ ItemCategory.gdtb
   │  ├─ Item.gdtb
   │  └─ manifest.json
   └─ BaseDataTables.g.cs
```

通过 `GoDo → DataTable...` 维护 Schema 和 `.datafiles`。`Runtime` 与 `BaseDataTables.g.cs` 是工具输出，不应手工编辑；推荐提交生成物，让新拉取的项目可直接编译，并让 CI 验证它们没有过期。Schema 和 `.datafiles` 不进入最终游戏包。

输入 CSV 使用 UTF-8，可带 BOM。列名必须与 Schema 字段名一致。

## 2. 编写 CSV

`ItemCategories.csv`：

```csv
id,display_name,sort_order,enabled
weapon,Weapon,10,true
consumable,Consumable,20,true
```

`Items.csv`：

```csv
id,category_id,display_name,enabled,max_stack,weight,rarity,description
iron_sword,weapon,Iron Sword,true,1,3.5,Common,A basic sword
health_potion,consumable,Health Potion,true,20,0.2,Uncommon,Restores health
```

稳定 ID 区分大小写。不要把本地化显示文字当作 ID；正式项目通常让表中保存翻译键，再由 LocalizationService 显示玩家文本。

## 3. 用 Schema 编辑器声明结构

打开 `GoDo → DataTable...`，选择 `.datatable.schema.json` 后点击“编辑 Schema...”。数据文件面板会显示已加入、已排除和缺失的 CSV；可以读取未加入 CSV 的表头创建初始字段，再在界面中设置真实类型、默认值、范围、主键和外键。以下 JSON 仅用于解释保存结果，不要求手工编辑：

```json
{
  "format_version": 2,
  "data_set_id": "game.items",
  "protocol_version": 1,
  "namespace": "MyGame.DataTables.Base",
  "source_directory": ".datafiles",
  "output_directory": "Runtime",
  "csharp_output": "BaseDataTables.g.cs",
  "tables": [
    {
      "id": "ItemCategory",
      "source": "ItemCategories.csv",
      "schema_version": 1,
      "audience": "Shared",
      "primary_key": "id",
      "fields": [
        { "name": "id", "type": "string", "required": true, "min_length": 1, "max_length": 64 },
        { "name": "display_name", "type": "string", "required": true, "min_length": 1, "max_length": 128 },
        { "name": "sort_order", "type": "int32", "required": true, "min": 0, "max": 10000 },
        { "name": "enabled", "type": "bool", "required": false, "default": true }
      ]
    },
    {
      "id": "Item",
      "source": "Items.csv",
      "schema_version": 1,
      "audience": "Shared",
      "primary_key": "id",
      "fields": [
        { "name": "id", "type": "string", "required": true, "min_length": 1, "max_length": 64 },
        { "name": "category_id", "type": "string", "required": true, "foreign_key": "ItemCategory.id" },
        { "name": "display_name", "type": "string", "required": true, "min_length": 1, "max_length": 128 },
        { "name": "enabled", "type": "bool", "required": false, "default": true },
        { "name": "max_stack", "type": "int32", "required": false, "default": 1, "min": 1, "max": 999 },
        { "name": "weight", "type": "float64", "required": true, "min": 0, "max": 1000 },
        { "name": "rarity", "type": "enum", "required": true, "values": ["Common", "Uncommon", "Rare", "Epic"] },
        { "name": "description", "type": "string", "required": false, "allow_empty": true, "null_token": "<null>", "max_length": 256 }
      ]
    }
  ]
}
```

当前支持 `string`、`bool`、`int32`、`float64` 和受控 `enum`。`audience` 可取：

- `Shared`：Client 和 Server 都包含。
- `ClientOnly`：只进入客户端目标。
- `ServerOnly`：只进入专服目标。

Schema 编辑器在结构真实变化时自动递增表的 `schema_version`。跨端数据协议发生约定变化时由项目递增 `protocol_version`。这些版本不会自动迁移旧二进制或网络连接。

Schema 同时保存源目录、运行时目录和 C# 输出路径。所有路径相对于 Schema，使用正斜杠，不能是绝对路径或包含 `..`。C# 文件必须位于运行时目录之外，因为运行时目录生成时会整体替换。

## 4. 先检查，再生成

只检查且不写文件：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py check `
  --schema DataTables/Base/.datatable.schema.json
```

检查通过后生成全部：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate `
  --schema DataTables/Base/.datatable.schema.json
```

成功返回退出码 0，数据、Schema、路径或 I/O 错误返回 1。工具会在全部校验通过后再提交文件；失败不会覆盖上一次成功生成的产物。内容没有变化的 C# 文件不会重写，避免无意义的 Godot/.NET 重编译。

生成物包括：

- 每张表的 `<TableId>.gdtb`。
- `manifest.json`，存在端侧专属表时再生成必要的 Client/Server Manifest。
- 聚合的强类型 C# 行类型、查询表和 Loader。

不要手工修改这些文件；下次生成会覆盖。

## 5. 从 Godot 编辑器操作

启用唯一的 **GoDo Framework** 插件，然后打开：

```text
GoDo → DataTable...
```

窗口默认寻找 `res://DataTables/Base/.datatable.schema.json`。可以编辑 Schema、查看或加入数据文件，并执行“检查全部”“生成全部...”或“生成选中表...”。生成操作会先展示目标并要求确认，完成后通知 Godot 扫描新文件。

Python 路径只保存在本机 EditorSettings，不写入项目配置。团队和 CI 共用版本控制内的 Schema。

## 6. 在游戏中读取生成表

生成代码位于 Schema 指定的命名空间。当前实验生成器提供强类型 Loader 和每表查询类型，例如：

```csharp
using MyGame.Generated;

ItemTable items = DataTableLoader.LoadItem(
    "res://DataTables/Base/Runtime/Item.gdtb");

ItemRow sword = items.Get("iron_sword");
if (items.TryGet("health_potion", out ItemRow potion))
    GD.Print(potion.MaxStack);
```

生成类型当前是程序集内部类型，供同一 Godot C# 项目直接使用。生成器和 Loader 命名尚未稳定，升级框架后先重新生成并编译，不要围绕生成代码建立额外公开兼容层。

运行时读取会校验文件 magic、格式版本、schema、表 ID、字段数、大小与 SHA-256 摘要；损坏或不兼容时明确抛出异常。单文件读取上限为 2 GiB。

## 7. 检查生成物是否过期

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py verify-generated `
  --schema DataTables/Base/.datatable.schema.json
```

该命令在内存中构建期望结果，并只读比较现有生成目录。缺失、额外或内容过期都会返回 1；不会写临时文件、删除额外文件或改变时间戳。适合在提交前和 CI 中执行。

单表生成：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate `
  --schema DataTables/Base/.datatable.schema.json `
  --table Item
```

首次生成、增删表、未选表已过期或结构发生变化时必须生成全部。单表模式仍会校验全部 CSV、外键和摘要，不是绕过全量正确性的快捷通道。

## 8. Client/Server 隔离与正式导出

客户端 Manifest 只包含 `Shared + ClientOnly`，服务器 Manifest 只包含 `Shared + ServerOnly`。检查共享数据兼容：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py compare-manifests `
  --client DataTables/Base/Runtime/manifest.client.json `
  --server DataTables/Base/Runtime/manifest.server.json
```

正式发布不要只点击 Godot 导出。Godot 4.7 的 EditorExportPlugin 无法可靠中止错误导出，应使用包装脚本先执行只读门禁，再启动 Godot：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable_export.py `
  --godot "E:/Godot/Godot_v4.7/Godot_v4.7-stable_mono_win64_console.exe" `
  --project . `
  --preset "Windows Desktop" `
  --output Builds/Windows/Game.exe `
  --mode release
```

普通 preset 选择 Client；带 `dedicated_server` feature tag 的 preset 选择 Server。Release 与 Debug 包都只映射目标 `.gdtb` 和 `manifest.json`；`.datatable.schema.json`、`.datafiles` 和诊断文件不进入包。

Manifest 的哈希用于发现 Client/Server 数据不一致，不是数字签名，也不能证明文件可信。认证、防篡改和连接拒绝策略由游戏网络层负责。

## 常见错误

- CSV 中文乱码：文件不是 UTF-8，或表格工具导出编码不一致。
- 外键不存在：引用值在目标表主键列中找不到，先修正源数据。
- 生成失败但旧文件仍存在：这是事务保护；旧产物不会因失败被覆盖，但已经过期。
- 单表生成被拒绝：缺少完整基线、其他表已变化或表集合有增删，应生成全部。
- 编辑器可以导出但包缺数据：直接 Godot 导出不是可靠门禁，改用包装脚本。
- Client 包包含敏感服务端表：检查 audience、preset 的 `dedicated_server` tag 和目标 Manifest。
- 修改 CSV 后游戏仍用旧数据：运行 generate，并用 verify-generated 检查。
- 把 Manifest 哈希当安全签名：它只能比较一致性，不能防止恶意替换。

DataTable 当前主要通过生成代码使用，尚未提供稳定的 GoDo public runtime API；升级框架后应重新生成并运行完整验证。
