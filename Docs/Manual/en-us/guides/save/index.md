---
translation_of: Docs/Manual/zh-cn/guides/save/index.md
translation_source_hash: sha256:7412de1d8460cad00694a04df5c71bdf7aef264b74037d477c2ac1c76b739b50
---

# Save: persist game progress

`SaveService` manages local slots, codecs, corruption recovery, and load results. Use it for versioned game progress; use Settings for player preferences. Define stable `SaveSlot` values and let project codecs own payload format and migration.

Distinguish no save, backup recovery, and real failure. Do not write synchronously on a hot path, and do not treat local integrity checks as encryption or anti-tamper protection.

See the [Save, Settings, and Localization workflow](../save-settings-localization/index.md) for slots, codecs, and recovery.

## Capability map

<div class="godo-capability-list">
<section><h4>Create a stable slot</h4><p>The slot name is persistent protocol, independent of UI text.</p><pre class="godo-capability-call"><code>SaveSlot slot = SaveSlot.Create("player-01");</code></pre></section>
<section><h4>Save versioned data</h4><p>The project codec owns serialization, compatibility, and migration.</p><pre class="godo-capability-call"><code>saves.Save(slot, state, version: 3, codec);</code></pre></section>
<section><h4>Load and distinguish results</h4><p>Handle success, NotFound, backup recovery, and corruption separately.</p><pre class="godo-capability-call"><code>SaveLoadResult&lt;GameState&gt; result = saves.Load(slot, codec);</code></pre></section>
<section><h4>Inspect and delete</h4><p>Build slot UI or perform an explicit delete operation.</p><pre class="godo-capability-call"><code>bool exists = saves.Exists(slot);
bool deleted = saves.Delete(slot);</code></pre></section>
</div>

These APIs synchronously access local storage and do not belong in frame loops. See the [ISaveService API](xref:GoDo.ISaveService).
