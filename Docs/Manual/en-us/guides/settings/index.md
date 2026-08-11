---
translation_of: Docs/Manual/zh-cn/guides/settings/index.md
translation_source_hash: sha256:94d17d675212d365029071571fd2e3fa15c4da4728b03256c99ae3d95ae49469
---

# Settings: apply and persist player preferences

`SettingsService` manages platform-aware settings such as volume, locale, window mode, resolution, and VSync. Check a platform capability before presenting its control, apply and inspect the result, then persist at a suitable confirmation point.

Do not assume desktop-window capability on mobile. UI expresses player choice; the service handles adaptation and results. Use its state and events when audio or language changes refresh UI.

See the [Save, Settings, and Localization workflow](../save-settings-localization/index.md) for the full flow.
