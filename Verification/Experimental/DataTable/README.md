# DataTable 阶段 A / B 与 C.1 至 C.6 验证

本目录验证 DataTable 的源数据、正式编译前端 CLI、跨语言产物和读取性能，不属于框架运行时，也不承诺 public API。编译器实现位于 `addons/godo_framework/Tools/DataTable/godo_datatable.py`；本目录的同名旧入口只保留命令转发兼容性。

## 运行

```powershell
python Verification/Experimental/DataTable/verify_prototype.py
dotnet build GoDoFramework.sln
& $env:GODOT_PATH --headless --path . res://Verification/Experimental/DataTable/DataTableCompressionTargetRunner.tscn
& $env:GODOT_PATH --headless --path . res://Verification/Experimental/DataTable/DataTablePrototypeBenchmark.tscn
& $env:GODOT_PATH --headless --editor --path . --script res://Verification/Experimental/DataTable/DataTableEditorExtensionProbe.gd
& $env:GODOT_PATH --headless --editor --path . --script res://Verification/Experimental/DataTable/DataTableExportPluginProbe.gd
python Verification/Experimental/DataTable/verify_export_plugin.py --godot $env:GODOT_PATH
python Verification/Experimental/DataTable/verify_export_release.py --godot $env:GODOT_PATH --output-root <新目录> --prepare-only
```

性能样例默认生成 10,000 行。需要观察大表加载峰值时，可生成 100,000 行后重新编译并运行同一基准：

```powershell
python Verification/Experimental/DataTable/verify_prototype.py --performance-rows 100000
dotnet build GoDoFramework.sln
& $env:GODOT_PATH --headless --path . res://Verification/Experimental/DataTable/DataTableCompressionTargetRunner.tscn
& $env:GODOT_PATH --headless --path . res://Verification/Experimental/DataTable/DataTablePrototypeBenchmark.tscn
```

加载报告中的 `RetainedManagedBytes` 表示表仍被持有时相对基线保留的托管内存；`PostReleaseManagedBytes` 表示加载方法返回并完成完整 GC 后相对基线的变化。后者用于观察趋势，不设置跨机器固定阈值。

`verify_export_release.py` 会创建独立 C# 项目，复用正式发布门禁和 `DataTableServiceRegression.tscn`。`--prepare-only` 只准备并导入项目，适合手动安装导出模板后从 Godot 导出；省略该参数会继续导出并运行 Windows ExportRelease。输出目录必须不存在，脚本不会覆盖或删除已有验收产物。

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
- 不包含加密、热更新或移动端导出；Windows 完整 ExportRelease 可执行文件通过 `verify_export_release.py` 准备或自动验收，正式 `IDataTableService` 语义由 `Verification/Automated/DataTableServiceRegression.tscn` 验证；
- 不进入永久 `Verification/Automated/run_all.py` 回归。

## 当前 Windows 证据

2026-07-27 在 Godot 4.7 Mono、.NET 8 和 Windows Debug 环境中，生成读取器改为顺序读取文件头和一份存储 payload。以下均为一次 Headless 样本：

| 构建 | Item 行数 | 压缩 | 加载耗时 | 总托管分配 | GC 后保留托管内存 | 释放后变化 | 100,000 次查询 |
|---|---:|---|---:|---:|---:|---:|---:|
| Debug | 10,000 | 无 | 9.453 ms | 4,089,224 bytes | 1,843,584 bytes | -8,224 bytes | 11.822 ms / 0 B |
| Debug | 10,000 | Zstd | 9.714 ms | 4,247,800 bytes | 1,851,808 bytes | 0 bytes | 11.420 ms / 0 B |
| Debug | 100,000 | 无 | 61.673 ms | 41,984,008 bytes | 19,434,368 bytes | -8,224 bytes | 3.169 ms / 0 B |
| Debug | 100,000 | Zstd | 59.684 ms | 43,496,336 bytes | 19,442,592 bytes | 0 bytes | 2.835 ms / 0 B |
| ExportRelease | 10,000 | 无 | 3.449 ms | 4,089,224 bytes | 1,843,584 bytes | -8,224 bytes | 2.002 ms / 0 B |
| ExportRelease | 10,000 | Zstd | 3.711 ms | 4,247,800 bytes | 1,851,808 bytes | 0 bytes | 1.857 ms / 0 B |
| ExportRelease | 100,000 | 无 | 34.271 ms | 41,984,032 bytes | 19,434,368 bytes | -8,224 bytes | 3.777 ms / 0 B |
| ExportRelease | 100,000 | Zstd | 46.347 ms | 43,496,344 bytes | 19,442,592 bytes | 0 bytes | 3.666 ms / 0 B |

10 万行 Debug 样本中，移除重复压缩 payload 后，Zstd 总托管分配从 45,008,320 bytes 降至 43,496,336 bytes，减少 1,511,984 bytes。剩余额外分配约等于一份压缩输入；完整解压 payload 仍是当前加载峰值的一部分。表中 ExportRelease 样本通过临时加载 ExportRelease 程序集验证 IL/JIT 行为；独立完整 ExportRelease 可执行文件另已实际运行并通过 `DataTableServiceRegression (10/10)`，但没有把包体和启动性能纳入本表。

该数据只用于证明原型路径可测，不是性能承诺或压缩 `Auto` 阈值。其他硬件、真实业务长期体验和移动平台仍需单独基准。
