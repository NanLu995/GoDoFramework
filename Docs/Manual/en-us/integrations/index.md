---
translation_of: Docs/Manual/zh-cn/integrations/index.md
translation_source_hash: sha256:7f8bc80dfea13aa1b98a1cfc65e92a0b206f2be134ac41fb8a7390802cd7ef3e
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
