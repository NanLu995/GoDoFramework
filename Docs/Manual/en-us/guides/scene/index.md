---
translation_of: Docs/Manual/zh-cn/guides/scene/index.md
translation_source_hash: sha256:b004feb77a5a24046ba215f629f144e486bc2c7dbaa19de00016f4fe3706b71e
---

# Scene: change main content scenes

`SceneService` replaces `SceneTree.CurrentScene`; use it for levels and main content, not HUD, settings, or confirmation UI. Obtain `ISceneService` from the current Procedure and await `ChangeAsync(ResourceKey)`. Do not access the old scene after success because it is already being released.

Only one change may run at a time. Coordinate it in top-level flow, handle `SceneChangeException`, and use the progress/cancellation overload when the UI needs it. Cancellation cannot undo an already-started scene commit.

See the [resource and scene workflow](../resources-and-scenes/index.md) for the complete path.
