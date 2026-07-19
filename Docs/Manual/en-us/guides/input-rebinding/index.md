---
translation_of: Docs/Manual/zh-cn/guides/input-rebinding/index.md
translation_source_hash: sha256:892aeab2b31a6dc647d45c817959cd49e2b8fceb9c99b6fb772673a8a546649d
---

# Build a runtime input-rebinding screen

InputService lets a settings screen list the current bindings, capture new input, explain conflicts, and save the result. Gameplay code can keep using stable Action and Binding IDs. The current GUIDE backend supports rebinding, text prompts, and SaveService persistence.

## 1. Declare rebindable slots in the Profile

Create a `GuideInputBindingDefinition` for each GUIDE mapping that players may change:

```text
BindingId    = gameplay.jump.primary
ContextId    = gameplay
ActionId     = gameplay.jump
MappingIndex = 0
```

A Binding ID is part of the game's stable protocol. Do not derive it from display text, a key name, or an array position. Each GUIDE slot must map to exactly one Binding ID, and its target must exist and be marked rebindable in GUIDE.

```csharp
public static readonly InputBindingId JumpPrimary =
    InputBindingId.Create("gameplay.jump.primary");
```

## 2. Detect backend capabilities

When the settings screen opens:

```csharp
IInputService input = Services.Get<IInputService>();
if (!input.TryGetRebinding(out IInputRebinding? rebinding))
{
    rebindingPanel.Visible = false;
    return;
}
```

Rebinding, prompt queries, and persistence are optional capabilities. Always use the corresponding `TryGet...` method instead of guessing from a platform or plugin name.

List the public slots in a context:

```csharp
IReadOnlyList<InputBindingInfo> bindings =
    rebinding.GetBindings(GameInput.Gameplay);
```

`InputBindingInfo` contains the stable Binding and Action IDs, device category, and current and default display text. The settings UI can render the list in Profile order.

## 3. Capture player input

```csharp
private async Task CaptureAsync(InputBindingId binding)
{
    _captureOverlay.Visible = true;
    try
    {
        InputBindingCandidate? candidate =
            await _rebinding.CaptureAsync(binding);

        if (candidate == null)
            return;

        await ResolveAndApplyAsync(binding, candidate);
    }
    catch (InputOperationException exception)
    {
        ErrorHub.Report(exception, "Game.Input", binding.Value);
        ShowCaptureFailed();
    }
    finally
    {
        _captureOverlay.Visible = false;
    }
}
```

GUIDE ignores the first 0.2 seconds after capture starts, applies an axis magnitude threshold of 0.5, and treats Esc as cancel. Call `CancelCapture()` when the settings screen closes; the task then returns `null`.

Only one capture may run on a backend at a time. Disable other rebinding buttons while it is active, or cancel the previous capture first. A concurrent request throws `InputOperationException`.

## 4. Let the player resolve conflicts

```csharp
IReadOnlyList<InputBindingInfo> conflicts =
    _rebinding.FindConflicts(binding, candidate);

if (conflicts.Count == 0)
{
    _rebinding.Apply(binding, candidate);
    return;
}

ShowConflictDialog(binding, candidate, conflicts);
```

`FindConflicts` only reports facts. `Apply` does not clear or overwrite another slot automatically. Offer at least Cancel and Capture again. Add swapping or clearing only when the game's input rules define those operations.

A candidate is an opaque, session-local value. Give it back only to the backend instance that created it; do not persist it or inspect GUIDE's internal object.

## 5. Apply or restore a binding

```csharp
_rebinding.Apply(binding, candidate);
RefreshBindingRows();
```

Restore one slot to its Profile default:

```csharp
_rebinding.RestoreDefault(GameBindings.JumpPrimary);
```

Apply and restore rebuild a GUIDE context. They are low-frequency settings operations, not calls for `_Process()`, slider updates, or tight loops.

Success publishes `InputBindingsChangedEvent`. Failure rolls the old binding back and publishes no event. Prompt UI can listen to this event and `InputDeviceChangedEvent` and refresh on demand instead of polling every frame.

## 6. Load and save bindings

After the Installer has a valid `PersistenceSlot`:

```csharp
if (input.TryGetRebindingPersistence(
        out IInputRebindingPersistence? persistence))
{
    InputBindingLoadStatus status = persistence.LoadAndApply();
}
```

Run this after the Installer finishes and before the first Procedure starts. A missing save uses Profile defaults. If the primary file is corrupt, SaveService attempts recovery from its backup.

Save when the player selects Apply or Confirm:

```csharp
try
{
    _persistence?.Save();
}
catch (SaveException exception)
{
    ErrorHub.Report(exception, "Game.Input", "Save bindings");
    ShowBindingsNotSaved();
}
```

`Apply` changes only the current runtime state; it does not write to disk. A save failure does not undo the binding already applied in this session, so tell the player that it is active now but may revert after restart.

Use a different `PersistenceSlot` for each local player profile. The default `godo-input-bindings` slot suits a single local player.

## 7. Show prompts for the active device

```csharp
if (input.TryGetPromptQuery(out IInputPromptQuery? prompts))
{
    IReadOnlyList<InputPromptInfo> infos = prompts.GetPrompts(
        GameInput.Gameplay,
        GameInput.Jump,
        input.ActiveDevice);
}
```

The GUIDE backend returns fallback text in stable order. The game UI owns glyphs, controller branding, localization, and layout. An unbound slot may return empty text; display a localized state such as “Unbound.”

## Common mistakes

- No rebinding entry appears: the backend does not expose Rebinding, or the Profile defines no Bindings.
- Mouse input is captured immediately: do not bypass GUIDE's startup delay.
- A second capture fails: another capture is still active; disable the button or cancel it first.
- `Apply` rejects a candidate: it came from an old or different backend instance; capture it again.
- Two actions still share a key: `Apply` does not resolve conflicts for the UI.
- A binding works but disappears after restart: `Save()` was not called, or its failure was hidden.
- Prompt text stays stale: listen for `InputBindingsChangedEvent` and `InputDeviceChangedEvent`.
- Loading reports corrupt data: handle `RecoveredFromBackup` or `SaveException` without deleting a healthy backup.

For exact signatures, see <xref:GoDo.IInputRebinding>, <xref:GoDo.IInputRebindingPersistence>, <xref:GoDo.IInputPromptQuery>, <xref:GoDo.InputBindingInfo>, and <xref:GoDo.InputBindingCandidate>.
