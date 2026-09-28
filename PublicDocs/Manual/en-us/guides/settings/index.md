---
translation_of: PublicDocs/Manual/zh-cn/guides/settings/index.md
translation_source_hash: sha256:ca3fe0d6580d6f4f6715a2437d86ae01b0d06a8ef056cd84cf9df9081a485b6d
---

# Settings: apply and persist player preferences

`SettingsService` manages platform-aware settings such as volume, locale, window mode, resolution, and VSync. Check a platform capability before presenting its control, apply and inspect the result, then persist at a suitable confirmation point.

Do not assume desktop-window capability on mobile. UI expresses player choice; the service handles adaptation and results. Use its state and events when audio or language changes refresh UI.

See the [Save, Settings, and Localization workflow](../save-settings-localization/index.md) for the full flow.

## Capability map

<div class="godo-capability-list">
<section><h4>Read platform, capabilities, and snapshot</h4><p>Decide whether a control belongs in the UI before reading its current value.</p><pre class="godo-capability-call"><code>settings.Platform
settings.Capabilities
settings.Current
settings.Supports(SettingsCapability.WindowMode)</code></pre></section>
<section><h4>Load and apply at startup</h4><p>Read persisted state; on first run, prefer a system language supported by the project and otherwise use the project default locale.</p><pre class="godo-capability-call"><code>SettingsLoadStatus status = settings.LoadAndApply();</code></pre></section>
<section><h4>Apply volume and locale</h4><p>Apply immediately to Audio or Localization, then save explicitly.</p><pre class="godo-capability-call"><code>settings.SetMasterVolume(1f);
settings.SetBgmVolume(0.7f);
settings.SetSfxVolume(0.9f);
settings.SetLocale("en-US");</code></pre></section>
<section><h4>Apply display settings</h4><p>Expose and call only capabilities supported by the platform.</p><pre class="godo-capability-call"><code>settings.SetWindowMode(SettingsWindowMode.Windowed);
settings.SetResolution(new Vector2I(1920, 1080));
settings.SetVSync(true);</code></pre></section>
<section><h4>Persist or restore defaults</h4><p>Save confirmed choices; reset reapplies the project defaults.</p><pre class="godo-capability-call"><code>settings.Save();
settings.ResetToDefaults();</code></pre></section>
</div>

Applying settings is a Godot-main-thread operation; unsupported platform features are reported as results. See the [ISettingsService API](xref:GoDo.ISettingsService).
