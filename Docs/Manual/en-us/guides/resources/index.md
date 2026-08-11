---
translation_of: Docs/Manual/zh-cn/guides/resources/index.md
translation_source_hash: sha256:cf1cc2e1da449542f16a8d661172a38625c1dfa818336694b13101fcdc8fa2f1
---

# ResourceHub: load Godot resources by stable key

Use `ResourceHub` and `ResourceKey` to load scenes, UI, audio, and configuration rather than scattering paths and private caches through nodes. Create a key from a valid `res://` path or UID; handle `ResourceLoadException` at the loading boundary rather than returning `null`.

Async callers can share underlying loading. Cancelling one caller does not stop other callers waiting for the same resource, and ResourceHub adds no second cache or reference-counting system over Godot resources.

For manifests, progress, asynchronous loading, and scene changes together, see the [resource and scene workflow](../resources-and-scenes/index.md).

## Capability map

<div class="godo-capability-list">
<section><h4>Create a resource key</h4><p>Create stable location from a canonical <code>res://</code> path or <code>uid://</code> identifier.</p><pre class="godo-capability-call"><code>ResourceKey key = ResourceKey.FromPath("res://Scenes/Main.tscn");</code></pre></section>
<section><h4>Load synchronously</h4><p>Use when the resource is prepared and the call site permits synchronous loading.</p><pre class="godo-capability-call"><code>PackedScene scene = ResourceHub.Load&lt;PackedScene&gt;(key);</code></pre></section>
<section><h4>Load asynchronously</h4><p>Obtain an awaitable operation with progress, status, and a typed result.</p><pre class="godo-capability-call"><code>ResourceLoadOperation&lt;PackedScene&gt; operation = ResourceHub.LoadAsync&lt;PackedScene&gt;(key);
PackedScene scene = await operation.Completion;</code></pre></section>
<section><h4>Observe active operations</h4><p>Use for diagnostics and loading UI, not as a per-frame gameplay condition.</p><pre class="godo-capability-call"><code>int loadingCount = ResourceHub.ActiveOperationCount;</code></pre></section>
</div>

ResourceHub does not return <code>null</code> for failure or maintain a second cache/reference count. See the [ResourceHub API](xref:GoDo.ResourceHub).
