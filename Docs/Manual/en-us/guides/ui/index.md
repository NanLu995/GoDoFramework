---
translation_of: Docs/Manual/zh-cn/guides/ui/index.md
translation_source_hash: sha256:d80e2c6e0826774caedaa7e946fc34fc076f2cab7a10e566f85dab8ab53abbc7
---

# UI: manage screen interfaces and back order

`UiService` manages screen UI separately from the main scene. Use `Scene` for scene-bound HUD, `View` for full pages, and `Modal` for topmost dialogs. Open by `UiId` from `UiConfig` for semantic project UI, or by `ResourceKey` for small direct cases.

The flow or coordinator that opens a view closes it. Do not use external `QueueFree()` to bypass service state. Async opening can be cancelled before instantiation, but cannot cancel shared ResourceHub loading; all UI operations are main-thread, non-frame work.

See the [UI and Audio workflow](../ui-and-audio/index.md) for layers, focus, and recovery.
