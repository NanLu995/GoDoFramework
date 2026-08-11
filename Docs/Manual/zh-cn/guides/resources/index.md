# ResourceHub：用稳定键加载 Godot 资源

当业务代码需要加载场景、UI、音频或配置资源时，统一通过 `ResourceHub` 与 `ResourceKey` 定位。它适合替代散落在业务节点中的路径字符串和各自维护的加载缓存。

## 最小使用边界

1. 用 `ResourceKey.FromPath(...)` 或 UID 创建资源键。
2. 在流程、UI 或加载边界调用 ResourceHub；不要在 `_Process()` 中发起加载。
3. 资源不存在、类型不匹配或加载失败时处理 `ResourceLoadException`，不要把失败转换为 `null`。

资源键是资源定位，不是业务语义。项目需要稳定的业务名称时，在资源清单中维护语义 ID，再由清单解析为 `ResourceKey`。

## 关键规则

- `res://` 路径和 `uid://` 都必须是有效、规范的 Godot 资源地址。
- 异步请求可共享底层加载；取消一个调用方不会强制中断其他调用方仍在等待的资源。
- ResourceHub 不提供第二套引用计数或缓存策略；Godot 自身管理已加载 Resource 的生命周期。

需要资源清单、进度、异步加载和主场景切换一起工作的完整示例，阅读[资源与场景工作流](../resources-and-scenes/index.md)。
