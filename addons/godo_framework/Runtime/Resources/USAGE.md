# ResourceHub 使用指南

## 定位与优势

ResourceHub 是 Scene、Audio、UI、Config 等模块共享的 Godot Resource 加载入口。它统一 `ResourceKey`、类型检查、线程化加载、进度、并发请求合并与主线程完成语义，同时继续使用 Godot `CacheMode.Reuse`，不建立第二套缓存。

使用前必须由 `GoDoRuntime.tscn` Autoload 完成初始化；所有公共 API 只能从 Godot 主线程调用。

## ResourceKey

```csharp
ResourceKey key = ResourceKey.Create("res://Scenes/Level01.tscn");
ResourceKey stableKey = ResourceKey.FromUid("uid://c8k2n4m8xj3fa");
```

ResourceKey 支持两类定位串：

- `res://`：具体文件的规范化 Godot 资源路径。会拒绝相对路径、目录、`//`、`.`、`..`、结尾斜杠、`user://` 和远程 URL。
- `uid://`：Godot 4.x Resource UID。UID 是不透明标识符，只校验前缀和非空 payload，不按路径规则规范化，也不在创建时检查资源是否存在。

`ResourceKey.IsUid` 可用于诊断日志或分支处理。`ResourceKey.FromPath(...)` 和 `ResourceKey.FromUid(...)` 是 `Create(...)` 的表意化别名。

如果手头只有 `res://` 路径，可以尝试解析为 UID：

```csharp
ResourceKey key = ResourceKey.ResolveUid("res://Scenes/Level01.tscn");
```

`ResolveUid` 使用 Godot 的资源 UID 表；找不到 UID 时会回退为原始 `res://` 路径键。

## ResourceManifest 与 ResourceRegistry

ResourceManifest 是可在 Godot 编辑器中维护的 Resource，用来记录业务语义 ID 到资源定位串的映射：

```text
ui/main_menu -> uid://...
item/sword_iron -> res://Items/SwordIron.tres
```

ResourceRegistry 是运行时映射表。业务代码通过语义 ID 解析 ResourceKey，再交给 ResourceHub 加载：

```csharp
ResourceManifest manifest =
    ResourceLoader.Load<ResourceManifest>("res://Data/ResourceManifest.tres");

ResourceRegistry.Load(manifest);

Texture2D icon = ResourceHub.Load<Texture2D>(
    ResourceRegistry.Resolve("ui/icon_close"));
```

语义：

- `Load(manifest)` 会清空旧表并加载单个清单。
- `LoadMerge(manifests)` 会清空旧表并按顺序合并多个清单。
- 重复 ID 以后者覆盖前者，并输出 Godot warning。
- 空 ID 和 null 记录会跳过并输出 warning。
- `Resolve(id)` 在未加载或找不到 ID 时抛出异常。
- `TryResolve(id, out key)` 在未加载或找不到 ID 时返回 false。

ResourceRegistry 只负责语义 ID 到 ResourceKey 的解析，不负责自动扫描目录、生成 Manifest、远程下载或热更新。

## 同步加载

```csharp
try
{
    PackedScene scene = ResourceHub.Load<PackedScene>(key);
}
catch (ResourceLoadException exception)
{
    ErrorHub.Report(exception, "LevelLoader", context: key.Value);
}
```

资源不存在、加载失败或实际类型不兼容时抛出 `ResourceLoadException`，不返回 null。ResourceHub 不会先 Report 再 throw；最了解业务上下文的上层只上报一次。

## 异步加载与进度

```csharp
ResourceLoadOperation<PackedScene> operation =
    ResourceHub.LoadAsync<PackedScene>(key);

operation.ProgressChanged += OnProgressChanged;
try
{
    PackedScene scene = await operation.Completion;
}
finally
{
    operation.ProgressChanged -= OnProgressChanged;
}

private void OnProgressChanged(float progress)
{
    GD.Print($"{progress:P0}");
}
```

`Status` 为 `Loading`、`Completed` 或 `Failed`，`Progress` 范围为 0–1。进度和 Completion 都由 GoDoRuntime 每帧轮询并在 Godot 主线程触发；进度监听者异常会交给 ErrorHub，不阻断加载。

## 并发与 Shutdown

- 同一 ResourceKey、同一类型的并发请求返回同一个操作实例。
- 同一路径按不同类型并发请求会明确失败。
- 异步加载期间不能对同一路径执行同步加载。
- 完成的操作会从 ResourceHub 活动表移除；后续请求继续复用 Godot 缓存，而不是旧操作对象。
- Shutdown 使等待方收到 `OperationCanceledException`；Godot 已启动的底层加载可能继续完成。

## 缓存、生命周期与性能

- ResourceHub 不维护引用计数、强引用 LRU 或递归释放。
- 调用方只在实际需要期间持有 Resource 引用。
- 每个异步操作复用进度数组，Update 复用缓冲列表；不要自行重复轮询 Godot API。
- `ActiveOperationCount` 只表示 ResourceHub 当前活动请求，不代表 Godot 全局缓存数量。

## 自动回归验证

`Verification/Automated/ResourceKeyRegression.tscn` 验证合法路径规范化、空白与非法前缀拒绝、非规范路径、父目录跳转、区分大小写的相等性、默认值、哈希集合、`uid://` 创建与空 UID 拒绝，以及 UID 反查失败时的路径回退。

```powershell
Godot_v4.7-stable_mono_win64_console.exe --headless --path . Verification/Automated/ResourceKeyRegression.tscn
```

成功退出码为 0，失败退出码为 1。

`Verification/Automated/ResourceRegistryRegression.tscn` 验证未加载状态、清单加载、解析成功、解析失败、`TryResolve`、重复 ID 覆盖、合并加载和清空语义。

`Verification/Automated/ResourceHubRegression.tscn` 复用现有强类型 `.tres`，验证同步加载、无效与缺失资源、同步类型不匹配、异步请求合并、同路径类型冲突、异步期间同步冲突，以及完成操作从活动表清理；不调用 Shutdown。

```powershell
Godot_v4.7-stable_mono_win64_console.exe --headless --path . Verification/Automated/ResourceHubRegression.tscn
```

ResourceHub runner 已通过 `dotnet build` 编译，并在 Godot 4.7 Mono Headless 中完成 5/5 项验证；成功退出码为 0，失败退出码为 1。

## 不负责的能力

远程下载、PCK/DLC、热更新、目录批量加载、手动 Unload、下载重试和高级缓存策略属于独立未来扩展，不应混入 ResourceHub 核心。

## 常见误用

| 应该 | 避免 |
|---|---|
| 上层捕获并补充业务上下文 | ResourceHub 和上层重复上报 |
| 具名方法订阅并在 finally 解绑进度 | 匿名 lambda 永久留在操作上 |
| `await operation.Completion` | 阻塞 `.Wait()` 或 `.Result` |
| 使用 ResourceKey | 在模块中散落 ResourceLoader 和字符串路径 |
