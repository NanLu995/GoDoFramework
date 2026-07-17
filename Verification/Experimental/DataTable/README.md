# DataTable 阶段 A / B 原型

本目录只验证 DataTable 的源数据、校验、跨语言产物和读取性能，不属于框架运行时，也不承诺 public API。

## 运行

```powershell
python Verification/Experimental/DataTable/verify_prototype.py
dotnet build GoDoFramework.sln
E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe --headless --path . res://Verification/Experimental/DataTable/DataTableCompressionTargetRunner.tscn
E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe --headless --path . res://Verification/Experimental/DataTable/DataTablePrototypeBenchmark.tscn
```

第一条命令使用固定种子生成小型数据、约一万行性能数据和六类错误样例，然后验证：

- 正常数据能生成规范化 IR、Manifest、未压缩 `.gdtb`、internal C# 类型、Debug JSON 和报告；
- 相同输入的产物完全一致；
- 缺列、数据行少列、重复键、非法 enum、越界和无效外键均产生精确诊断；
- 失败生成不会覆盖上一次成功产物。

压缩目标场景使用 Godot 自带 Zstd 生成候选、`Auto` 保守选择结果和确定性体积报告。Headless 基准同时读取未压缩与 Zstd 文件，并拒绝 magic、格式版本、Schema 版本、未知 flags、payload 摘要、截断文件、字符串池索引、主键索引、Zstd 篡改、错误原始大小和解压后摘要异常。内部边界样例会按需重新计算 payload SHA，确保测试实际进入目标检查。

生成数据和二进制位于本目录 `Artifacts/`，不纳入版本控制。`Generated/DataTablePrototype.Generated.cs` 由原型编译器生成并参与项目编译，禁止手工修改。

## 当前边界

- 只支持 UTF-8 CSV 与受控 JSON Profile；
- 只实现阶段 A 所需的 string、bool、int32、float64 和 enum；
- `.gdtb` v2 使用小端序，支持未压缩或 Godot Zstd payload；
- `Auto` 当前只提供压缩建议并选择未压缩，`Never` / `Always` 已有实验语义；
- 不包含加密、EditorPlugin、导出过滤、热更新或正式运行时加载 API；
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
