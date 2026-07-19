---
translation_of: Docs/Manual/zh-cn/getting-started/save-progress-and-settings.md
translation_source_hash: sha256:e72765018da6a54fd332d20c47b64a95010b384a422baec7c46320cddfcf3c47
---

# Save Game Progress and Volume Settings

This tutorial adds two kinds of persistent data to the existing flow: the number of completed runs and the player's BGM and SFX volume choices. They use separate boundaries. Game progress belongs to a game save, while volume is a player preference shared across save slots.

SaveService manages slots, integrity checks, temporary files, backups, and recovery. The game owns its data model, JSON encoding, and version migration. SettingsService already handles common preferences such as volume, so do not duplicate volume fields in a game save.

## 1. Define game progress and its Codec

Create `res://Shared/GameProgress.cs`:

```csharp
namespace MyGame;

public sealed class GameProgress
{
    public int CompletedRuns { get; set; }
}
```

Create `res://Shared/GameProgressCodec.cs`:

```csharp
using System;
using System.IO;
using System.Text.Json;
using GoDo;

namespace MyGame;

public sealed class GameProgressCodec : ISaveCodec<GameProgress>
{
    public const int CurrentVersion = 1;

    public byte[] Encode(GameProgress value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }

    public GameProgress Decode(ReadOnlySpan<byte> payload, int dataVersion)
    {
        if (dataVersion != CurrentVersion)
        {
            throw new InvalidDataException(
                $"Unsupported game-save version: {dataVersion}");
        }

        return JsonSerializer.Deserialize<GameProgress>(payload)
            ?? throw new InvalidDataException("The game-save payload is empty.");
    }
}
```

`CurrentVersion` is the game payload version, not the framework container version. When fields change, migrate older data in `Decode()` according to `dataVersion`, then increase the current version.

`System.Text.Json` is included with .NET and does not require another NuGet package. Do not serialize Godot Resources, Nodes, or large binary assets directly into an ordinary save.

## 2. Wrap the game-owned slot

Create `res://Shared/GameProgressRepository.cs`:

```csharp
using GoDo;

namespace MyGame;

public sealed class GameProgressRepository
{
    private static readonly SaveSlot Slot = SaveSlot.Create("slot_1");
    private static readonly GameProgressCodec Codec = new();

    private readonly ISaveService _saves;

    public GameProgressRepository(ISaveService saves)
    {
        _saves = saves;
    }

    public GameProgress LoadOrCreate()
    {
        SaveLoadResult<GameProgress> result = _saves.Load(Slot, Codec);
        if (!result.HasValue)
            return new GameProgress();

        if (result.Status == SaveLoadStatus.RecoveredFromBackup)
        {
            ErrorHub.Warn(
                "The primary save was unavailable; a backup was loaded.",
                "GameProgress",
                Slot.Value);
        }

        return result.Value;
    }

    public void Save(GameProgress progress)
    {
        _saves.Save(
            Slot,
            progress,
            GameProgressCodec.CurrentVersion,
            Codec);
    }
}
```

A slot may contain ASCII letters, digits, underscores, and hyphens, up to 64 characters. `NotFound` is the normal first-run result and does not throw. A damaged primary with a healthy backup returns `RecoveredFromBackup`.

Read, integrity, Codec, and write failures throw `SaveException`. The Repository does not silently swallow them. The startup or player-action boundary displays and reports the failure.

## 3. Load settings and progress at startup

Load settings in `Boot.cs` before entering the first Procedure:

```csharp
ISettingsService settings = Services.Get<ISettingsService>();
SettingsLoadStatus settingsStatus = settings.LoadAndApply();

if (settingsStatus == SettingsLoadStatus.DefaultsApplied)
{
    settings.SetBgmVolume(0.7f);
    settings.SetSfxVolume(0.9f);
}
else if (settingsStatus == SettingsLoadStatus.RecoveredFromBackup)
{
    ErrorHub.Warn("Player settings were recovered from backup.", "GameBoot");
}

var progressRepository = new GameProgressRepository(
    Services.Get<ISaveService>());
GameProgress progress = progressRepository.LoadOrCreate();

IProcedureService procedures = Services.Get<IProcedureService>();
await procedures.ChangeAsync(
    new MainMenuProcedure(progressRepository, progress));
```

This replaces the direct `IAudioService.SetVolume()` startup defaults from the previous tutorial. `LoadAndApply()` immediately applies saved volume to AudioService. The tutorial defaults are only used on the first run.

The existing `try/catch` in `Boot` reports a load failure and stops before entering the main flow, avoiding a silent overwrite when the save state is unknown.

## 4. Carry session progress through Procedures

`MainMenuProcedure` and `GameplayProcedure` now require constructor data. Add this to `MainMenuProcedure`:

```csharp
private readonly GameProgressRepository _progressRepository;
private readonly GameProgress _progress;

public MainMenuProcedure(
    GameProgressRepository progressRepository,
    GameProgress progress)
{
    _progressRepository = progressRepository;
    _progress = progress;
}
```

After entering the menu, print the current progress so restart loading is easy to verify:

```csharp
GD.Print($"Completed runs: {_progress.CompletedRuns}");
```

Change the start request to the instance overload:

```csharp
private void OnStartGameRequested(StartGameRequestedEvent _)
{
    _context!.RequestChange(
        new GameplayProcedure(_progressRepository, _progress));
}
```

Add the matching fields and constructor to `GameplayProcedure`:

```csharp
private readonly GameProgressRepository _progressRepository;
private readonly GameProgress _progress;

public GameplayProcedure(
    GameProgressRepository progressRepository,
    GameProgress progress)
{
    _progressRepository = progressRepository;
    _progress = progress;
}
```

Carry the same objects during an ordinary return:

```csharp
_context!.RequestChange(
    new MainMenuProcedure(_progressRepository, _progress));
```

Use `RequestChange(IProcedure next)` when a transition carries session data. Do not register temporary progress objects as global Services.

## 5. Save when a run is completed

Add this event to `GameEvents.cs`:

```csharp
public readonly struct CompleteRunRequestedEvent : IGameEvent
{
}
```

Add a `CompleteButton` with the text “Complete Run” to `GameplayHud.tscn`. Export, subscribe, and unsubscribe it like the existing return button. Its handler only publishes intent:

```csharp
private void OnCompletePressed()
{
    _ = GameAudio.PlayButtonClickAsync(_audio!);
    EventChannel.Emit<CompleteRunRequestedEvent>();
}
```

Add the listener when `GameplayProcedure.EnterAsync()` creates its EventScope:

```csharp
_events = new EventScope()
    .On<ReturnToMenuRequestedEvent>(OnReturnToMenuRequested)
    .On<CompleteRunRequestedEvent>(OnCompleteRunRequested);
```

Implement the save boundary:

```csharp
private void OnCompleteRunRequested(CompleteRunRequestedEvent _)
{
    _progress.CompletedRuns++;

    try
    {
        _progressRepository.Save(_progress);
        _context!.RequestChange(
            new MainMenuProcedure(_progressRepository, _progress));
    }
    catch (SaveException exception)
    {
        ErrorHub.Report(exception, "Gameplay", "Save completed progress");
    }
}
```

SaveService currently uses a synchronous main-thread API intended for ordinary small saves. Save at explicit milestones, not in `_Process()`, and do not wrap Godot file operations in `Task.Run`.

On failure, the current flow remains active so game UI can offer a retry. A production game should show a player-readable error instead of relying only on development output.

## 6. Edit and save volume from Settings

Expand `SettingsView.tscn`:

```text
SettingsView (Control)
└─ Content (VBoxContainer)
   ├─ Title (Label, text “Settings”)
   ├─ BgmVolume (HSlider, Min 0, Max 1, Step 0.05)
   ├─ SfxVolume (HSlider, Min 0, Max 1, Step 0.05)
   ├─ SaveButton (Button, text “Save and Return”)
   └─ BackButton (Button, text “Return without Saving”)
```

Add exported fields to `SettingsView.cs`:

```csharp
[Export] private HSlider? _bgmVolume;
[Export] private HSlider? _sfxVolume;
[Export] private Button? _saveButton;
```

Add the service field:

```csharp
private ISettingsService? _settings;
```

After checking every reference in `_Ready()`, set slider values before subscribing:

```csharp
_settings = Services.Get<ISettingsService>();
_ui = Services.Get<IUiService>();

_bgmVolume!.Value = _settings.Current.BgmVolume;
_sfxVolume!.Value = _settings.Current.SfxVolume;

_bgmVolume.ValueChanged += OnBgmVolumeChanged;
_sfxVolume.ValueChanged += OnSfxVolumeChanged;
_saveButton!.Pressed += OnSavePressed;
_backButton!.Pressed += OnBackPressed;
```

Unsubscribe all four signals in `_ExitTree()`, then implement:

```csharp
private void OnBgmVolumeChanged(double value)
{
    _settings!.SetBgmVolume((float)value);
}

private void OnSfxVolumeChanged(double value)
{
    _settings!.SetSfxVolume((float)value);
}

private void OnSavePressed()
{
    try
    {
        _settings!.Save();
        _ui!.TryGoBack();
    }
    catch (SaveException exception)
    {
        ErrorHub.Report(exception, "SettingsView", "Save player settings");
    }
}

private void OnBackPressed()
{
    _ui!.TryGoBack();
}
```

Dragging a slider applies volume immediately but does not write every change to disk. Only “Save and Return” persists the current snapshot. “Return without Saving” skips the write, but the volume already applied remains active for the current run.

## 7. Run and verify

Check these steps:

1. The first run uses 0.7 BGM and 0.9 SFX defaults.
2. Change volume and select “Save and Return.” The slider values and actual volume survive a restart.
3. Complete a run. The menu output shows an increased completed count.
4. Exit completely and restart. The completed count is still present.
5. An ordinary “Return to Main Menu” does not increment or save the completion count.

Authoritative files live under `user://saves/`; do not depend on a platform-specific absolute path in game code. Use `ISaveService.Delete()` when deleting a test save instead of removing only the primary file and leaving its backup and temporary files behind.

For exact members, see <xref:GoDo.ISaveService>, <xref:GoDo.ISaveCodec%601>, <xref:GoDo.SaveLoadResult%601>, <xref:GoDo.ISettingsService>, and <xref:GoDo.SettingsSnapshot>.
