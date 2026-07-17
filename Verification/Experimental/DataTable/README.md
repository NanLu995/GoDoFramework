# DataTable 阶段 A 原型

本目录只验证 DataTable 的源数据、校验、跨语言产物和读取性能，不属于框架运行时，也不承诺 public API。

## 运行

```powershell
python Verification/Experimental/DataTable/verify_prototype.py
dotnet build GoDoFramework.sln
E:\Godot\Godot_v4.7\Godot_v4.7-stable_mono_win64_console.exe --headless --path . res://Verification/Experimental/DataTable/DataTablePrototypeBenchmark.tscn
```

第一条命令使用固定种子生成小型数据、约一万行性能数据和六类错误样例，然后验证：

- 正常数据能生成规范化 IR、Manifest、未压缩 `.gdtb`、internal C# 类型、Debug JSON 和报告；
- 相同输入的产物完全一致；
- 缺列、数据行少列、重复键、非法 enum、越界和无效外键均产生精确诊断；
- 失败生成不会覆盖上一次成功产物。

生成数据和二进制位于本目录 `Artifacts/`，不纳入版本控制。`Generated/DataTablePrototype.Generated.cs` 由原型编译器生成并参与项目编译，禁止手工修改。

## 当前边界

- 只支持 UTF-8 CSV 与受控 JSON Profile；
- 只实现阶段 A 所需的 string、bool、int32、float64 和 enum；
- `.gdtb` 使用小端序且不压缩；
- 不包含加密、EditorPlugin、导出过滤、热更新或正式运行时加载 API；
- 不进入永久 `Verification/Automated/run_all.py` 回归。

## 当前 Windows 证据

2026-07-17 在 Godot 4.7 Mono Debug 构建中，10,004 行的两张表生成未压缩二进制共 812,979 bytes。一次 Headless 样本中加载耗时 9.553 ms、总托管分配 4,088,648 bytes、完整 GC 后保留托管内存约 1,851,808 bytes；预生成字符串键的 100,000 次查询耗时 11.037 ms、托管分配为 0。

该数据只用于证明原型路径可测，不是性能承诺或压缩 `Auto` 阈值。Release、其他硬件和移动平台仍需单独基准。
