# DataTable 设计

> 状态：整体方案已确认，阶段 A / B 原型、阶段 C.1 至 C.6 工具链以及 C.7 显式运行时加载服务已实现，但尚未进入稳定基线。生成门面、表级进度和事务加载已通过 Windows Godot 回归，移动端、AOT 与完整 ExportRelease 仍待验证。

## 1. 定位

DataTable 将策划维护的表格数据在 Editor / CI 阶段校验并生成紧凑二进制，运行时通过生成的强类型代码读取。它面向大量结构化、按稳定主键查询的数据，不替代现有 Config：

- Config 继续管理业务手写的 Godot `Resource` 配置、复杂资源引用和 `ConfigTable<TKey, TEntry>` 索引；
- DataTable 管理 CSV 等外部表格来源、批量格式校验、代码生成、二进制构建与版本摘要；
- Config 不接入 Services；DataTable 由长期 `IDataTableService` 管理显式加载、缓存与卸载，但框架启动不自动读取业务数据，两者都不在每帧解析数据。

功能名称为 **DataTable**。框架 public API 位于根命名空间 `GoDo`，使用 `IDataTableService`、`DataTableSetDefinition` 等明确名称，不声明名为 `GoDo.DataTable` 的类型，避免与 .NET 的 `System.Data.DataTable` 冲突。生成的业务类型使用 `ItemRow`、`ItemTable` 等具体名称。

## 2. 目标

- 为策划提供适合 Excel、LibreOffice 和 Google Sheets 的表格工作流；
- 在生成阶段发现缺列、错误类型、非法范围、重复键和失效外键；
- Debug 提供可读数据和精确源位置，Release 不携带源表；
- Debug 与 Release 使用同一二进制解码路径，避免只在 Release 出现格式差异；
- 通过强类型生成代码减少反射、Dictionary 和逐行 Resource 对象开销；
- 为单机存档兼容和网络客户端 / 权威服务器版本核对提供稳定摘要。

## 3. 非目标

首版不实现：

- 运行时解析 CSV / JSON；
- 数据加密、PCK 加密或密钥管理；
- 远程配置、热更新、CDN 下载、缓存和回滚；
- 直接连接 Excel、Google Sheets 或数据库；
- 玩家 Mod、DLC 或不受信任数据包；
- 自动迁移存档或网络协议；
- 用客户端表决定伤害、掉落、经济等权威结果。

加密只提高客户端提取门槛，密钥管理和跨平台密码学验证需要独立立项，明确后置。

## 4. 策划配置方式

### 首版：电子表格导出 CSV

CSV 适合大量扁平数据、批量填充、排序、筛选和公式辅助。推荐流程：

```text
可视化 DataTable Schema 编辑器
        ↓ 创建并同步 CSV 表头
Excel / LibreOffice / Google Sheets
        ↓ 导出 UTF-8 CSV
DataTable 生成与检查
```

CSV 只承载数据，字段类型、必填、默认值、范围、enum、主键与外键规则放在受版本控制的 Schema 中。Schema JSON 由 EditorPlugin 的可视化编辑器维护，不要求开发者手写；复杂嵌套关系优先拆成主表和子表，通过稳定 ID 引用。

### 其他方式

- JSON 源导入放在第二阶段，适合开发人员维护的嵌套数据；
- Curve、Texture、Material 等 Godot 类型继续使用 Config Resource Inspector；
- 直接读取 `.xlsx` 需要额外库和公式语义，不进入首版；
- Google Sheets 自动导出涉及网络与凭据，作为后续可选连接器。

## 5. 生成流程

```text
CSV + Schema
      ↓
编码与语法检查
      ↓
Schema、类型、范围、主键与外键检查
      ↓
业务校验
      ↓
规范化中间数据
      ├─ 生成数据集聚合强类型 *.g.cs
      ├─ 生成运行时 *.gdtb
      └─ 生成运行时 Manifest
```

Schema 变化时重新生成 C# 类型和读取器，需要重新编译；只有数据值变化时只重新生成 `.gdtb` 与 Manifest，不应无意义改写 C# 文件。规范化 IR、可读 JSON、源位置映射和构建报告不默认落盘。

生成代码还包含数据集聚合门面。`data_set_id` 最后一段决定稳定门面和默认目录，例如 `game.base` 生成 `BaseDataTables` 并默认指向 `res://DataTables/Base/Runtime`。业务调用 `BaseDataTables.LoadAsync()`，生成门面向 `IDataTableService` 提交表描述与强类型解码委托；Service 不反向依赖业务程序集。

所有输出必须确定性生成：相同输入、Schema 和工具版本在不同操作系统上产生相同内容摘要。换行风格和 UTF-8 BOM 不应改变摘要，真实字段、值或行顺序变化必须改变摘要。

## 6. 生成代码

最高性能与低内存需要生成强类型代码，避免运行时反射和动态 `Dictionary<string, Variant>`。候选产物：

```text
ItemRow.Generated.cs
ItemTable.Generated.cs
Items.gdtb
Items.manifest.json
```

生成的 Table 提供具体的 `Get` / `TryGet`、数量与只读遍历能力。实际 public API、Row 使用只读 struct 还是其他布局，必须根据首张真实表、AOT 编译和 Godot 平台基准决定。

首版字段优先支持：

- `string`、`bool`；
- 有明确范围检查的整数；
- 有限 `float` / `double`，拒绝 NaN 和 Infinity；
- enum 名称；
- 必填、可选、默认值；
- 以稳定 ID 表达的跨表引用。

数组、Dictionary、资源路径、颜色、向量和自定义转换器按真实需求增加。首版不把外键直接解析成另一行对象，避免循环引用和加载顺序问题。

## 7. Debug 与 Release 产物

Debug 和 Release 都读取 `.gdtb`，保证二进制布局、解码器和查询行为一致。

### Debug

- 保留原始 CSV 与 Schema；
- 规范化 JSON、字段说明、源位置映射和完整构建报告属于后续按需诊断产物，不默认写入运行时目录；
- 后续可在 Debugger 中读取诊断产物并展示 DataTable 状态；
- 可读 JSON 只用于检查，不作为默认运行时数据源。

### Release

- 只携带运行时 `.gdtb` 和必要 Manifest；
- 排除 CSV、可读 JSON、源位置、字段注释和编辑器 Schema；
- 具体导出包含 / 排除由 DataTable 编辑器导出扩展负责，不要求业务手工维护路径列表；
- ServerOnly 表不得进入客户端导出包。

## 8. 二进制布局

候选 `.gdtb` 结构：

```text
Header
├─ Magic: GDTB
├─ 文件格式版本
├─ Schema 版本
├─ Flags：未压缩或 Zstd
├─ Table ID
├─ 行数与字段数
├─ 未压缩 payload 大小
├─ 未压缩 payload SHA-256
└─ Payload
   ├─ 字符串池
   ├─ 紧凑行数据
   └─ 主键索引
```

面向游戏常见的“按 ID 读取完整一行”，首版优先采用紧凑行布局，并在加载后形成连续强类型 Row 数组：

- 重复字符串通过字符串池去重；
- 固定宽度数值直接解码；
- 可选字段使用位图；
- 数字主键按实际分布选择紧凑索引；
- 字符串主键使用哈希索引并保留原值处理碰撞；
- 查询时不解析文本、不反射、不创建临时 Dictionary。

是否保留完整二进制缓冲、是否支持延迟解码和超大表分块，需要以峰值内存与真实访问模式决定，不在首版提前复杂化。

## 9. 压缩策略

压缩减少磁盘、安装包和下载体积，但会增加解压 CPU，完整加载后的常驻内存通常不会减少，还可能产生临时解压缓冲。小表或启动热表未必适合压缩。

候选策略：

```csharp
public enum DataTableCompressionMode
{
    Auto,
    Never,
    Always
}
```

该名称目前只表达设计语义，不是已承诺 API。默认 `Auto`：

1. 生成未压缩二进制；
2. 同时尝试当前 Godot 4.x 自带的 Zstd；
3. 比较压缩比例和绝对节省量；
4. 按经过目标平台基准验证的规则选择；
5. 允许 Schema 对单表覆盖为 `Never` 或 `Always`。

确定性构建报告至少输出源文件大小、未压缩二进制大小、Zstd 大小和最终选择。生成、加载与解压时间以及峰值内存和常驻内存单独进入基准证据；必须在 Windows 与目标移动平台实测后，才能确定 Auto 阈值。

阶段 B 原型采用 Godot C# 的 `byte[].Compress(FileAccess.CompressionMode.Zstd)` 与对应 `Decompress`，不引入 Python 或 NuGet 压缩依赖。Python 编译器始终先产生已校验的未压缩 v2 文件，Godot C# 目标处理器只压缩 payload；头部继续保持可读，并记录未压缩大小和摘要。运行时解压后验证相同摘要，因此未压缩与 Zstd 共用一套行解码路径。

当前 `Auto` 只生成两种候选和建议报告，保守选择未压缩产物；`Never` 选择未压缩，`Always` 选择 Zstd。确定性报告记录体积、建议和最终选择，易波动的压缩/解压耗时只进入基准输出和验证证据。移动端数据完成前不固化 Auto 阈值。

## 10. 原数据校验

生成是全有或全无操作。只有全部解析与校验成功后才替换上一次生成物；失败必须保留此前可用文件。

校验顺序：

1. UTF-8、CSV 引号和列结构；
2. 必填列、重复列和未知列；
3. 缺失、显式空值、空字符串与默认值；
4. bool、数字、enum 和有限浮点转换；
5. 最小值、最大值、长度和允许值；
6. 主键非空与重复键；
7. 跨表外键存在性；
8. 业务自定义校验；
9. 规范化摘要与生成物一致性。

诊断至少包含 Schema、源文件、行号、列名、目标字段、严重级别和经过长度限制的原始值：

```text
Error Items.csv:27 price
无法将 "12元" 转换为有限 float。

Error Enemies.csv:43 drop_table_id
引用的数据项 "drop/boss_03" 不存在。

Warning Skills.csv:18 old_effect
字段已废弃，将在 Schema 版本 3 移除。
```

- 当前实现仅产生 Error，任一诊断都会阻止生成；废弃字段的 Warning 分级及其 CI / Release 提升规则留待独立需求实现；
- 批量检查收集多个独立问题，但必须限制最大诊断数量；
- 日志不得输出密钥、凭据或不受限的大段原始内容。

## 11. 单机游戏语义

- 发布包只携带 `.gdtb`，运行时不读取源 CSV；
- 存档保存稳定业务 ID，不保存行号、数组索引或完整配置行；
- Schema 变化由业务提供存档迁移，不自动猜测重命名或替代项；
- 缺失旧 ID 时由游戏明确选择拒绝、默认项或兼容映射；
- Mod、DLC 和玩家可编辑表属于独立信任边界。

## 12. 网络游戏语义

### Godot C# 权威服务器

客户端与服务器从相同源表和 Schema 生成各自允许的 `.gdtb`。构建流程必须执行 DataTable 检查，不能依赖开发机缓存。

### 非 Godot 权威服务器

服务器消费同一份规范化 CSV 或由其技术栈生成的等价数据。双方共享 Table ID、Schema 版本和规范化内容摘要，不要求服务器解析 Godot 文件。

### 客户端边界

- 伤害、掉落、商店价格和匹配规则由服务器裁决；
- 客户端表只用于显示、预测和本地表现；
- 连接握手可交换 DataTable 集合摘要；
- 摘要不一致时由游戏决定拒绝连接、兼容或进入补丁流程；
- ServerOnly 表在客户端构建阶段排除，而不是依靠运行时隐藏。

首版只生成摘要，不内置网络协议、下载器或断线策略。

### 跨语言生成边界

DataTable 编译器先产生与运行时语言无关的规范化 IR 和 Manifest，再由目标适配器生成各技术栈产物：

```text
私有 CSV + Schema
      → 校验与规范化 IR
      ├─ canonical-json：跨语言基线与诊断
      ├─ godot-csharp：.gdtb + C# Row / Table
      ├─ kbengine-python：Python 数据或加载代码（后续）
      └─ 其他服务端目标：Go / Java / C++ / TypeScript（按需）
```

- 目标适配器通过稳定文件与命令行契约消费 IR，不要求服务端实现 C# 接口；
- 首版先提供 `canonical-json` 和 `godot-csharp`，KBEngine-Nex / Python 适配器在真实服务器接入时实现；
- 表和字段使用 `Shared`、`ClientOnly`、`ServerOnly` 受众标记，目标适配器只能消费允许的投影；
- `SharedSchemaHash` 与 `SharedContentHash` 只覆盖双方共享投影，服务端完整摘要单独计算；
- 客户端握手只需传递 `DataSetId`、协议版本和共享摘要，不传输整表；
- ServerOnly 源数据应保存在受控数据仓库或服务端仓库，客户端仓库只接收脱敏后的生成物。

该边界允许 KBEngine-Nex 成为首个服务器目标，但不把 DataTable 的源格式、校验器或摘要算法绑定到 KBEngine、Python、Godot 或 C#。

## 13. 阶段 A 原型规格

阶段 A 使用 `ItemCategory` 与 `Item` 两张关联表验证首批真实类型：字符串主键、bool、范围整数、有限浮点、enum、默认值、可选字符串和跨表外键。固定种子脚本同时生成小型正确性数据、约一万行性能数据，以及缺列、重复键、非法 enum、越界和无效外键样例。

原型产物包括规范化 IR、数据集 Manifest、共享与完整摘要、未压缩 `.gdtb`、`internal` C# Row / Table、Debug JSON 和构建诊断报告。C# 验证读取器必须实际读取 Python 编译器生成的二进制并检查查询结果、文件体积、加载耗时和托管内存变化。

原型数据与性能验证放在 `Verification/Experimental/DataTable/`。正式离线编译前端与 Editor 扩展位于 `addons/godo_framework/Tools/DataTable/`；正式运行时服务位于 `addons/godo_framework/Runtime/DataTable/` 并由 `GoDoRuntime` 注册，但不自动加载任何数据。阶段 B 已验证 Zstd 候选、压缩模式选择与共用读取器，加密不保留标志、不实现接口。

## 14. 性能与生命周期

- 解析、校验、代码生成和压缩只在 Editor / CI 执行；
- Release 不执行反射、JSON 解析或每帧更新；
- 运行时仅在业务显式请求时读取 Manifest 和 `.gdtb`，逐表完成后让出一帧并报告进度，全部成功后才发布缓存；
- 单张表仍整文件读取和解码，取消只能在表边界观察；超大表分块与字节级进度后置；
- 表在明确初始化边界加载并由业务持有只读引用；
- 不在 `_Process` / `_PhysicsProcess` 中重复加载或重建索引；
- 对源文件大小、行列数量、字符串长度和诊断数量设置上限；
- `.gdtb` 的跨平台字节序、AOT、损坏文件和版本不兼容必须显式验证。

## 15. 分阶段交付

### 阶段 A：真实 CSV 原型

- 选择 Item 或 Enemy 等真实业务表；
- 完成 Schema、模板、CSV 解析和精确诊断；
- 生成强类型 Row / Table 和未压缩 `.gdtb`；
- 验证确定性、查询、失败不覆盖旧产物与基础性能；
- 在进入压缩前验证 magic、格式版本、Schema 版本、payload 摘要、截断文件、字符串池索引和主键索引的拒绝语义；
- 分别记录 Windows Debug 与 Release IL/JIT 基线，且不把临时程序集替换结果表述为 ExportRelease 包体性能。

### 阶段 B：压缩与构建报告

- 实现 `Auto` / `Never` / `Always` 语义；
- 使用 Zstd 生成候选产物；
- 在 Windows 与目标移动平台比较加载、峰值内存和体积；
- 根据证据确定 Auto 规则。

当前 Windows Debug / Release IL/JIT 原型已完成前三项中的 Windows 部分；移动平台与正式 ExportRelease 仍待验证，Auto 因此继续保守选择未压缩产物。

### 阶段 C：Editor / CI / Export

- 接入唯一 GoDo EditorPlugin；
- 支持单表生成、全部生成和只检查不写入；
- CI 拒绝过期生成物；
- Debug 保留可读产物，Release 排除源数据；
- 验证客户端、Godot 专服和非 Godot 服务器摘要边界。

阶段 C.1 已将 Python 编译前端放入 `addons/godo_framework/Tools/DataTable/`，提供整套 `generate` 和真正不写项目文件的 `check`。阶段 C.2 已收敛为每数据集一个可移植 Schema：字段规则、CSV 源目录和生成输出都由该文件声明，并由唯一 GoDo EditorPlugin 在后台线程调用 Python；可视化编辑器负责维护 Schema、同步 CSV 表头和结构版本，开发者不手写 JSON。数据文件面板递归显示已加入、未加入和缺失 CSV，可从表头创建初始字段；未加入 Schema 的 CSV 明确排除，已引用 CSV 缺失时拒绝保存为空文件。编辑器保存前完成完整本地校验，并将 Schema、CSV 表头和 `.gdignore` 作为同一事务提交；数据表 ID 或字段重命名同步维护外键引用，仍被引用的目标不可直接删除，仅修改 CSV 来源路径不递增表结构版本。检查不写入，生成先展示准确目标并确认，成功后刷新编辑器文件系统。工具拒绝绝对路径、`..` 逃逸、非法 C# 标识符、无效默认值和可能覆盖源数据的危险输出目录。

阶段 C.3 在不拆分聚合 C# 的前提下提供 `generate --table <ID>`。它始终全量校验输入和构建候选，只提交选中 `.gdtb`、数据集级元数据，以及内容变化的聚合 C#；提交前通过已有 Manifest 证明表集合一致，并确认未选表结构与二进制均未过期。首次生成、增删表、未选表变化或产物缺失会被拒绝并要求生成全部。局部变化通过多文件事务回滚，未选表与未变化 C# 不改写。

阶段 C.4 提供 `verify-generated`，复用完整解析、跨表校验、摘要、二进制和 C# 构建流程，在内存中得到预期状态后只读比较全部生成文件。它接受全量或安全单表生成留下的合法产物，拒绝缺失、额外、被修改或与当前源数据 / Schema 不一致的产物，并以非零退出码供本地、手动工作流或 CI 使用。

阶段 C.5.1 为导出准备按需的 `manifest.client.json` 与 `manifest.server.json`：客户端目标只包含 `Shared + ClientOnly`，权威服务器目标只包含 `Shared + ServerOnly`，共享摘要在两端保持一致。全 Shared 数据集不重复生成目标 Manifest。生成 C# 改用 Godot `FileAccess`，已验证普通绝对路径和项目内 `res://`；PCK 实际读取、`dedicated_server` 目标选择、导出前过期阻断和源文件过滤属于 C.5.2。

阶段 C.5.2 注册 DataTable `EditorExportPlugin`，普通 preset 选择 Client，带 `dedicated_server` feature tag 的 preset 选择 Server；包只加入目标 `.gdtb` 与 Manifest，并排除 Schema、Schema 声明的原始数据目录和诊断目录。新数据集默认使用 `.datafiles`，内部保留 `.gdignore`；旧 Schema 的其他源目录名继续兼容。Windows 隔离项目已实际验证 Client / Server PCK 内容与 PCK 内 `res://` 读取。当前支持的 Godot 4.x 虽将 `EXPORT_MESSAGE_ERROR` 记录为错误，但 `--export-pack` 实测仍可能成功返回并留下包，且 `EditorExportPlugin` 没有公开中止接口；因此正式发布通过 `godo_datatable_export.py` 先校验全部 Schema，成功后才启动 Godot，以“未启动导出”保证过期数据阻断，升级引擎后需重新验证该限制。

阶段 C.6 固化语言无关的目标 Manifest 契约并提供 `compare-manifests`。兼容性只要求两端的格式、数据集、协议和 Shared 表结构/内容严格一致，明确忽略 ClientOnly / ServerOnly 及目标级摘要的预期差异；错误 target、字段、重复 ID、JSON 和摘要差异均返回非零退出码。非 Godot 服务端可直接消费同次编译生成的 Server Manifest 与规范化 IR，不要求解析 Godot `.gdtb`，也不把 KBEngine-Nex 或任何握手策略引入框架。摘要只用于一致性检测，不提供签名或防篡改能力。

阶段 C.7 新增 `IDataTableService` 与生成数据集门面。`GoDoRuntime` 只注册服务；业务显式触发 Base、DLC 等数据集加载。Service 校验 Manifest 与生成描述，按表解码并报告进度，失败或取消不发布半加载状态，重复加载复用缓存，并支持显式卸载。PCK 下载、挂载、可信版本选择、热更新和回滚继续属于外层系统。

### 阶段 D：后续能力

按真实需求评估 JSON 源、Google Sheets 连接器、超大表分块、远程更新、签名、加密、DLC / Mod 和自定义字段转换器。加密当前明确不实施。

## 16. 开工前输入

1. 第一张真实表及其预计行数、字段类型和访问频率；
2. 目标平台及可接受的启动时间、峰值内存和包体；
3. 首个网络目标是 Godot C# 专服还是其他技术栈；
4. ClientOnly、Shared、ServerOnly 表的实际分类需求；
5. 生成 C# 文件和 `.gdtb` 的版本控制策略。
