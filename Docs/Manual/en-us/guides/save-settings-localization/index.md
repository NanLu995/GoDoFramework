---
translation_of: Docs/Manual/zh-cn/guides/save-settings-localization/index.md
translation_source_hash: sha256:7faea189b7b31deb713bff660b9430684ae73ed341396f08c142ac67a2781a37
---

# Design Multi-Slot Saves, Cross-Platform Settings, and Localization

SaveService stores game progress, SettingsService stores volume, locale, and platform display preferences, and LocalizationService queries current-language content. Keep these responsibilities separate: do not duplicate player settings in every progress slot, and do not make localization persist locale choice.

## 1. Plan stable save slots

Slots allow only ASCII letters, digits, underscores, and hyphens, up to 64 characters:

```csharp
SaveSlot autosave = SaveSlot.Create("autosave");
SaveSlot slot1 = SaveSlot.Create("slot-1");
```

A slot name is a file protocol, not player-entered display text. A multi-slot UI keeps its own titles and maps them to stable slots.

```csharp
SaveLoadResult<GameSave> result = saves.Load(slot1, GameSaveCodec.Instance);
switch (result.Status)
{
    case SaveLoadStatus.NotFound:
        ShowEmptySlot();
        break;
    case SaveLoadStatus.Loaded:
        ShowSlot(result.Value, result.SavedAtUtc);
        break;
    case SaveLoadStatus.RecoveredFromBackup:
        ErrorHub.Warn("Save recovered from backup.", "Game.Save", slot1.Value);
        ShowSlot(result.Value, result.SavedAtUtc);
        break;
}
```

NotFound is a normal result. Deleting a slot removes its main, backup, and temporary files and returns `false` when none exist. Require player confirmation first.

## 2. Let the Codec migrate versions

The framework container protects file integrity but does not understand game fields. Decode old formats according to `dataVersion` and return the current model:

```csharp
public GameSave Decode(ReadOnlySpan<byte> payload, int dataVersion)
{
    return dataVersion switch
    {
        1 => MigrateFromV1(DecodeV1(payload)),
        2 => MigrateFromV2(DecodeV2(payload)),
        CurrentVersion => DecodeCurrent(payload),
        _ => throw new InvalidDataException($"Unsupported save version: {dataVersion}")
    };
}
```

Do not overwrite the old file immediately after a successful read. Validate the migrated state in the game, then write the current version at the next explicit safe milestone. Retain old-version fixtures and test every supported migration path.

Field renames, enum reorderings, Resource-ID changes, and unit changes can all require migration. Debug and Release must use the same authoritative Codec and container format.

## 3. Understand backup recovery

Save first writes and rereads `.tmp`, promotes a healthy old main file to `.bak`, and commits the new file. A corrupt main file never replaces a healthy backup.

`RecoveredFromBackup` means this value came from `.bak`; the framework does not silently rewrite the main file. Recommended handling:

1. Tell the player that recent progress may have been lost.
2. Let the player enter and inspect the recovered state.
3. Write a new main file at the next safe save point.

Both files corrupt, Codec failure, and I/O failure throw `SaveException`. Report once at the boundary that can retry, choose another slot, or return to title.

## 4. Control save frequency and payload

The current API performs synchronous main-thread I/O for ordinary small saves. Do not save every frame and do not wrap Godot file paths in `Task.Run`.

Save at explicit milestones such as level completion, checkpoints, settings confirmation, or a defined focus-loss policy. Build a plain-data snapshot before autosaving instead of letting the Codec traverse a changing scene tree.

Payload is limited to 64 MiB. Store stable IDs and required state, not texture, audio, scene, or other large Resource binaries.

## 5. Load and apply settings

Before Boot starts the first Procedure:

```csharp
ISettingsService settings = Services.Get<ISettingsService>();
SettingsLoadStatus status = settings.LoadAndApply();

if (status == SettingsLoadStatus.RecoveredFromBackup)
    ErrorHub.Warn("Settings recovered from backup.", "Game.Settings");
```

First run applies defaults and returns `DefaultsApplied`. Setters update memory and runtime immediately but do not write automatically:

```csharp
settings.SetMasterVolume(0.8f);
settings.SetLocale("zh_CN");
settings.Save(); // when the player applies or confirms
```

Preview slider movement immediately, then Save on release or confirmation to avoid frequent disk writes. `ResetToDefaults()` also requires explicit Save for persistence.

## 6. Build settings UI from platform capabilities

```csharp
resolutionPanel.Visible = settings.Supports(SettingsCapability.Resolution);
windowModePanel.Visible = settings.Supports(SettingsCapability.WindowMode);
vsyncPanel.Visible = settings.Supports(SettingsCapability.VSync);
```

Windows Desktop supports volume, locale, window mode, resolution, and VSync. Mobile guarantees volume and locale. Unknown platforms safely fall back to common capabilities.

An unsupported setting returns `SettingsApplyResult.Unsupported` without changing the snapshot. Do not show a control that cannot apply, and never treat Unsupported as success.

`Current` is immutable. Invalid volume, locale, and resolution throw argument exceptions and preserve current state.

## 7. Organize translation keys and dynamic text

Use stable semantic keys:

```csharp
string title = localization.Translate("UI.SETTINGS.TITLE");
string count = localization.TranslatePlural(
    "INVENTORY.ITEM_COUNT",
    "INVENTORY.ITEM_COUNT_PLURAL",
    itemCount);
```

Do not use an English sentence as the key. Prefer Godot automatic translation for ordinary Controls. Refresh runtime-composed, cached, and non-Control text after `LocaleChangedEvent`.

A missing translation returns the source key without throwing or logging on the hot query path. Content validation should find missing keys before release.

AvailableLocales is built during service initialization. Runtime language-pack addition is not supported. `SetLocale` accepts only the default or a canonical locale matching loaded translation content.

## 8. Fonts, RTL, and pseudolocalization

LocalizationService does not replace Theme fonts. Configure a fallback chain covering every target script and verify font imports and memory on real target platforms.

Manually test RTL languages for:

- Control layout and automatic text direction.
- Icon meaning and placement, margins, and scrollbars.
- Focus order and gamepad navigation.
- Custom drawing, numbers, paths, and mixed-direction text.

Godot project settings or `TranslationServer.PseudolocalizationEnabled` control pseudolocalization; it is not a player setting. Use it to expose text expansion, hard-coded copy, clipping, and layout assumptions. `IsPseudolocalizationEnabled` is diagnostic only.

## 9. Pre-release checklist

- Test empty, normal, backup-recovered, and doubly corrupt save slots in UI.
- Retain a fixture for every supported dataVersion.
- Show only platform-supported settings and verify mobile behavior on-device.
- Configure the default locale and complete all critical strings.
- Check every locale for missing keys, plurals, context, and dynamic refresh.
- Run pseudolocalization, then test at least one RTL locale.
- Verify translations and fonts are included in exported packages.

## Common failures

- Settings are stored in every progress slot: locale and volume change with slot selection; use the fixed SettingsService slot.
- Backup recovery immediately overwrites silently: notify the player and save later at a safe point.
- Migration is tested only with the current model: retain real old payload fixtures.
- Every slider change writes disk: Apply immediately, Save on confirmation.
- Mobile shows resolution controls: check `Supports` first.
- Some text remains after locale change: cached or dynamic text ignored LocaleChangedEvent.
- Chinese or Arabic displays boxes: the Theme font fallback is incomplete.
- RTL testing mirrors text only: focus, icons, and custom drawing still need manual validation.
- Compression or SHA-256 is treated as encryption: neither provides privacy or anti-cheat guarantees.

For exact members, see <xref:GoDo.ISaveService>, <xref:GoDo.ISaveCodec%601>, <xref:GoDo.SaveLoadResult%601>, <xref:GoDo.ISettingsService>, <xref:GoDo.SettingsCapability>, and <xref:GoDo.ILocalizationService>.
