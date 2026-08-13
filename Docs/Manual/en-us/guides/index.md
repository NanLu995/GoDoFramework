---
translation_of: Docs/Manual/zh-cn/guides/index.md
translation_source_hash: sha256:46597321bc20a2b453766ecc1ba064a6bc44d868a4f11850694b100b04adc0b2
---

# Module guides

Quick start connects capabilities into one game flow. This section is organized by module for projects that are already installed and need to solve a specific engineering task. Each guide explains its use case, smallest calling boundary, failure semantics, and common misuse; use the API Reference for exact signatures.

## Runtime collaboration and flow

- [Services and EventChannel](services-and-events/index.md): choose direct calls, Godot signals, or broadcast events, and manage subscription lifetime.
- [Procedure](procedure-recovery/index.md): organize top-level game phases, cancellation, recovery, and concurrent changes.
- [Scene and ResourceHub](resources-and-scenes/index.md): maintain resource manifests, asynchronous loading, and main-scene changes.
- [Scheduler](scheduler/index.md): schedule delays, repeating work, and asynchronous waits scoped to an owner lifetime.
- [NodePool](node-pool/index.md): reuse frequently created nodes and restore state at rent/return boundaries.
- [Friflo ECS](friflo-ecs/index.md): batch-update homogeneous data in a scene-level World and manage its start, pause, and exit lifecycle.

## Game interaction and presentation

- [UI and Audio](ui-and-audio/index.md): manage screen UI layers, focus, BGM, sound effects, and voice capacity.
- [Input](input/index.md): read semantic input, change Contexts, and display device prompts.
- [Runtime rebinding](input-rebinding/index.md): provide rebinding, conflict resolution, defaults, and persistence.
- [Camera](camera/index.md): register, activate, and restore the primary camera; Phantom Camera is optional.

## Game data and release preparation

- [Config](configuration/index.md): create strongly typed, Inspector-authored, validated configuration.
- [DataTable](data-tables/index.md): generate, validate, and read data tables from CSV.
- [Save, Settings, and Localization](save-settings-localization/index.md): organize save slots, platform settings, and game text.

## Observability

- [Diagnostics](diagnostics/index.md): record logs, report errors, and inspect runtime state with the Debugger.

For a third-party input, camera, or ECS backend, read [Integrations and extensions](../integrations/index.md) before the corresponding module guide.
