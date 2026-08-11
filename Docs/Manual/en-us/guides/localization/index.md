---
translation_of: Docs/Manual/zh-cn/guides/localization/index.md
translation_source_hash: sha256:063a25151ab181662cda0a08fc2c792485395b786e30a9e2bdfee003da9505c2
---

# Localization: change game text language

`LocalizationService` coordinates Godot translation resources, the active locale, and settings persistence. Use stable translation keys for game concepts; do not use visible text as business IDs. Refresh dynamic UI after a locale change and configure static scene text with Godot localization.

Remote language packs are out of scope. Font coverage, plural rules, RTL layout, and pseudo-localization require content and layout validation. Persist locale through Settings, not game-progress saves.

See the [Save, Settings, and Localization workflow](../save-settings-localization/index.md) for release checks.

## Capability map

<div class="godo-capability-list">
<section><h4>Read locale state</h4><p>Show the current/default locale, available locales, and pseudo-localization state.</p><pre class="godo-capability-call"><code>localization.DefaultLocale
localization.CurrentLocale
localization.AvailableLocales
localization.IsPseudolocalizationEnabled</code></pre></section>
<section><h4>Check locale support</h4><p>Normalize and validate imported or external locale codes.</p><pre class="godo-capability-call"><code>bool supported = localization.IsLocaleSupported(locale);</code></pre></section>
<section><h4>Translate dynamic text</h4><p>Translate code-generated text, with optional context for ambiguous keys.</p><pre class="godo-capability-call"><code>string text = localization.Translate("menu.start", context: "main-menu");</code></pre></section>
<section><h4>Translate plural text</h4><p>Provide a count and let translation resources select the locale's plural form.</p><pre class="godo-capability-call"><code>string text = localization.TranslatePlural("item.one", "item.many", count, context);</code></pre></section>
</div>

Settings applies and persists locale changes; Localization owns queries and translation. See the [ILocalizationService API](xref:GoDo.ILocalizationService).
