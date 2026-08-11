---
translation_of: Docs/Manual/zh-cn/guides/ui/index.md
translation_source_hash: sha256:aff055cbb081abf9aece7618a9aa1e0fb5e370da0ae71fb27000ee4b47061d46
---

# UI: manage screen interfaces and back order

`UiService` manages screen UI separately from the main scene. Use `Scene` for scene-bound HUD, `View` for full pages, and `Modal` for topmost dialogs. Open by `UiId` from `UiConfig` for semantic project UI, or by `ResourceKey` for small direct cases.

The flow or coordinator that opens a view closes it. Do not use external `QueueFree()` to bypass service state. Async opening can be cancelled before instantiation, but cannot cancel shared ResourceHub loading; all UI operations are main-thread, non-frame work.

See the [UI and Audio workflow](../ui-and-audio/index.md) for layers, focus, and recovery.

## Capability map

<div class="godo-capability-list">
<section><h4>Load semantic UI configuration</h4><p>Load and validate <code>UiConfig</code> before using <code>UiId</code>.</p><pre class="godo-capability-call"><code>ui.LoadUiConfig(configKey);</code></pre></section>
<section><h4>Open synchronously or asynchronously</h4><p>Open prepared resources synchronously, or use the typed async overload for progress and cancellation.</p><pre class="godo-capability-call"><code>SettingsView view = ui.Open&lt;SettingsView&gt;(UiIds.Settings);
SettingsView view = await ui.OpenAsync&lt;SettingsView&gt;(UiIds.Settings, Configure, OnProgress, cancellationToken);</code></pre></section>
<section><h4>Open directly by resource key</h4><p>Small projects can provide the resource and layer explicitly.</p><pre class="godo-capability-call"><code>Control view = ui.Open(viewKey, UiLayer.View);</code></pre></section>
<section><h4>Inspect open and loading state</h4><p>Distinguish mounted instances from requests that are still loading.</p><pre class="godo-capability-call"><code>ui.IsOpen(id)
ui.GetOpenCount(id)
ui.IsOpening(id)
ui.GetOpeningCount(id)
ui.TryGetTop(id, out Control view)</code></pre></section>
<section><h4>Cancel requests and manage cached instances</h4><p>Cancel uncommitted requests by ID/layer, then inspect or clear reuse state.</p><pre class="godo-capability-call"><code>ui.CancelOpenRequests(id);
ui.HasCachedInstance(id);
ui.ClearCachedInstance(id);
ui.ClearCachedInstances();</code></pre></section>
<section><h4>Close, go back, and own a scope</h4><p>Close one, all matching views, a layer, or back to a target; a scope guarantees cleanup with its owner.</p><pre class="godo-capability-call"><code>ui.TryClose(view);
ui.CloseAll(id);
ui.CloseAll(UiLayer.Modal);
ui.CloseTo(id);
ui.TryGoBack();
using UiScope&lt;SettingsView&gt; scope = ui.OpenScoped&lt;SettingsView&gt;(id);</code></pre></section>
</div>

Opening, querying, cancellation, and closing are Godot-main-thread operations. See the [IUiService API](xref:GoDo.IUiService) for overloads, return values, and exceptions.
