---
translation_of: Docs/Manual/zh-cn/guides/resources-and-scenes/index.md
translation_source_hash: sha256:50e8efb78a9dd93d3e814a369dce8b54d0c95b6003e0dbe3d2e6625f06443656
---

# Manage Resource Manifests, Async Loading, and Scene Changes

ResourceHub is the framework's shared Godot Resource loading entry point. SceneService builds on it to load and replace the main content scene. Game code uses `ResourceKey` or semantic IDs from a manifest instead of scattering `ResourceLoader` calls and path strings.

## 1. Choose a path, UID, or semantic ID

A direct path fits a local, stable Resource:

```csharp
ResourceKey key = ResourceKey.FromPath("res://Scenes/Gameplay.tscn");
```

A UID remains more stable when files move:

```csharp
ResourceKey key = ResourceKey.ResolveUid("res://Scenes/Gameplay.tscn");
```

`ResolveUid` returns `uid://` when available and otherwise keeps the original `res://`. Do not hand-write UIDs; let Godot create and maintain them.

Larger projects should expose semantic IDs to game code:

```text
scene/gameplay -> uid://...
ui/icon_close  -> uid://...
audio/bgm_menu -> uid://...
```

Semantic IDs decouple game meaning from file location and allow manifests to be divided by package or feature.

## 2. Create and maintain ResourceManifest

After enabling the GoDo Framework editor plugin, use the top menu:

1. **Create Resource Manifest...** creates a `.tres` or `.res` manifest.
2. **Select Resource to Add...** selects several project assets and writes them after preview.
3. **Manage Resource Manifest...** edits IDs, converts paths to UIDs, or removes mappings.
4. **Validate Resource Manifest...** checks values, duplicate IDs, Locators, and resolvability without writing.

The add tool initially derives an ID from the path without `res://` or the extension. Change important entries to stable game semantics such as `ui/icon_close` before committing. Removing a mapping does not delete its Resource file.

Manifest writes require confirmation. Generating a missing UID also requires confirmation. Validation never repairs or modifies files.

## 3. Load the registry during startup

```csharp
ResourceManifest manifest =
    ResourceLoader.Load<ResourceManifest>("res://Data/ResourceManifest.tres");

ResourceRegistry.Load(manifest);
```

Merge several manifests in order:

```csharp
ResourceRegistry.LoadMerge(new[]
{
    coreManifest,
    gameplayManifest,
    platformOverrideManifest,
});
```

A later duplicate ID replaces an earlier one with a Warning, supporting explicit override layers. Empty IDs and null entries are skipped. Centralize merge order in Boot instead of allowing scenes to compete over the global registry.

Resolve and load from game code:

```csharp
ResourceKey iconKey = ResourceRegistry.GetKey("ui/icon_close");
Texture2D icon = ResourceHub.Load<Texture2D>(iconKey);
```

Use `GetKey` for required content and `TryGetKey` only for a genuinely optional asset.

## 4. Load synchronously or asynchronously

Small startup-critical content can load synchronously:

```csharp
PackedScene scene = ResourceHub.Load<PackedScene>(key);
```

Use an async operation for large content or loading UI:

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

Progress ranges from 0 to 1 and is delivered on Godot's main thread. Use a named method and unsubscribe in `finally`. Do not call `.Wait()`, `.Result`, or poll Godot's threaded API yourself.

Concurrent async requests for the same Key and type share one operation. Synchronous loading of that path during async work, or requesting another type, fails explicitly. Completed operations leave the active table; later loads continue to use Godot's cache.

## 5. Change the main content scene

```csharp
ISceneService scenes = Services.Get<ISceneService>();
ResourceKey gameplay = ResourceRegistry.GetKey("scene/gameplay");

try
{
    Node newScene = await scenes.ChangeAsync(gameplay);
}
catch (SceneChangeException exception)
{
    ErrorHub.Report(exception, "Game.Procedure", context: gameplay.Value);
    ShowSceneLoadFailure();
}
```

SceneService fully loads, verifies the PackedScene, instantiates it, and adds it to SceneTree before changing `CurrentScene`. A pre-commit failure leaves the old scene unchanged.

After commit, the old scene is `QueueFree()`d and released at frame end. Once `await` returns, use only the returned new scene and never access old scene Nodes.

## 6. Display loading progress

SceneService currently exposes polling properties:

```csharp
public override void _Process(double delta)
{
    if (_scenes?.IsChanging == true)
        _progressBar.Value = _scenes.Progress * 100.0;
}
```

Loading UI must live in a persistent UI layer or another owner that survives the old main scene. Hide or close it after the transition.

Only one scene change can run at a time; a second `ChangeAsync` throws `InvalidOperationException`. Centralize coordination in a Procedure, disable repeated input, and do not use fire-and-forget that loses exceptions.

## 7. Failure, cancellation, and shutdown

- Missing, mismatched, or failed Resource: `ResourceLoadException`.
- Scene load, instantiation, or attachment failure: `SceneChangeException`, retaining the target Key.
- SceneService leaves the tree or shuts down: an uncommitted change ends with a SceneChangeException containing `OperationCanceledException`.
- ResourceHub shutdown: unfinished waiters receive `OperationCanceledException`; Godot's underlying load may still finish.

Modules do not report before throwing. Add game context and report once at the Procedure or startup boundary.

## 8. Cache and lifetime

ResourceHub uses Godot `CacheMode.Reuse` and has no second LRU, reference count, or manual Unload. Retain Resource references only while needed.

Do not reload in `_Process()` or attempt to “clear cache” by freeing a Resource still referenced by scenes, materials, or scripts. Remote downloads, PCK/DLC, hot update, directory loading, and advanced caching are not current features.

## Common failures

- Registry is not loaded: Boot did not load a Manifest first.
- A moved file no longer resolves: use a UID or update its Locator with the management tool.
- One Resource produces conflicting behavior: callers requested the same path with different generic types.
- Loading UI disappears with the old scene: move it to a persistent UI layer.
- Repeated clicks start a second change: serialize through Procedure and lock the entry point.
- Code accesses an old Node after success: the old scene has entered QueueFree lifetime.
- One failure produces two logs: a lower and upper layer both reported; retain only the game boundary report.
- ResourceHub is expected to unload or hot-update: those capabilities are not provided.

For exact members, see <xref:GoDo.ResourceKey>, <xref:GoDo.ResourceRegistry>, <xref:GoDo.ResourceHub>, <xref:GoDo.ResourceLoadOperation%601>, <xref:GoDo.ISceneService>, and <xref:GoDo.SceneChangeException>.
