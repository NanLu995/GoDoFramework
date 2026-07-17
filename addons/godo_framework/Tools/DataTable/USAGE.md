# DataTable 编译前端使用指南

## 定位

本工具在 Editor、CI 或开发命令行中校验 UTF-8 CSV 与 JSON Profile，并生成确定性的规范化 IR、完整及 Client / Server 目标 Manifest、未压缩 `.gdtb`、完整及目标 Debug JSON、构建报告和强类型 C# 读取代码。它不进入游戏运行时，不依赖第三方 Python 包，也不提供网络协议或加密。

当前已完成阶段 C.6 的整套数据集、安全单表生成、只读过期检查、Client / Server 导出隔离、PCK 读取验证、语言无关 Manifest 兼容契约和 EditorPlugin 接入，但尚未进入稳定基线。Zstd 仍由后续 Godot C# 构建目标处理；本工具生成未压缩候选，因此不能单独完成 `Always` 压缩的正式发布流程。

## 环境

- Python 3.10 或更高版本；
- 输入 CSV 使用 UTF-8，可带 BOM；
- Profile、源目录和输出位置必须显式传入，或通过版本控制内的 Build Config 声明；Python 解释器路径只保存在本机 EditorSettings，不写入项目配置。

## 命令

只检查，不创建或修改任何输出文件：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py check `
  --profile DataTables/profile.json `
  --source DataTables/Sources
```

校验并生成全部产物：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate `
  --profile DataTables/profile.json `
  --source DataTables/Sources `
  --output DataTables/Generated/Data `
  --csharp DataTables/Generated/DataTables.Generated.cs
```

所有相对路径均相对于命令的当前工作目录。路径包含空格时应按 shell 规则加引号。

### Build Config

Editor 与后续 CI 共用 Build Config。其所有路径都相对于该 JSON 所在目录，必须使用正斜杠，不能是绝对路径或包含 `..`：

```json
{
  "format_version": 1,
  "profile": "profile.json",
  "source": "Sources",
  "output": "Generated/Data",
  "csharp": "Generated/DataTables.Generated.cs"
}
```

使用同一配置检查或生成：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py check --build-config DataTables/datatable.build.json
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate --build-config DataTables/datatable.build.json
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate --build-config DataTables/datatable.build.json --table Item
python addons/godo_framework/Tools/DataTable/godo_datatable.py verify-generated --build-config DataTables/datatable.build.json
python addons/godo_framework/Tools/DataTable/godo_datatable.py compare-manifests --client DataTables/Generated/Data/manifest.client.json --server DataTables/Generated/Data/manifest.server.json
```

`--build-config` 不能与 `--profile`、`--source`、`--output` 或 `--csharp` 混用。`--table` 只用于 `generate`，值必须是 Profile 中精确的数据表 ID；`check` 与 `verify-generated` 始终检查全部数据表。

`check` 只判断源数据能否成功编译，不要求已有生成产物。`verify-generated` 先执行同样的全量校验和内存构建，再只读比较输出目录中的 IR、完整及目标 Debug JSON、完整及目标 Manifest、构建报告、全部 `.gdtb` 和聚合 C#；缺失、额外或内容过期均返回退出码 `1`。全量生成和安全单表生成留下的合法构建报告都可通过检查。

生成目录中的 `manifest.client.json` / `debug.client.json` 只描述 `Shared + ClientOnly`，`manifest.server.json` / `debug.server.json` 只描述 `Shared + ServerOnly`。目标 Manifest 的 `schema_hash` / `content_hash` 只覆盖该目标实际允许携带的表，同时保留共享摘要用于客户端与权威服务器兼容性比较。完整 `manifest.json` / `debug.json` 只用于离线检查，不应直接进入目标导出包。

### 跨语言 Manifest 兼容契约

`compare-manifests` 面向 Godot C# 专服、KBEngine-Nex 或其他技术栈，只判断 Client 与 Server 的 Shared 数据是否严格一致。它要求两端具有相同的 `format_version`、`data_set_id`、`protocol_version`、`shared_schema_hash`、`shared_content_hash` 和 Shared 表集合，并逐表比较 `schema_version`、`content_hash` 与 `row_count`。ClientOnly 和 ServerOnly 表、目标级 `schema_hash` / `content_hash` 本来就会不同，不参与跨端相等判断。兼容返回 `0`；无效 JSON、字段类型错误、错误 target、重复表 ID 或任一兼容差异返回 `1` 并列出原因。

目标 Manifest 契约如下：

- `format_version`：当前固定为 `2`，表示 `.gdtb` 与 Manifest 格式版本；
- `data_set_id`：Profile 声明的稳定数据集 ID；
- `protocol_version`：由项目控制的跨端数据协议版本；
- `target`：精确为 `Client` 或 `Server`；
- `included_audiences`：Client 固定为 `Shared, ClientOnly`，Server 固定为 `Shared, ServerOnly`；
- `schema_hash` / `content_hash`：当前目标全部允许表的结构与内容摘要；
- `shared_schema_hash` / `shared_content_hash`：仅 Shared 表的结构与内容摘要；
- `tables`：目标允许表的 `id`、`audience`、`schema_version`、`row_count`、小写 SHA-256 `content_hash` 和 `<id>.gdtb` 文件名。

摘要输入是规范化 IR，不是原始 CSV 文本。规范化 JSON 使用 UTF-8、对象键升序、无多余空白、非 ASCII 字符不转义，数组保持 Profile / 行顺序；摘要为这些字节的 SHA-256 小写十六进制。`shared_schema_hash` 对 Shared 表移除 `rows` 后的数组计算，`shared_content_hash` 对包含 `rows` 的完整 Shared 表数组计算，逐表 `content_hash` 对单个完整表对象计算。推荐非 Godot 服务端直接消费同一次编译产生的 `manifest.server.json` 和规范化 IR，避免自行复刻规范化细节。

这些摘要用于发现版本和内容不一致，不是数字签名、认证或防篡改机制。网络握手如何交换摘要、是否拒绝连接以及补丁策略由游戏决定，框架不内置网络协议。

## EditorPlugin

启用唯一的 GoDo EditorPlugin 后，打开顶部 `GoDo → DataTable...`。窗口默认查找 `res://DataTables/datatable.build.json`，也可选择其他项目内 JSON；选择结果按项目保存在 EditorSettings，不修改 `project.godot`。Python 留空时依次检测 `python3` 与 `python`，也可选择本机解释器文件。

“检查全部”只读取并显示完整诊断；“生成全部...”先展示数据目录与 C# 文件并要求确认。“生成选中表...”从 Profile 填充表 ID 下拉框，确认窗口会列出目标 `.gdtb`、数据集元数据和聚合 C#。外部 Python 在独立线程中执行，操作期间禁止并发 DataTable 命令；生成成功后通知 Godot 重新扫描文件。禁用插件时若命令仍在运行，会等待该进程结束并可靠回收线程。

### 导出过滤与可靠发布门禁

导出扩展扫描 `res://DataTables/` 一级目录中的 `*.build.json`。普通 preset 选择 Client 目标，只加入 `Shared + ClientOnly`；包含 `dedicated_server` feature tag 的 preset 选择 Server 目标，只加入 `Shared + ServerOnly`。Release 只映射目标 `.gdtb` 与 `manifest.json`，Debug 额外映射目标 `debug.json`；Build Config、Profile、CSV 源目录和完整生成目录均从包中排除。

导出扩展会在 Godot 导出开始时执行 `verify-generated` 并记录 `EXPORT_MESSAGE_ERROR`。Godot 4.7 的 `EditorExportPlugin` 没有中止导出的公开接口，实际 `--export-pack` 在收到该错误后仍可能以退出码 `0` 生成不含 DataTable 的包，因此不能把直接点击导出或直接调用 Godot CLI 当作可靠发布门禁。

正式发布和 CI 应通过包装脚本启动 Godot。它先扫描并校验项目内全部 Build Config；任一配置失败时不会启动 Godot，也不会创建目标文件：

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable_export.py `
  --godot "E:/Godot/Godot_v4.7/Godot_v4.7-stable_mono_win64_console.exe" `
  --project . `
  --preset "Windows Desktop" `
  --output Builds/Windows/Game.exe `
  --mode release
```

`--mode` 可取 `release`、`debug` 或 `pack`，分别调用 Godot 的 `--export-release`、`--export-debug` 与 `--export-pack`。默认扫描 `<project>/DataTables/*.build.json`；需要非标准位置时可重复传入 `--build-config`。包装脚本只负责编排本地命令，不修改 `export_presets.cfg`，并原样返回 Godot 的非零退出码。

## 失败语义与写入边界

- 成功返回退出码 `0`；数据诊断、Profile 错误、路径错误或 I/O 失败返回 `1`；
- `check` 会实际完成解析、跨表校验、二进制构建、摘要和 C# 生成，但只保留内存结果；
- `verify-generated` 在内存中构建预期产物并逐文件比较，不创建临时文件、不删除额外文件，也不改变现有文件和时间戳；差异最多显示前 20 项；
- `generate` 在所有数据校验通过后才提交产物，输出目录和 C# 文件使用备份与回滚共同提交；
- `generate --table <ID>` 仍解析并校验全部 CSV、Profile、外键、摘要和二进制候选，只局部提交目标 `.gdtb`、数据集 IR / Debug JSON / Manifest / 报告，以及内容确有变化的聚合 C#；
- 单表生成要求先有一次成功的全量生成。已有 IR、Manifest、`.gdtb` 表集合必须完整，所有未选表的结构与二进制必须和当前输入一致；否则不写入并提示先生成全部；
- 单表提交的所有变化文件使用同一备份/回滚事务；未变化文件不改写，因此未选表和未变化 C# 保留时间戳；
- 生成读取器使用 Godot `FileAccess`，支持普通绝对路径和 `res://`；当前已验证编辑器、Headless 项目目录和实际加载 PCK 内的 `res://` 读取；单文件读取上限为 2 GiB；
- 生成的 C# 内容未变化时保留原文件和时间戳，避免触发无意义的 Godot / .NET 重编译；
- 数据输出目录会被整体替换，因此工具拒绝把 Profile、源目录或它们的祖先作为输出目录；
- C# 文件必须位于数据输出目录之外，避免目录替换吞掉单独产物；
- 数据错误不会覆盖上一次成功生成的产物。

## 生命周期与性能

Python 工具是一次性离线进程，不读取 Autoload，也不产生游戏运行时分配。Editor 扩展只在打开窗口或执行命令时工作，不使用 `_Process()`；后台线程只负责阻塞式进程调用，Godot UI 与文件系统刷新留在主线程。编译器会在内存中保存规范化数据；`check` 还会逐表构建并丢弃二进制候选。超大表分块尚未实现，当前仍受 Profile 中的行列、字符串和诊断上限约束。

## 当前限制

- 只支持 `string`、`bool`、`int32`、`float64` 和受控 `enum`；
- 单表生成不会绕过全量校验，也不能用于首次生成、增删表或修复已过期的未选表；这些情况必须生成全部；
- 已提供可供本地、手动工作流或 CI 调用的只读过期检查、跨语言 Manifest 兼容比较与可靠导出包装命令，但本阶段不新增或修改 GitHub Actions 触发规则；
- 导出过滤已验证 Windows Client / Server PCK；完整 ExportRelease 可执行文件、移动平台和真实权威服务器仍需后续验证；
- Godot 4.7 直接导出不能由 `EditorExportPlugin` 可靠中止，发布流程必须调用包装脚本；
- Zstd 正式选择仍需移动端和真实表分布验证。

## 验证

运行 `python Verification/Experimental/DataTable/verify_prototype.py`，会验证确定性、六类数据错误、`check` 不写入、CLI 错误码、Build Config、危险输出目录拒绝、空格路径、失败不覆盖旧产物、全量提交回滚，单表数据/结构变化、未选表保留、基线拒绝和多文件回滚，`verify-generated` 的只读过期检查，Client / Server audience 隔离，以及 Manifest 正常兼容与六类拒绝语义。`DataTablePrototypeBenchmark.tscn` 已实际验证绝对路径、项目目录和 PCK 内 `res://` 读取、损坏拒绝及查询性能；`DataTableExportPluginProbe.gd` 验证导出规划，`verify_export_plugin.py` 在隔离项目中验证实际 Client / Server PCK 和发布门禁。实验验证不加入永久 `run_all.py`。
