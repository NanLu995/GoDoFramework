# DataTable 编译前端使用指南

## 定位

本工具在 Editor、CI 或开发命令行中校验 UTF-8 CSV 与 JSON Profile，并生成确定性的规范化 IR、数据集 Manifest、未压缩 `.gdtb`、Debug JSON、构建报告和强类型 C# 读取代码。它不进入游戏运行时，不依赖第三方 Python 包，也不提供网络协议或加密。

当前已完成阶段 C.3 的整套数据集与安全单表生成命令、EditorPlugin 接入。Zstd 仍由后续 Godot C# 构建目标处理；本工具生成未压缩候选，因此不能单独完成 `Always` 压缩的正式发布流程。

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
```

`--build-config` 不能与 `--profile`、`--source`、`--output` 或 `--csharp` 混用。`--table` 只用于 `generate`，值必须是 Profile 中精确的数据表 ID；`check` 始终检查全部数据表。

## EditorPlugin

启用唯一的 GoDo EditorPlugin 后，打开顶部 `GoDo → DataTable...`。窗口默认查找 `res://DataTables/datatable.build.json`，也可选择其他项目内 JSON；选择结果按项目保存在 EditorSettings，不修改 `project.godot`。Python 留空时依次检测 `python3` 与 `python`，也可选择本机解释器文件。

“检查全部”只读取并显示完整诊断；“生成全部...”先展示数据目录与 C# 文件并要求确认。“生成选中表...”从 Profile 填充表 ID 下拉框，确认窗口会列出目标 `.gdtb`、数据集元数据和聚合 C#。外部 Python 在独立线程中执行，操作期间禁止并发 DataTable 命令；生成成功后通知 Godot 重新扫描文件。禁用插件时若命令仍在运行，会等待该进程结束并可靠回收线程。

## 失败语义与写入边界

- 成功返回退出码 `0`；数据诊断、Profile 错误、路径错误或 I/O 失败返回 `1`；
- `check` 会实际完成解析、跨表校验、二进制构建、摘要和 C# 生成，但只保留内存结果；
- `generate` 在所有数据校验通过后才提交产物，输出目录和 C# 文件使用备份与回滚共同提交；
- `generate --table <ID>` 仍解析并校验全部 CSV、Profile、外键、摘要和二进制候选，只局部提交目标 `.gdtb`、数据集 IR / Debug JSON / Manifest / 报告，以及内容确有变化的聚合 C#；
- 单表生成要求先有一次成功的全量生成。已有 IR、Manifest、`.gdtb` 表集合必须完整，所有未选表的结构与二进制必须和当前输入一致；否则不写入并提示先生成全部；
- 单表提交的所有变化文件使用同一备份/回滚事务；未变化文件不改写，因此未选表和未变化 C# 保留时间戳；
- 生成的 C# 内容未变化时保留原文件和时间戳，避免触发无意义的 Godot / .NET 重编译；
- 数据输出目录会被整体替换，因此工具拒绝把 Profile、源目录或它们的祖先作为输出目录；
- C# 文件必须位于数据输出目录之外，避免目录替换吞掉单独产物；
- 数据错误不会覆盖上一次成功生成的产物。

## 生命周期与性能

Python 工具是一次性离线进程，不读取 Autoload，也不产生游戏运行时分配。Editor 扩展只在打开窗口或执行命令时工作，不使用 `_Process()`；后台线程只负责阻塞式进程调用，Godot UI 与文件系统刷新留在主线程。编译器会在内存中保存规范化数据；`check` 还会逐表构建并丢弃二进制候选。超大表分块尚未实现，当前仍受 Profile 中的行列、字符串和诊断上限约束。

## 当前限制

- 只支持 `string`、`bool`、`int32`、`float64` 和受控 `enum`；
- 单表生成不会绕过全量校验，也不能用于首次生成、增删表或修复已过期的未选表；这些情况必须生成全部；
- 尚未实现 CI 过期产物检查和 Release 导出过滤；
- Zstd 选择、正式 ExportRelease、移动平台和服务器目标仍需后续验证。

## 验证

运行 `python Verification/Experimental/DataTable/verify_prototype.py`，会验证确定性、六类数据错误、`check` 不写入、CLI 错误码、Build Config、危险输出目录拒绝、空格路径、失败不覆盖旧产物、全量提交回滚，以及单表数据/结构变化、未选表保留、基线拒绝和多文件回滚。`DataTableEditorExtensionProbe.gd` 不加入永久 `run_all.py`，只在本阶段通过 Headless Editor 真实验证窗口、后台检查、全量/单表确认生成与成功刷新。
