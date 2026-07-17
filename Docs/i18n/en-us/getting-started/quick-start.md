---
translation_of: Docs/i18n/zh-cn/getting-started/quick-start.md
translation_source_hash: sha256:73b2a71bff764d61b3bf4517290e47b3e356f8cd3c3931dc81f21e1a5b06818f
---

# 5-Minute Quick Start

This page adds GoDoFramework to an existing Godot 4.7 C# project and verifies that its Runtime services are available.

## Prerequisites

- The .NET edition of Godot 4.7.
- A working C# solution that has completed at least one successful Debug build.
- .NET 8 for the target project; Android builds use .NET 9 according to the project requirements.

## 1. Copy the framework directory

Copy the complete directory into the target project without splitting its internal modules:

```text
addons/godo_framework/
```

Do not copy this repository's `project.godot`, `.csproj`, verification scenes, or Demo as target-project configuration.

## 2. Enable the editor plugin

Open **Project Settings → Plugins** in Godot and enable `GoDo Framework`. Enabling the plugin only registers editor tools; it does not install an Autoload automatically.

## 3. Check and install the Runtime

1. Complete a C# Debug build of the target project.
2. Open **Project → Tools → GoDo Framework → Setup...**.
3. Resolve errors shown by the setup window.
4. When all checks pass, explicitly select **Install Runtime**.

The plugin installs only the single `GoDoRuntime` Autoload. It does not modify the `.csproj`, input map, export presets, or game scenes.

## 4. Get services from game code

```csharp
using GoDo;

IProcedureService procedures = Services.Get<IProcedureService>();
IUiService ui = Services.Get<IUiService>();
IAudioService audio = Services.Get<IAudioService>();
```

The game entry point starts its own first Procedure. Do not initialize GoDoRuntime again in a game scene, and do not place menu or level flow inside GoDoRuntime.

## Expected result

- The Godot Autoload list contains exactly one `GoDoRuntime`.
- The framework checks in the Setup window pass.
- C# code can obtain registered long-lived services through `Services.Get<T>()`.

## Next steps

- Read the Chinese game-development and project-structure guides while their translations are prepared.
- Use module guides to confirm failure behavior, lifecycle, performance, and verification scope.
