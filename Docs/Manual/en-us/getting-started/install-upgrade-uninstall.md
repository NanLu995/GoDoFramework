---
translation_of: Docs/Manual/zh-cn/getting-started/install-upgrade-uninstall.md
translation_source_hash: sha256:2703fff4942309f56e9b6462999329600a034805700da2373d13a438f44eb3b3
---

# Install, Upgrade, and Uninstall the Framework

GoDoFramework is distributed as the complete `addons/godo_framework/` directory. Its editor plugin checks the environment and explicitly installs the single `GoDoRuntime` Autoload. It does not take ownership of the target project's `.csproj`, input map, export presets, or game scenes.

This page covers adding a specific framework version to an existing Godot C# project, safely upgrading it, and removing it completely.

## Before installation

The target project needs:

- Godot 4.7 .NET.
- Exactly one usable `.csproj` at the project root.
- At least one successful Debug C# build.
- .NET 8 for runtime targets; Android uses .NET 9 according to project requirements.

Commit or back up the project first. Do not copy the framework workbench's `project.godot`, `.csproj`, solution, Demo, Verification, `.godot`, `bin`, or `obj` into the game project.

## 1. Copy the complete framework directory

Copy the versioned package directory to:

```text
res://addons/godo_framework/
```

Do not select only the Runtime subdirectories that currently appear useful. Core, editor setup, and modules form a defined package boundary; partial copying makes health checks, compilation, and future upgrades inconsistent.

Optional integrations still need their own dependencies, such as GUIDE or Phantom Camera. Finish core setup first, then follow the relevant guide:

- [Input and the GUIDE backend](../guides/input/index.md)
- [Main cameras and Phantom Camera](../guides/camera/index.md)

## 2. Enable the editor plugin

Open in Godot:

```text
Project Settings → Plugins → GoDo Framework → Enable
```

Enabling the plugin only adds the top-level `GoDo Framework` menu and editor tools. It does not write an Autoload. The plugin uses GDScript, so its setup window should open even before the C# assembly is built.

If the plugin does not appear:

1. Confirm the exact directory is `addons/godo_framework/`.
2. Check that `addons/godo_framework/plugin.cfg` exists.
3. Inspect Godot editor output for GDScript loading errors.
4. Make sure the package was not copied as a nested `addons/godo_framework/godo_framework/` directory.

## 3. Complete a C# build

The target project must own its C# solution. Generate it through Godot's C# project tooling, then complete one Debug build.

The framework does not automatically:

- Create or modify `.csproj` and solution files.
- Change the assembly name or target framework.
- Add NuGet packages.
- Copy build configuration from the workbench repository.

Setup reports an error when the root has no `.csproj`, has several `.csproj` files, has no built Debug assembly, or contains framework source newer than that assembly.

## 4. Run the health check

Open:

```text
GoDo Framework → Setup...
```

The window checks, in order:

1. Godot version.
2. Framework Runtime scene completeness.
3. C# environment and build output.
4. Duplicate framework copies or Runtime paths.
5. `GoDoRuntime` Autoload state.

The health check is read-only. Fix every error before installing Runtime. Understand each warning instead of manually bypassing checks to enable the button.

## 5. Install Runtime explicitly

After all checks pass, click **Install Runtime**. The plugin installs:

```text
Name: GoDoRuntime
Path: res://addons/godo_framework/Core/GoDoRuntime.tscn
```

It rechecks actual state before installation and reads project configuration again afterward. A correct existing installation is not written twice.

Do not manually add another Runtime under **Project Settings → Globals/Autoload**. If another Autoload owns the name, or another name points to the same Runtime scene, the plugin reports the conflict without overwriting or deleting it.

## 6. Verify the installation

Confirm that:

- All core Setup checks pass.
- The Autoload list contains exactly one `GoDoRuntime`.
- The C# project rebuilds without errors.
- Required services are available after game startup.

Minimal verification code:

```csharp
using Godot;
using GoDo;

public partial class FrameworkProbe : Node
{
    public override void _Ready()
    {
        IProcedureService procedures = Services.Get<IProcedureService>();
        ISceneService scenes = Services.Get<ISceneService>();
        LogHub.Info("GoDo runtime is ready.", "Game.Boot");
    }
}
```

Remove the temporary Probe afterward. The game entry point starts its first Procedure; game and test scenes must not instantiate GoDoRuntime again.

## Install optional integrations

Enable integrations only after core setup. Each has an independent health check and should not be forced into compatibility by editing third-party source.

| Integration | Setup entry | Required by core |
|---|---|---:|
| GUIDE Input | `GoDo Framework → Editor Extensions → 输入映射配置 (GUIDE Input Settings)...` | No |
| Phantom Camera | `GoDo Framework → Editor Extensions → 幻影相机配置 (Phantom Camera Settings)...` | No |
| DataTable | `GoDo Framework → Data Tables → 数据表配置 (DataTable Configuration)...` | No; development-time tool |

Disabling or omitting an optional integration does not change core GoDoRuntime initialization. Game code must not reference an integration type that is not installed.

## Upgrade to a new version

Upgrade by replacing the complete directory, not by overwriting matching files. An incremental copy leaves source files removed by the new version, and Godot may still scan or compile them.

Recommended process:

1. Read the target release notes and check Godot/.NET, public API, and migration requirements.
2. Commit or back up the project, and ensure game code has not modified the framework directory.
3. Close Godot and every running game instance.
4. Remove the old `addons/godo_framework/` and copy the complete new directory to the same path.
5. Reopen Godot and wait for file scanning and C# project updates.
6. Complete a Debug build.
7. Open Setup and resolve every error and warning.
8. Regenerate DataTable and other outputs, then validate optional integrations.
9. Run the game project's automated tests and manually verify critical scenes.

When its name and path still match, upgrading does not require reinstalling GoDoRuntime. The plugin never silently rewrites game code or migrates public API calls.

### If game code modified the framework directory

Do not overwrite it and expect an automatic merge. Move game-specific changes into game code, an adapter, or an independent extension, then restore a clean framework package. Maintaining a private framework fork increases merge and regression cost for every upgrade.

### Roll back a failed upgrade

1. Close Godot.
2. Restore the complete framework directory and migration changes from the pre-upgrade commit or backup.
3. Remove outputs generated only by the new version and absent from the old one.
4. Reopen and build the project.
5. Run the old version's Setup and confirm Runtime path and uniqueness.

Do not continue with a mixed installation. Source, scenes, and generated code must come from one framework version.

## Disable temporarily or uninstall completely

Disabling **GoDo Framework** in the plugin list removes only editor menus and windows. It does not uninstall `GoDoRuntime`; the framework still initializes at game runtime.

For complete removal:

1. Open `GoDo Framework → Setup...`.
2. Click **Uninstall Runtime** and confirm.
3. Confirm there is no exact `GoDoRuntime` entry in Autoload.
4. Disable GoDo Framework in the plugin list.
5. Close Godot.
6. Delete the complete `addons/godo_framework/` directory.
7. Remove or migrate every `GoDo.*`, generated-code, and optional-integration reference in game code.
8. Reopen and build, then clean remaining configuration owned by the game project.

The uninstall button removes only an Autoload named `GoDoRuntime` that points exactly to the framework Runtime scene. It refuses when the path differs, another name owns the same path, or project configuration cannot be read, preventing accidental deletion of unrelated settings.

The plugin does not delete framework files, game scenes, resource manifests, saves, input mappings, or export presets. The project decides which game data should be removed.

## Common failures

- Install is disabled: inspect the earliest Setup error; common causes are a missing Debug build or an incorrect `.csproj` count.
- Runtime name conflict: another Autoload uses `GoDoRuntime`; establish ownership before changing it.
- Duplicate Runtime path: another name points to the same scene; keep one registration.
- Old types still compile after update: incremental copying left removed files; restore, then repeat a complete-directory replacement.
- Framework still runs after disabling the plugin: disabling editor tools does not uninstall the Autoload.
- The project fails after deleting the directory: game code still references GoDo types, or Runtime was not uninstalled first.
- An optional setup check is healthy but runtime behavior fails: a C# build and real-scene verification are still required; health checks are not complete functional tests.

After installation, continue with [Get Long-Lived Services and Publish Game Events](../guides/services-and-events/index.md), then follow the quick-start path through the first Procedure, scene, and UI.
