---
translation_of: Docs/Manual/zh-cn/guides/scene/index.md
translation_source_hash: sha256:8a80246ed814cc55e51f34f8056bb2fc99096095e1caaae266b66e4e25a02359
---

# Scene: change main content scenes

`SceneService` replaces `SceneTree.CurrentScene`; use it for levels and main content, not HUD, settings, or confirmation UI. Obtain `ISceneService` from the current Procedure and await `ChangeAsync(ResourceKey)`. Do not access the old scene after success because it is already being released.

Only one change may run at a time. Coordinate it in top-level flow, handle `SceneChangeException`, and use the progress/cancellation overload when the UI needs it. Cancellation cannot undo an already-started scene commit.

See the [resource and scene workflow](../resources-and-scenes/index.md) for the complete path.

## Capability map

<div class="godo-capability-list">
<section><h4>Change the main scene</h4><p>Load, instantiate, and commit a new <code>SceneTree.CurrentScene</code>.</p><pre class="godo-capability-call"><code>Node scene = await scenes.ChangeAsync(sceneKey);</code></pre></section>
<section><h4>Observe progress and cancel waiting</h4><p>Feed 0–1 progress to loading UI; cancellation applies only before commit.</p><pre class="godo-capability-call"><code>Node scene = await scenes.ChangeAsync(sceneKey, OnProgress, cancellationToken);</code></pre></section>
<section><h4>Read change state</h4><p>Disable duplicate entry points or display loading state; do not poll it to drive gameplay.</p><pre class="godo-capability-call"><code>scenes.IsChanging
scenes.Progress</code></pre></section>
</div>

All calls are Godot-main-thread operations. See the [ISceneService API](xref:GoDo.ISceneService) for exact signatures and failure boundaries.
