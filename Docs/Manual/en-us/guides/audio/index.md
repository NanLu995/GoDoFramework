---
translation_of: Docs/Manual/zh-cn/guides/audio/index.md
translation_source_hash: sha256:f70644cee86d67bb6502711a22dd4bfaeb48e16f4de42b7b40c7152f7036c4d0
---

# Audio: manage BGM, sound effects, and playback budget

Use `AudioService` for long-lived BGM and short sound effects. Procedures or another explicit coordinator select BGM; interaction code makes low-frequency SFX requests with priority and capacity in mind. Persist volume through Audio Bus and Settings rather than duplicating it at every playback point.

SFX capacity, same-resource limits, and priority are observable policy: do not assume every request plays. 3D SFX has separate follow, physics-frame, and distance budgets. Audio APIs run on the Godot main thread.

See the [UI and Audio workflow](../ui-and-audio/index.md) for complete examples.
