---
translation_of: Docs/Manual/zh-cn/guides/save/index.md
translation_source_hash: sha256:14762172ded21166e700257e9889a940491d5220195fcbf4f9fd993d9f2af1c9
---

# Save: persist game progress

`SaveService` manages local slots, codecs, corruption recovery, and load results. Use it for versioned game progress; use Settings for player preferences. Define stable `SaveSlot` values and let project codecs own payload format and migration.

Distinguish no save, backup recovery, and real failure. Do not write synchronously on a hot path, and do not treat local integrity checks as encryption or anti-tamper protection.

See the [Save, Settings, and Localization workflow](../save-settings-localization/index.md) for slots, codecs, and recovery.
