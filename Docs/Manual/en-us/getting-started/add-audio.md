---
translation_of: Docs/Manual/zh-cn/getting-started/add-audio.md
translation_source_hash: sha256:04a5ca97c3275b846b00867bc58622ce6456b96232efeeabcef483dcfbc220bc
---

# Add Background Music and Button Sounds

This tutorial adds two BGM tracks and one button sound to the previous menu and gameplay flows. Procedures decide which long-running music belongs to the current phase. UI only plays short sounds directly associated with clicks.

AudioService manages non-spatial BGM, SFX, and group volume. Sounds that follow a 2D or 3D position should still use Godot's `AudioStreamPlayer2D` or `AudioStreamPlayer3D`.

## Prepare audio resources

Prepare audio files that you have permission to use in the project:

```text
res://Audio/
├─ MenuTheme.ogg
├─ GameplayTheme.ogg
└─ ButtonClick.wav
```

Enable looping for both BGM tracks in Godot's Import panel and reimport them. The button sound normally does not loop.

The filenames and formats are not framework requirements. Each `ResourceKey` in code must exactly match the real path and casing.

## 1. Create a game-owned audio entry point

Create `res://Shared/GameAudio.cs`:

```csharp
using System;
using System.Threading.Tasks;
using GoDo;

namespace MyGame;

public static class GameAudio
{
    public static readonly ResourceKey MenuTheme =
        ResourceKey.FromPath("res://Audio/MenuTheme.ogg");
    public static readonly ResourceKey GameplayTheme =
        ResourceKey.FromPath("res://Audio/GameplayTheme.ogg");

    private static readonly ResourceKey ButtonClick =
        ResourceKey.FromPath("res://Audio/ButtonClick.wav");

    public static async Task PlayBgmAsync(
        IAudioService audio,
        ResourceKey key)
    {
        try
        {
            await audio.PlayBgmAsync(key);
        }
        catch (OperationCanceledException)
        {
            // Stopping or replacing a load is not a damaged-resource error.
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "GameAudio", key.Value);
        }
    }

    public static async Task PlayButtonClickAsync(IAudioService audio)
    {
        try
        {
            // false is the normal capacity-full result.
            await audio.PlaySfxAsync(ButtonClick);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "GameAudio", ButtonClick.Value);
        }
    }
}
```

This class only centralizes game-owned resource keys and the policy for non-critical failures. It does not create another player or cache. AudioService still owns loading, the BGM player, and the SFX pool.

`PlaySfxAsync()` returns `false` when the default 32 simultaneous voices are already occupied. This is not a resource-loading failure, so a button click can simply be skipped.

## 2. Play BGM from the menu flow

Add this field to `MainMenuProcedure`:

```csharp
private IAudioService? _audio;
```

In `EnterAsync()`, after scene and UI initialization but before event subscriptions, add:

```csharp
_audio = context.GetService<IAudioService>();
await GameAudio.PlayBgmAsync(_audio, GameAudio.MenuTheme);
```

Clear the reference at the end of `ExitAsync()`:

```csharp
_audio = null;
```

Do not call `StopBgm()` when leaving the menu flow. The next GameplayProcedure requests another track, and AudioService only replaces the current BGM after the new resource has loaded. This avoids an unnecessary silent gap during loading.

Requesting the same resource does not restart it by default. Use `PlayBgmAsync(key, restart: true)` only when a deliberate restart is required.

## 3. Change BGM in the gameplay flow

Add the same field to `GameplayProcedure`:

```csharp
private IAudioService? _audio;
```

After the gameplay scene and HUD have initialized in `EnterAsync()`, add:

```csharp
_audio = context.GetService<IAudioService>();
await GameAudio.PlayBgmAsync(_audio, GameAudio.GameplayTheme);
```

Clear the reference in `ExitAsync()`:

```csharp
_audio = null;
```

The menu-to-game transition now changes to Gameplay BGM, and returning changes back to Menu BGM. Replacing the main scene does not free AudioService because the only GoDoRuntime owns it for the application lifetime.

If a flow requires silence, call `StopBgm()` explicitly when entering that flow instead of expecting a scene change to stop music.

## 4. Play sounds from main-menu buttons

Add this import to `MainMenu.cs`:

```csharp
using MyGame;
```

Add a field and obtain the service in `_Ready()`:

```csharp
private IAudioService? _audio;

// In _Ready(), after the button checks pass:
_audio = Services.Get<IAudioService>();
```

Add a helper method:

```csharp
private void PlayButtonClick()
{
    _ = GameAudio.PlayButtonClickAsync(_audio!);
}
```

Call it at the start of each button handler:

```csharp
private void OnStartPressed()
{
    PlayButtonClick();
    EventChannel.Emit<StartGameRequestedEvent>();
}

private void OnSettingsPressed()
{
    PlayButtonClick();
    Open(SettingsKey, UiLayer.View);
}

private void OnQuitPressed()
{
    PlayButtonClick();
    Open(ConfirmQuitKey, UiLayer.Modal);
}
```

The short sound is intentionally not awaited by the button, so the first resource load does not delay navigation or a flow change. `GameAudio` handles exceptions inside the asynchronous boundary, so playback failures are not lost.

Do not start BGM this way. Procedures await long-running music requests so their ordering remains clear.

## 5. Play a sound from the return button

Add `using MyGame;` to `GameplayHud.cs`, then add the service field:

```csharp
private IAudioService? _audio;
```

Obtain the service in `_Ready()` after the button check succeeds:

```csharp
_audio = Services.Get<IAudioService>();
```

Update the return handler:

```csharp
private void OnReturnPressed()
{
    _ = GameAudio.PlayButtonClickAsync(_audio!);
    EventChannel.Emit<ReturnToMenuRequestedEvent>();
}
```

AudioService remains alive when the HUD closes during the flow change, so a submitted short sound can finish playing.

## 6. Set initial volume

Set group volume once in `Boot.cs` before entering the first Procedure:

```csharp
IAudioService audio = Services.Get<IAudioService>();
audio.SetVolume(AudioGroup.Bgm, 0.7f);
audio.SetVolume(AudioGroup.Sfx, 0.9f);

IProcedureService procedures = Services.Get<IProcedureService>();
await procedures.ChangeAsync<MainMenuProcedure>();
```

Volume is a finite linear value from 0 to 1. Out-of-range values throw `ArgumentOutOfRangeException`. These are startup defaults and do not yet save player choices; a later settings and saves guide will add persistence.

If the project has no persistent `BGM` or `SFX` Audio Bus, the framework creates it at runtime and reports a Warning. It does not modify the project's saved Audio Bus Layout.

## 7. Run and verify

Confirm that:

1. Menu BGM plays after startup.
2. Every main-menu button plays a short sound.
3. “Start Game” changes to Gameplay BGM.
4. “Return to Main Menu” plays a sound and restores Menu BGM.
5. Repeated trips do not layer several BGM tracks.
6. Temporarily breaking an audio path still allows the flow to enter and produces an ErrorHub report in Godot's Output panel.

Restore the correct path after testing. Do not commit sample music without appropriate copyright permission.

For exact members, see <xref:GoDo.IAudioService>, <xref:GoDo.AudioGroup>, and <xref:GoDo.AudioPlaybackException>.
