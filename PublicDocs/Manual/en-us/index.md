---
translation_of: PublicDocs/Manual/zh-cn/index.md
translation_source_hash: sha256:d168da5a6749bf28156beafbb165db097850da7fbe0b7f48c15e14514f75b577
---

# GoDoFramework Manual

GoDoFramework provides reusable game flow, scenes, UI, audio, input, data, and diagnostics for Godot 4.x C# projects. It does not replace gameplay code or native Godot nodes; it defines lifecycle, failure, and collaboration boundaries for the shared systems that become difficult as a project grows.

This manual covers GoDoFramework 0.8.0. It requires Godot 4.7.0 .NET or later, with regression coverage through Godot 4.7.2 .NET. A newer Godot 4.x release triggers an unverified-version warning; complete your own build, automated tests, and critical-scene regression before shipping. See [Install, Upgrade, and Uninstall](getting-started/install-upgrade-uninstall.md) for installation and upgrade boundaries.

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
    <span>Learn the setup, boundaries, and constraints for G.U.I.D.E-CSharp, Phantom Camera, and Friflo ECS.</span>
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
