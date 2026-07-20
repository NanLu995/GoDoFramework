# DataTableService 使用指南

## 定位

`DataTableService` 是 DataTable 离线编译器对应的运行时服务。框架只注册服务，不自动加载任何业务数据；业务加载流程决定何时加载 Base、DLC 或其他数据集。生成代码负责强类型解码，Service 负责 Manifest 校验、逐表加载、事务发布、缓存和卸载。

本模块不解析 CSV 或 Schema，不下载、挂载或回滚 PCK，也不实现热更新、签名、加密和业务版本选择。

## 上手

Schema 的 `data_set_id` 最后一段决定生成门面和默认目录。例如 `game.base` 生成 `BaseDataTables`，默认加载 `res://DataTables/Base/Runtime`：

```csharp
using Game.DataTables.Base;

await BaseDataTables.LoadAsync(
    progress => loadingView.SetProgress(progress.Ratio));

if (BaseDataTables.Items.TryGet("iron_sword", out ItemRow item))
    GD.Print(item.DisplayName);
```

框架启动只执行 `Services.Register<IDataTableService>()`。调用 `LoadAsync` 前不会读取 Manifest 或 `.gdtb`。

需要从其他已挂载目录加载同一套生成类型时，业务可显式指定目录：

```csharp
await BaseDataTables.LoadFromAsync("res://MountedData/Base/Runtime");
```

目录必须包含与生成代码匹配的 `manifest.json` 和其中列出的 `.gdtb`。

## Public API

- `IDataTableService.LoadAsync(definition, runtimeDirectory, progress, cancellationToken)`：完整加载并事务发布数据集。
- `IDataTableService.IsLoaded(dataSetId)`：判断数据集是否已经发布。
- `IDataTableService.GetTable<TTable>(dataSetId, tableId)`：获取已缓存的强类型表；主要由生成门面调用。
- `IDataTableService.Unload(dataSetId)`：释放 Service 持有的数据集引用。
- `DataTableSetDefinition` / `DataTableDefinition`：生成代码与 Service 之间的描述契约，不要求业务手写。
- `DataTableLoadProgress`：初始回调为 `0/N`，随后每完成一张表报告一次；空 Manifest 的比例为 1。
- `DataTableLoadException`：Manifest、版本、描述或二进制加载失败。

生成门面提供 `LoadAsync`、`LoadFromAsync`、`IsLoaded`、`Unload` 和按表复数命名的属性，例如 `Items`、`ItemCategories`。

## 失败语义

- Manifest 的 `data_set_id`、`format_version` 或 `protocol_version` 与生成代码不一致时，抛出 `DataTableLoadException`。
- Manifest 包含未知表、重复表、危险产物路径或与生成代码不同的文件名时拒绝加载。
- `.gdtb` 的 magic、版本、表 ID、字段数、大小、UTF-8、索引或摘要无效时，由生成读取器失败并包装为 `DataTableLoadException`。
- 取消抛出 `OperationCanceledException`，不会包装为加载异常。
- 全部表成功前不写入公开缓存；失败或取消后无法取得半加载表。
- 同一数据集从同一目录重复加载会复用现有实例；从不同目录加载同一 ID 必须先卸载。
- 数据集正在加载时，重复加载或卸载会明确失败，不排队。
- 未加载、目标导出中不存在的表或请求类型不匹配时，`GetTable<TTable>` 抛出 `InvalidOperationException`。

Service 不调用 `ErrorHub` 后再抛出，异常只由决定重试、降级或退出的业务边界处理一次。

## 生命周期、线程与进度

所有 public API 必须在 Godot 主线程调用。加载一张表仍是同步的完整文件读取与解码；多表数据集在表与表之间等待一个 Process 帧，使加载 UI 可以刷新。取消也只在表边界观察，因此不能中断正在解码的单张大表。

`Unload` 只释放 Service 对表的引用；业务仍持有表实例时，该实例会继续存活。`GoDoRuntime` 退出时取消未完成加载并清空全部缓存。

## 性能与内存

- 数据表只在显式加载时分配，不存在每帧更新。
- `GetTable` 和生成门面属性执行数据集/表字典查询；热点逻辑可以缓存返回的 Table 引用。
- `TryGet(id)` 使用生成的主键索引，正常查询不扫描整表。
- 当前读取器会把单个 `.gdtb` 读入内存，再构造字符串池、行数组和主键索引；压缩表解压时还会产生临时 payload。
- 单文件读取上限为 2 GiB，未压缩 payload 上限为 512 MiB。超大表分块和字节级进度尚未实现。

## Client / Server 与更新边界

导出插件会把目标 Manifest 映射为运行时 `manifest.json`。Service 只加载该 Manifest 实际列出的表，因此 ClientOnly / ServerOnly 子集可复用同一份生成代码；访问未进入目标包的表会明确失败。

热更新、DLC 下载和 PCK 挂载不属于本模块。外层系统完成可信包选择与挂载后，可以调用生成门面的 `LoadFromAsync`；替换同一数据集前必须先卸载。首版不承诺加载中替换或无缝回滚。

## 验证

- `Verification/Automated/DataTableServiceRegression.tscn`：真实 Base Manifest 与三张 `.gdtb` 的逐表进度、强类型查询、重复加载、取消、失败不发布和卸载。
- `Verification/Experimental/DataTable/DataTablePrototypeBenchmark.tscn`：绝对路径、`res://`、PCK、Zstd、损坏文件拒绝和查询性能。
- `Verification/Experimental/DataTable/verify_prototype.py`：生成确定性、校验、单表生成、过期检查和 Manifest 契约。

当前状态为首版验证中；Windows Godot 运行时已验证，移动端、AOT 和完整 ExportRelease 仍需正式验收。
