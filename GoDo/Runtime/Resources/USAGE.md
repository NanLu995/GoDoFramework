# GoDo.ResourceHub 使用指南

## 定位

ResourceHub 是 Scene、Audio、UI、Config 等运行时模块共享的 Godot Resource 加载入口。它包装 `Godot.ResourceLoader`，不替代 Godot 缓存，也不负责远程下载、资源包或热更新。

## ResourceKey

公共加载 API 不接收随意字符串，先创建经过验证的资源键：

```csharp
ResourceKey key = ResourceKey.Create("res://Scenes/Level01.tscn");
```

首版仅支持具体资源文件的规范化 `res://` 绝对路径，不支持相对路径、父目录跳转、目录路径和 `user://`。

## 同步加载

```csharp
PackedScene scene = ResourceHub.Load<PackedScene>(key);
```

资源不存在、Godot 加载失败或实际类型不兼容时抛出 `ResourceLoadException`。调用方应在 Scene、Audio 等功能边界捕获异常，再交给 ErrorHub 补充业务上下文；ResourceHub 不会先上报再抛出。

## 异步加载

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
```

GoDoRuntime 每帧轮询 Godot 的线程化加载状态。进度通知和 Completion 都在 Godot 主线程触发；不要阻塞等待，也不要直接提前调用 `ResourceLoader.LoadThreadedGet`。

## 并发语义

- 同一 ResourceKey、同一类型的并发请求返回同一个 `ResourceLoadOperation<T>`。
- 同一路径不同类型的并发请求会明确失败。
- 一个资源正在异步加载时，不允许同时通过 ResourceHub 同步加载。
- Shutdown 会停止 GoDo 调用方等待，但 Godot 已开始的底层加载可能继续完成。

## 缓存与释放

首版使用 Godot `CacheMode.Reuse`：

- ResourceHub 不维护引用计数。
- 不递归释放 PackedScene、纹理或音频依赖。
- 不提供手动 Unload、LRU 或内存预算。
- 调用方只在需要期间持有 Resource 引用。

## 未来扩展

- EditorPlugin：拖拽引用、Resource UID 与代码生成。
- Resource Pack：PCK、DLC、版本与挂载策略。
- Remote Asset：HTTP 下载、校验、重试、持久化和限流。
- Cache Policy：在真实内存数据证明有需要后评估强引用缓存与 LRU。
- Preload：明确资源持有者和释放时机后再设计。
