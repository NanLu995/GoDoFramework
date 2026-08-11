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

## 能力全景图

<div class="godo-capability-list">
<section><h4>创建资源键</h4><p>从规范的 <code>res://</code> 路径或 <code>uid://</code> 标识创建稳定定位。</p><pre class="godo-capability-call"><code>ResourceKey key = ResourceKey.FromPath("res://Scenes/Main.tscn");</code></pre></section>
<section><h4>同步加载</h4><p>资源已准备好且调用点允许同步等待时使用；类型不匹配会失败。</p><pre class="godo-capability-call"><code>PackedScene scene = ResourceHub.Load&lt;PackedScene&gt;(key);</code></pre></section>
<section><h4>异步加载</h4><p>在加载流程中取得可等待操作，并读取进度、状态或结果。</p><pre class="godo-capability-call"><code>ResourceLoadOperation&lt;PackedScene&gt; operation = ResourceHub.LoadAsync&lt;PackedScene&gt;(key);
PackedScene scene = await operation.Completion;</code></pre></section>
<section><h4>观察活动操作</h4><p>用于调试与加载界面诊断，不应成为每帧业务控制条件。</p><pre class="godo-capability-call"><code>int loadingCount = ResourceHub.ActiveOperationCount;</code></pre></section>
</div>

ResourceHub 不返回 <code>null</code> 表示失败，也不维护第二套缓存或引用计数。完整签名见 [ResourceHub API](xref:GoDo.ResourceHub)。
