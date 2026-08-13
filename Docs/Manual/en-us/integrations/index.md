---
translation_of: Docs/Manual/zh-cn/integrations/index.md
translation_source_hash: sha256:30775f24cf5c5614674738ea3d55641a602a754cd3e0f5b5be792588227cb520
---

# Integrations and extensions

Optional GoDo integrations use adapter layers: the framework retains stable business interfaces while the project installs, upgrades, and validates the third-party plugin. Do not modify third-party source to adapt it to GoDo; validate upgrade risk and platform compatibility in the project.

The Friflo ECS integration is an exception: GoDo provides only a scene-level lifecycle host and does not abstract Friflo Entity, Component, Query, or System APIs. Game code using this capability therefore depends directly on Friflo.

## G.U.I.D.E-CSharp: input backend

With G.U.I.D.E-CSharp, GoDo `InputService` can read semantic actions, maintain Contexts, show device prompts, and provide runtime rebinding with persistence. See [Input](../guides/input/index.md) for installation, profiles, and the startup boundary, then [runtime rebinding](../guides/input-rebinding/index.md) for its UI.

Without that backend, do not assume rebinding or device-prompt capability exists. Check the relevant interface capability before displaying that UI.

## Phantom Camera: camera backend

With Phantom Camera, register its rig through `PhantomCameraRig`, then let game flow activate or restore cameras by stable `CameraId`. See [Camera](../guides/camera/index.md) for node configuration, changes, and failure boundaries.

Without Phantom Camera, derive an adapter from `CameraRig` for the project's chosen backend. Business code should not depend directly on third-party camera nodes.

## Friflo ECS: scene-level data processing

Friflo ECS suits batch simulation of many homogeneous entities, such as unit movement, projectiles, or status effects. Menus, saves, scene changes, audio, and ordinary UI should continue to use GoDo services or Godot nodes. The core package does not depend on Friflo; only projects selecting this capability install its separate adapter package.

Add `Friflo.Engine.ECS` 3.6.0 to the target `.csproj`, overlay `GoDoFramework-FrifloEcs-<version>.zip`, then restore and build again. The adapter archive does not bundle the NuGet assembly, but it includes the upstream MIT license. Add `EcsWorldHost` to the game scene that needs ECS, register game systems and entities, then explicitly set `IsRunning = true`. The World is released with its host when the scene exits and is not retained across scenes automatically.

Keep game components and systems in the game's own namespace. `GoDo.Integrations.FrifloEcs` identifies the current backend adapter; it does not promise that game ECS code can move to another framework at zero cost.

For a complete runnable path from components and systems to a game scene, see [Batch Scene Data with Friflo ECS](../guides/friflo-ecs/index.md).

## Integration checklist

1. Pin and record the third-party plugin version; do not treat an unverified latest release as a framework prerequisite.
2. Confirm that Godot recognizes the plugin, resources, and node types before installing the corresponding GoDo backend.
3. Validate both Debug and target export platforms; an editor-ready third-party plugin is not necessarily export-ready.
4. After upgrading either side, revalidate the smallest input, camera, or ECS scene and consult [Troubleshooting](../troubleshooting/index.md).

## Integration capability map

<div class="godo-capability-list">
<section><h4>Install the GUIDE Input backend</h4><p>Configure its Profile and persistence slot; game code continues to depend on IInputService.</p><pre class="godo-capability-call"><code>installer.Profile = inputProfile;
installer.PersistenceSlot = "input-bindings";</code></pre></section>
<section><h4>Use GUIDE extension capabilities</h4><p>Feature-detect rebinding, persistence, and prompt queries.</p><pre class="godo-capability-call"><code>input.TryGetRebinding(out IInputRebinding rebinding);
input.TryGetPromptQuery(out IInputPromptQuery prompts);</code></pre></section>
<section><h4>Configure a Phantom Camera Rig</h4><p>Set its backend and active/inactive priorities; CameraService switches it by CameraId.</p><pre class="godo-capability-call"><code>rig.PhantomCameraNode = pcam;
rig.ActivePriority = 20;
rig.InactivePriority = 0;</code></pre></section>
<section><h4>Start a scene-level ECS World</h4><p>Register game systems and entities before enabling updates; the World follows the host scene lifecycle.</p><pre class="godo-capability-call"><code>host.Systems.Add(new MovementSystem());
host.Store.CreateEntity(new Position(), new Velocity());
host.IsRunning = true;</code></pre></section>
</div>

See the [GuideInputBackendInstaller API](xref:GoDo.GuideInput.GuideInputBackendInstaller), [PhantomCameraRig API](xref:GoDo.PhantomCameraRig), and [EcsWorldHost API](xref:GoDo.Integrations.FrifloEcs.EcsWorldHost).
