---
translation_of: Docs/Manual/zh-cn/index.md
translation_source_hash: sha256:16af23d6e4acfe0e44163d53580f04111ebff306bcbbc105835c244fed9b846a
---

# GoDoFramework Manual

GoDoFramework provides reusable game flow, scenes, UI, audio, input, data, and diagnostics for Godot 4.x C# projects. It does not replace gameplay code or native Godot nodes; it defines lifecycle, failure, and collaboration boundaries for the shared systems that become difficult as a project grows.

## Choose a reading path

<div class="godo-doc-grid">
  <a class="godo-doc-card" href="getting-started/index.md">
    <strong>Build a game skeleton</strong>
    <span>Install the Runtime, then add flow, scenes, UI, an interaction loop, audio, saves, and localization.</span>
  </a>
  <a class="godo-doc-card" href="guides/index.md">
    <strong>Solve a problem by module</strong>
    <span>If the framework is already installed, start directly with Procedure, Scene, UI, Input, Save, or another module.</span>
  </a>
  <a class="godo-doc-card" href="integrations/index.md">
    <strong>Integrate third-party capabilities</strong>
    <span>Learn the setup, boundaries, and constraints for G.U.I.D.E-CSharp and Phantom Camera.</span>
  </a>
  <a class="godo-doc-card" href="troubleshooting/index.md">
    <strong>Troubleshoot a runtime issue</strong>
    <span>Locate installation, resource, flow, UI, input, and diagnostic problems from their observable symptoms.</span>
  </a>
</div>

## When this framework fits

- A Godot C# project that needs consistent top-level flow, main scenes, screen UI, audio, input, saves, and settings.
- A team that needs explicit boundaries for asynchronous failure, node lifetime, and the Godot main thread.

Use the API Reference for exact types, members, parameters, and exceptions. This manual focuses on when to use a capability, how to combine it, and how to verify the result.
