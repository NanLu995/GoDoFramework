---
translation_of: Docs/Manual/zh-cn/guides/localization/index.md
translation_source_hash: sha256:3ff6166f45953b9cdf8b40f76ffe3002df4fc0eed393f2fa7cf81afa956c5935
---

# Localization: change game text language

`LocalizationService` coordinates Godot translation resources, the active locale, and settings persistence. Use stable translation keys for game concepts; do not use visible text as business IDs. Refresh dynamic UI after a locale change and configure static scene text with Godot localization.

Remote language packs are out of scope. Font coverage, plural rules, RTL layout, and pseudo-localization require content and layout validation. Persist locale through Settings, not game-progress saves.

See the [Save, Settings, and Localization workflow](../save-settings-localization/index.md) for release checks.
