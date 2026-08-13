---
translation_of: Docs/Manual/zh-cn/getting-started/index.md
translation_source_hash: sha256:0ee777b6be1b9d57e99e53c3c05aedba03eb4ac4c84ceadcad538cc36ff9d3bf
---

# Quick start: build a runnable game skeleton

This path is for developers who already use Godot and C#. Its goal is not to demonstrate every API, but to establish useful boundaries in a small project: Runtime owns framework services, Procedure owns top-level game phases, Scene owns main content, and UI owns screen interfaces.

After installation, complete these pages in order. Each one leaves a visible result.

1. This page: install Runtime and confirm that services are available.
2. [Create the first game flow](first-procedure.md): enter a Procedure from the game's boot scene.
3. [Change the first main scene](first-scene.md): load main content from the Procedure.
4. [Open a main menu and confirmation dialog](first-ui.md): create Scene, View, and Modal UI.
5. [Enter gameplay from the menu and return](switch-procedures.md): connect UI intent to Procedure changes through events.
6. [Add audio](add-audio.md), [save progress and settings](save-progress-and-settings.md), and [localize game text](localize-game-text.md): add common game foundations.

After this path, use the [module guides](../guides/index.md) to explore the capabilities your project needs.

## Prerequisites

- Godot 4.7.1 .NET edition.
- A usable C# solution that has completed at least one Debug build.
- .NET 8 for the target project; use .NET 9 for Android builds when the project requires it.

## 1. Copy the framework directory

Copy the complete core directory into the target project. Do not split Core, editor setup, or core Runtime modules.

```text
addons/godo_framework/
```

The core package may omit `Integrations/`; a missing optional directory does not prevent the editor plugin or Runtime setup. Add the required overlay package and third-party dependency later for GUIDE Input or Phantom Camera, following [Integrations and extensions](../integrations/index.md).

Do not copy this repository's `project.godot`, `.csproj`, verification scenes, or demos as target-project configuration.

## 2. Enable the editor plugin

In Godot, open **Project Settings → Plugins** and enable `GoDo Framework`. This registers editor tools only; it does not install the Autoload automatically.

## 3. Check and install Runtime

1. Complete a C# Debug build for the target project.
2. Open **GoDo → Setup...** from the editor menu.
3. Resolve every reported issue.
4. When all checks pass, explicitly select **Install Runtime**.

The plugin installs the single `GoDoRuntime` Autoload only after an explicit action. Under **Project configuration → C# project**, you may also preview and confirm missing GoDo-owned conditional compilation rules. Neither operation changes input mappings, export presets, or gameplay scenes.

## 4. Confirm that services are available

Business code can obtain registered long-lived services through `Services.Get<T>()`:

```csharp
using GoDo;

IProcedureService procedures = Services.Get<IProcedureService>();
IUiService ui = Services.Get<IUiService>();
IAudioService audio = Services.Get<IAudioService>();
```

The game's entry point starts its first Procedure. Do not initialize GoDoRuntime again in a gameplay scene or put menu and level flow inside GoDoRuntime.

## Expected result

- The Godot Autoload list contains only one `GoDoRuntime`.
- The Setup window reports that framework checks pass.
- C# code can obtain registered services.
