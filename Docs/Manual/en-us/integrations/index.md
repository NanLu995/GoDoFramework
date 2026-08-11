---
translation_of: Docs/Manual/zh-cn/integrations/index.md
translation_source_hash: sha256:084d5ffdc0362b588e4c5b8bff78ad9e98410cec0ed9e842ce04e6f0a701d3ad
---

# Integrations and extensions

Optional GoDo integrations use adapter layers: the framework retains stable business interfaces while the project installs, upgrades, and validates the third-party plugin. Do not modify third-party source to adapt it to GoDo; validate upgrade risk and platform compatibility in the project.

## G.U.I.D.E-CSharp: input backend

With G.U.I.D.E-CSharp, GoDo `InputService` can read semantic actions, maintain Contexts, show device prompts, and provide runtime rebinding with persistence. See [Input](../guides/input/index.md) for installation, profiles, and the startup boundary, then [runtime rebinding](../guides/input-rebinding/index.md) for its UI.

Without that backend, do not assume rebinding or device-prompt capability exists. Check the relevant interface capability before displaying that UI.

## Phantom Camera: camera backend

With Phantom Camera, register its rig through `PhantomCameraRig`, then let game flow activate or restore cameras by stable `CameraId`. See [Camera](../guides/camera/index.md) for node configuration, changes, and failure boundaries.

Without Phantom Camera, derive an adapter from `CameraRig` for the project's chosen backend. Business code should not depend directly on third-party camera nodes.

## Integration checklist

1. Pin and record the third-party plugin version; do not treat an unverified latest release as a framework prerequisite.
2. Confirm that Godot recognizes the plugin, resources, and node types before installing the corresponding GoDo backend.
3. Validate both Debug and target export platforms; an editor-ready third-party plugin is not necessarily export-ready.
4. After upgrading either side, revalidate the smallest input or camera scene and consult [Troubleshooting](../troubleshooting/index.md).

## Integration capability map

<div class="godo-capability-list">
<section><h4>Install the GUIDE Input backend</h4><p>Configure its Profile and persistence slot; game code continues to depend on IInputService.</p><pre class="godo-capability-call"><code>installer.Profile = inputProfile;
installer.PersistenceSlot = "input-bindings";</code></pre></section>
<section><h4>Use GUIDE extension capabilities</h4><p>Feature-detect rebinding, persistence, and prompt queries.</p><pre class="godo-capability-call"><code>input.TryGetRebinding(out IInputRebinding rebinding);
input.TryGetPromptQuery(out IInputPromptQuery prompts);</code></pre></section>
<section><h4>Configure a Phantom Camera Rig</h4><p>Set its backend and active/inactive priorities; CameraService switches it by CameraId.</p><pre class="godo-capability-call"><code>rig.PhantomCameraNode = pcam;
rig.ActivePriority = 20;
rig.InactivePriority = 0;</code></pre></section>
</div>

See the [GuideInputBackendInstaller API](xref:GoDo.GuideInput.GuideInputBackendInstaller) and [PhantomCameraRig API](xref:GoDo.PhantomCameraRig).
