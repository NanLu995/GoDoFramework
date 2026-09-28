# UI：管理屏幕界面与返回顺序

`UiService` 管理独立于主场景的屏幕 UI。使用 `UiConfig` 与 `UiId` 时，业务用语义标识打开界面；小型项目也可以直接用 `ResourceKey` 打开场景。

## 选择层级

- `Scene`：与当前主场景关联的 HUD、关卡提示；主场景切换后自动清理。
- `View`：设置、背包等完整页面；返回时恢复被遮挡的页面。
- `Modal`：确认框等最上层界面；优先由 `TryGoBack()` 关闭。

## 关键规则

- 由打开 UI 的流程或协调者负责关闭它；不要用外部 `QueueFree()` 绕过服务记录。
- 异步打开可以取消；取消阻止实例化与挂载，但不会停止 ResourceHub 的共享资源加载。
- 打开、查询与关闭都必须在 Godot 主线程，且不属于每帧操作。

层级、焦点、失败恢复和与音频协作的完整示例见[UI 与 Audio 工作流](../ui-and-audio/index.md)。

## 能力全景图

<div class="godo-capability-list">
<section><h4>加载语义 UI 配置</h4><p>使用 <code>UiId</code> 前加载并校验 <code>UiConfig</code>。</p><pre class="godo-capability-call"><code>ui.LoadUiConfig(configKey);</code></pre></section>
<section><h4>同步或异步打开</h4><p>已加载资源可同步打开；需要进度与取消时使用异步泛型重载。</p><pre class="godo-capability-call"><code>SettingsView view = ui.Open&lt;SettingsView&gt;(UiIds.Settings);
SettingsView view = await ui.OpenAsync&lt;SettingsView&gt;(UiIds.Settings, Configure, OnProgress, cancellationToken);</code></pre></section>
<section><h4>按资源键直接打开</h4><p>小型项目可跳过语义配置，明确提供资源键和层级。</p><pre class="godo-capability-call"><code>Control view = ui.Open(viewKey, UiLayer.View);</code></pre></section>
<section><h4>查询打开与加载状态</h4><p>区分已经挂载的实例和仍在加载的请求。</p><pre class="godo-capability-call"><code>ui.IsOpen(id)
ui.GetOpenCount(id)
ui.IsOpening(id)
ui.GetOpeningCount(id)
ui.TryGetTop(id, out Control view)</code></pre></section>
<section><h4>取消请求与管理缓存</h4><p>可按 ID 或层级取消未提交请求，并检查或清理复用实例。</p><pre class="godo-capability-call"><code>ui.CancelOpenRequests(id);
ui.HasCachedInstance(id);
ui.ClearCachedInstance(id);
ui.ClearCachedInstances();</code></pre></section>
<section><h4>关闭、返回与作用域持有</h4><p>关闭一个、同 ID 全部、某层全部，或退回指定界面；作用域适合确保离开流程时清理。</p><pre class="godo-capability-call"><code>ui.TryClose(view);
ui.CloseAll(id);
ui.CloseAll(UiLayer.Modal);
ui.CloseTo(id);
ui.TryGoBack();
using UiScope&lt;SettingsView&gt; scope = ui.OpenScoped&lt;SettingsView&gt;(id);</code></pre></section>
</div>

打开、查询、取消和关闭必须在 Godot 主线程执行。精确重载、返回值与异常见 [IUiService API](xref:GoDo.IUiService)。
