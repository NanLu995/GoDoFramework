---
translation_of: Docs/Manual/zh-cn/guides/resources/index.md
translation_source_hash: sha256:764aacc6c89ce7ae6b1fcd0bdc8f4292871940cad0894a1324e0bffb09d48d2a
---

# ResourceHub: load Godot resources by stable key

Use `ResourceHub` and `ResourceKey` to load scenes, UI, audio, and configuration rather than scattering paths and private caches through nodes. Create a key from a valid `res://` path or UID; handle `ResourceLoadException` at the loading boundary rather than returning `null`.

Async callers can share underlying loading. Cancelling one caller does not stop other callers waiting for the same resource, and ResourceHub adds no second cache or reference-counting system over Godot resources.

For manifests, progress, asynchronous loading, and scene changes together, see the [resource and scene workflow](../resources-and-scenes/index.md).
