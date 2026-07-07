# ResourceHub 使用指南

## 定位与优势

ResourceHub 是 Scene、Audio、UI、Config 等模块共享的 Godot Resource 加载入口。它统一 `ResourceKey`、类型检查、线程化加载、进度、并发请求合并与主线程完成语义，同时继续使用 Godot `CacheMode.Reuse`，不建立第二套缓存。

使用前必须由 `GoDoRuntime.tscn` Autoload 完成初始化；所有公共 API 只能从 Godot 主线程调用。

## ResourceKey

```csharp
ResourceKey key = ResourceKey.Create("res://Scenes/Level01.tscn");
```

首版仅支持具体文件的规范化 `res://` 路径，不支持相对路径、目录、`user://`、远程 URL 或父目录跳转。无效路径在创建 ResourceKey 时立即失败。

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

## 不负责的能力

远程下载、PCK/DLC、热更新、目录批量加载、手动 Unload、下载重试和高级缓存策略属于独立未来扩展，不应混入 ResourceHub 核心。

## 常见误用

| 应该 | 避免 |
|---|---|
| 上层捕获并补充业务上下文 | ResourceHub 和上层重复上报 |
| 具名方法订阅并在 finally 解绑进度 | 匿名 lambda 永久留在操作上 |
| `await operation.Completion` | 阻塞 `.Wait()` 或 `.Result` |
| 使用 ResourceKey | 在模块中散落 ResourceLoader 和字符串路径 |
