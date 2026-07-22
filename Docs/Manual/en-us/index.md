---
translation_of: Docs/Manual/zh-cn/index.md
translation_source_hash: sha256:05831465c36a7477c0ea3cc6d2e7fc89e31d13b8498401c07c37167e5bb4f0b4
---

# GoDoFramework User Manual

GoDoFramework is a game-development framework for Godot 4.7.1 C# projects. It provides consistent ways to handle scene transitions, UI navigation, audio, input, saves, settings, and top-level game flow so you can focus on the game itself.

## Projects it is designed for

- Godot C# projects that want consistent ways to use scenes, UI, audio, input, saves, and game flow.
- Growing teams that need explicit failure behavior, lifecycle rules, and thread constraints.

## What you can build with it

- Organize top-level flow from the main menu through gameplay and results.
- Switch main scenes and manage HUDs, menus, and modal dialogs.
- Play music and sound effects, read input, and control the main camera.
- Save game data and player settings.
- Load resources, read configuration, and diagnose runtime problems consistently.

The framework does not design characters, combat, or level rules for you, and it does not prevent you from using normal Godot nodes and scenes. It provides reusable foundations that can be shared across projects.

## Start here

Start with the [5-minute quick start](getting-started/index.md) to install and verify Runtime. Then follow the tutorials through the first Procedure, scene, UI, flow change, audio, saves, and localization.

After the introductory path, choose by task:

- Framework setup and version maintenance: [Install, Upgrade, and Uninstall](getting-started/install-upgrade-uninstall.md).
- Build a framework mental model: [Services and Events](guides/services-and-events/index.md) and [Procedure Recovery](guides/procedure-recovery/index.md).
- Build game features: [Input](guides/input/index.md), [Camera](guides/camera/index.md), and [UI and Audio](guides/ui-and-audio/index.md).
- Manage game data: [Resources and Scenes](guides/resources-and-scenes/index.md), [Typed Configuration](guides/configuration/index.md), and [Data Tables](guides/data-tables/index.md).
- Improve release quality: [Saves, Settings, and Localization](guides/save-settings-localization/index.md) and [Logs and Diagnostics](guides/diagnostics/index.md).
- When something fails, start with symptom-based [Troubleshooting](troubleshooting/index.md).

Use API Reference when you need exact types, members, parameters, or exceptions.
