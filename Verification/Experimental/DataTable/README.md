# DataTable 阶段 A / B 与 C.1 至 C.6 验证

本目录验证 DataTable 的源数据、正式编译前端 CLI、跨语言产物和读取性能，不属于框架运行时，也不承诺 public API。编译器实现位于 `addons/godo_framework/Tools/DataTable/godo_datatable.py`；本目录的同名旧入口只保留命令转发兼容性。

## 运行

```powershell
python Verification/Experimental/DataTable/verify_prototype.py
dotnet build GoDoFramework.sln
E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe --headless --path . res://Verification/Experimental/DataTable/DataTableCompressionTargetRunner.tscn
E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe --headless --path . res://Verification/Experimental/DataTable/DataTablePrototypeBenchmark.tscn
E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe --headless --editor --path . --script res://Verification/Experimental/DataTable/DataTableEditorExtensionProbe.gd
E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe --headless --editor --path . --script res://Verification/Experimental/DataTable/DataTableExportPluginProbe.gd
python Verification/Experimental/DataTable/verify_export_plugin.py --godot E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe
```

第一条命令使用固定种子生成小型数据、约一万行性能数据和六类错误样例，然后通过正式工具验证：

- 正常数据能生成规范化 IR、完整及 Client / Server 目标 Manifest、未压缩 `.gdtb`、internal C# 类型、完整及目标 Debug JSON 和报告；
- 相同输入的产物完全一致；
- 缺列、数据行少列、重复键、非法 enum、越界和无效外键均产生精确诊断；
- 失败生成不会覆盖上一次成功产物。
- `check` 完成全部内存构建但不写入，错误返回非零退出码；
- `generate` 支持带空格路径，并拒绝可能覆盖源数据的输出目录。
- 数据目录提交成功但 C# 提交失败时，两类旧产物都会恢复。
- 单一 Schema 的相对路径、缺字段和目录逃逸会在写入前验证；实验 Editor Probe 还会打开可视化 Schema 编辑器，验证 `.datafiles` 扫描、未加入 CSV 排除状态、按表头加入 Schema、原样保存不升级结构版本、自动检查、生成确认和文件刷新。
- 单表生成仍执行全量外键与输入校验，并验证目标表数据/结构更新、未选表内容与时间戳保留、过期/缺失/表集合变化拒绝、未知表 ID 和多文件回滚；Editor Probe 也会真实选择 `Item` 并确认生成。
- `verify-generated` 接受单表生成后的完整有效状态，且只读检出源数据、Schema 结构、聚合 C#、缺失文件和额外文件造成的过期状态。
- Client 目标只包含 `Shared + ClientOnly`，Server 目标只包含 `Shared + ServerOnly`；生成读取器通过 Godot `FileAccess` 实际读取绝对路径、项目目录和 PCK 内的 `res://`。
- `compare-manifests` 接受兼容的 Client / Server 目标 Manifest，并精确拒绝数据集、共享结构、共享内容、target、必需字段和 JSON 错误。
- 导出规划 Probe 检查 Client / Server、Debug / Release 映射和过期校验；隔离导出脚本实际打开两个 PCK 检查 audience 与源文件排除，并证明发布包装命令会在过期时拒绝启动 Godot。

压缩目标场景使用 Godot 自带 Zstd 生成候选、`Auto` 保守选择结果和确定性体积报告。Headless 基准同时读取未压缩与 Zstd 文件，并拒绝 magic、格式版本、Schema 版本、未知 flags、payload 摘要、截断文件、字符串池索引、主键索引、Zstd 篡改、错误原始大小和解压后摘要异常。内部边界样例会按需重新计算 payload SHA，确保测试实际进入目标检查。

生成数据和二进制位于本目录 `Artifacts/`，不纳入版本控制。`Generated/DataTablePrototype.Generated.cs` 由原型编译器生成并参与项目编译，禁止手工修改。

## 当前边界

- 只支持 UTF-8 CSV 与受控 DataTable Schema；
- 只实现阶段 A 所需的 string、bool、int32、float64 和 enum；
- `.gdtb` v2 使用小端序，支持未压缩或 Godot Zstd payload；
- `Auto` 当前只提供压缩建议并选择未压缩，`Never` / `Always` 已有实验语义；
- 不包含加密、热更新、正式运行时 public API、移动端导出或完整 ExportRelease 可执行文件验证；
- 不进入永久 `Verification/Automated/run_all.py` 回归。

## 当前 Windows 证据

2026-07-17 在 Godot 4.7 Mono、.NET 8 和 Windows 环境中，10,004 行的两张表生成未压缩 v2 二进制共 812,987 bytes，Zstd 候选共 158,646 bytes，减少约 80.49%。以下均为一次 Headless 样本：

| 构建 | 压缩 | 加载耗时 | 总托管分配 | GC 后保留托管内存 | 100,000 次查询 |
|---|---|---:|---:|---:|---:|
| Debug | 无 | 9.559 ms | 4,088,888 bytes | 1,851,808 bytes | 11.077 ms / 0 B |
| Debug | Zstd | 9.414 ms | 4,405,608 bytes | 1,851,808 bytes | 10.954 ms / 0 B |
| Release | 无 | 2.901 ms | 4,088,888 bytes | 1,851,808 bytes | 1.814 ms / 0 B |
| Release | Zstd | 3.367 ms | 4,405,608 bytes | 1,851,808 bytes | 1.894 ms / 0 B |

Release 压缩目标单次样本中，`Item` 从 812,726 bytes 降至 158,419 bytes，压缩约 2.965 ms、解压约 1.607 ms；仅 261 bytes 的 `ItemCategory` 压缩后为 227 bytes。当前报告对两张表都建议 Zstd，但 `Auto` 不据此直接选择，避免在移动端和真实表分布验证前固化错误阈值。

Release 样本通过构建 Release 程序集并临时放入 Godot Headless 的 Debug 加载位置获得；验证结束后已经恢复普通 Debug 构建。它验证 Release IL/JIT 行为，不是正式 ExportRelease 包体性能。

该数据只用于证明原型路径可测，不是性能承诺或压缩 `Auto` 阈值。正式 ExportRelease、其他硬件和移动平台仍需单独基准。
