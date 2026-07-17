# DataTable 设计

> 状态：整体方案已确认，尚未进入实现与稳定基线。本文中的具体类型名仍需通过首张真实业务表原型验证后才能成为 public API。

## 1. 定位

DataTable 将策划维护的表格数据在 Editor / CI 阶段校验并生成紧凑二进制，运行时通过生成的强类型代码读取。它面向大量结构化、按稳定主键查询的数据，不替代现有 Config：

- Config 继续管理业务手写的 Godot `Resource` 配置、复杂资源引用和 `ConfigTable<TKey, TEntry>` 索引；
- DataTable 管理 CSV 等外部表格来源、批量格式校验、代码生成、二进制构建与版本摘要；
- 两者都不接入 Services，不在每帧解析数据，也不建立隐式全局可变状态。

功能名称为 **DataTable**。框架代码使用 `GoDo.DataTables` 命名空间，不声明名为 `GoDo.DataTable` 的 public 类型，避免与 .NET 的 `System.Data.DataTable` 冲突。生成的业务类型使用 `ItemRow`、`ItemTable` 等具体名称。

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
DataTable Profile / Schema
        ↓ 生成模板与字段说明
Excel / LibreOffice / Google Sheets
        ↓ 导出 UTF-8 CSV
DataTable 生成与检查
```

CSV 只承载数据，字段类型、必填、默认值、范围、enum、主键与外键规则放在受版本控制的 Profile / Schema 中。复杂嵌套关系优先拆成主表和子表，通过稳定 ID 引用。

### 其他方式

- JSON 源导入放在第二阶段，适合开发人员维护的嵌套数据；
- Curve、Texture、Material 等 Godot 类型继续使用 Config Resource Inspector；
- 直接读取 `.xlsx` 需要额外库和公式语义，不进入首版；
- Google Sheets 自动导出涉及网络与凭据，作为后续可选连接器。

## 5. 生成流程

```text
CSV + Profile
      ↓
编码与语法检查
      ↓
Schema、类型、范围、主键与外键检查
      ↓
业务校验
      ↓
规范化中间数据
      ├─ 生成强类型 *.Generated.cs
      ├─ 生成运行时 *.gdtb
      ├─ Debug 生成可读 JSON / 源位置映射
      └─ 生成 Manifest 与构建报告
```

Schema 变化时重新生成 C# 类型和读取器，需要重新编译；只有数据值变化时只重新生成 `.gdtb` 和报告，不应无意义改写 C# 文件。

所有输出必须确定性生成：相同输入、Profile 和工具版本在不同操作系统上产生相同内容摘要。换行风格和 UTF-8 BOM 不应改变摘要，真实字段、值或行顺序变化必须改变摘要。

## 6. 生成代码

最高性能与低内存需要生成强类型代码，避免运行时反射和动态 `Dictionary<string, Variant>`。候选产物：

```text
ItemRow.Generated.cs
ItemTable.Generated.cs
Items.gdtb
Items.manifest.json
Items.debug.json          # 仅 Debug / 开发产物
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

- 保留原始 CSV 与 Profile；
- 额外生成规范化 JSON、字段说明、源位置映射和完整构建报告；
- 后续可在 Debugger 中读取诊断产物并展示 DataTable 状态；
- 可读 JSON 只用于检查，不作为默认运行时数据源。

### Release

- 只携带运行时 `.gdtb` 和必要 Manifest；
- 排除 CSV、可读 JSON、源位置、字段注释和编辑器 Profile；
- 具体导出包含 / 排除由 DataTable 编辑器导出扩展负责，不要求业务手工维护路径列表；
- ServerOnly 表不得进入客户端导出包。

## 8. 二进制布局

候选 `.gdtb` 结构：

```text
Header
├─ Magic: GDTB
├─ 文件格式版本
├─ Schema 版本
├─ Table ID
├─ 行数与字段数
├─ Flags：压缩方式等
├─ 内容 SHA-256
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
2. 同时尝试 Godot 4.7 自带的 Zstd；
3. 比较压缩比例和绝对节省量；
4. 按经过目标平台基准验证的规则选择；
5. 允许 Profile 对单表覆盖为 `Never` 或 `Always`。

构建报告至少输出源文件大小、未压缩二进制大小、Zstd 大小、生成耗时和最终选择。加载时间、解压时间、峰值内存和常驻内存必须在 Windows 与目标移动平台实测后，才能确定 Auto 阈值。

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

诊断至少包含 Profile、源文件、行号、列名、目标字段、严重级别和经过长度限制的原始值：

```text
Error Items.csv:27 price
无法将 "12元" 转换为有限 float。

Error Enemies.csv:43 drop_table_id
引用的数据项 "drop/boss_03" 不存在。

Warning Skills.csv:18 old_effect
字段已废弃，将在 Schema 版本 3 移除。
```

- Error 阻止生成；
- Warning 本地允许继续，CI / Release 可按规则视为错误；
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

客户端与服务器从相同源表和 Profile 生成各自允许的 `.gdtb`。构建流程必须执行 DataTable 检查，不能依赖开发机缓存。

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
私有 CSV + Profile
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

原型放在 `Verification/Experimental/DataTable/`，不接入 `GoDoRuntime`、Services、EditorPlugin 或正式文档 API，不承诺 public API。压缩只保留格式标志，不执行压缩；加密不保留标志、不实现接口。

## 14. 性能与生命周期

- 解析、校验、代码生成和压缩只在 Editor / CI 执行；
- Release 不执行反射、JSON 解析或每帧更新；
- 表在明确初始化边界加载并由业务持有只读引用；
- 不在 `_Process` / `_PhysicsProcess` 中重复加载或重建索引；
- 对源文件大小、行列数量、字符串长度和诊断数量设置上限；
- `.gdtb` 的跨平台字节序、AOT、损坏文件和版本不兼容必须显式验证。

## 15. 分阶段交付

### 阶段 A：真实 CSV 原型

- 选择 Item 或 Enemy 等真实业务表；
- 完成 Profile、模板、CSV 解析和精确诊断；
- 生成强类型 Row / Table 和未压缩 `.gdtb`；
- 验证确定性、查询、失败不覆盖旧产物与基础性能。

### 阶段 B：压缩与构建报告

- 实现 `Auto` / `Never` / `Always` 语义；
- 使用 Zstd 生成候选产物；
- 在 Windows 与目标移动平台比较加载、峰值内存和体积；
- 根据证据确定 Auto 规则。

### 阶段 C：Editor / CI / Export

- 接入唯一 GoDo EditorPlugin；
- 支持单表生成、全部生成和只检查不写入；
- CI 拒绝过期生成物；
- Debug 保留可读产物，Release 排除源数据；
- 验证客户端、Godot 专服和非 Godot 服务器摘要边界。

### 阶段 D：后续能力

按真实需求评估 JSON 源、Google Sheets 连接器、超大表分块、远程更新、签名、加密、DLC / Mod 和自定义字段转换器。加密当前明确不实施。

## 16. 开工前输入

1. 第一张真实表及其预计行数、字段类型和访问频率；
2. 目标平台及可接受的启动时间、峰值内存和包体；
3. 首个网络目标是 Godot C# 专服还是其他技术栈；
4. ClientOnly、Shared、ServerOnly 表的实际分类需求；
5. 生成 C# 文件和 `.gdtb` 的版本控制策略。
