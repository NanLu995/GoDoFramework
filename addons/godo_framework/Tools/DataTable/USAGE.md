# DataTable 编译前端使用指南

## 定位

本工具在 Editor、CI 或开发命令行中校验 UTF-8 CSV 与 JSON Profile，并生成确定性的规范化 IR、数据集 Manifest、未压缩 `.gdtb`、Debug JSON、构建报告和强类型 C# 读取代码。它不进入游戏运行时，不依赖第三方 Python 包，也不提供网络协议或加密。

当前为阶段 C.1 工具边界，尚未接入 GoDo EditorPlugin。Zstd 仍由后续 Godot C# 构建目标处理；本工具生成未压缩候选，因此不能单独完成 `Always` 压缩的正式发布流程。

## 环境

- Python 3.10 或更高版本；
- 输入 CSV 使用 UTF-8，可带 BOM；
- Profile、源目录和输出位置必须显式传入，不读取机器级或项目级隐式配置。

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

## 失败语义与写入边界

- 成功返回退出码 `0`；数据诊断、Profile 错误、路径错误或 I/O 失败返回 `1`；
- `check` 会实际完成解析、跨表校验、二进制构建、摘要和 C# 生成，但只保留内存结果；
- `generate` 在所有数据校验通过后才提交产物，输出目录和 C# 文件使用备份与回滚共同提交；
- 数据输出目录会被整体替换，因此工具拒绝把 Profile、源目录或它们的祖先作为输出目录；
- C# 文件必须位于数据输出目录之外，避免目录替换吞掉单独产物；
- 数据错误不会覆盖上一次成功生成的产物。

## 生命周期与性能

工具是一次性离线进程，不创建 Godot 节点，不读取 Autoload，也不产生运行时分配。它会在内存中保存规范化数据；`check` 还会逐表构建并丢弃二进制候选。超大表分块尚未实现，当前仍受 Profile 中的行列、字符串和诊断上限约束。

## 当前限制

- 只支持 `string`、`bool`、`int32`、`float64` 和受控 `enum`；
- 只支持整套数据集生成，尚未提供单表增量模式；
- 尚未接入 EditorPlugin、CI 过期产物检查和 Release 导出过滤；
- Zstd 选择、正式 ExportRelease、移动平台和服务器目标仍需后续验证。

## 验证

运行 `python Verification/Experimental/DataTable/verify_prototype.py`，会验证确定性、六类数据错误、`check` 不写入、CLI 错误码、危险输出目录拒绝、空格路径、失败不覆盖旧产物和双产物提交失败回滚。
