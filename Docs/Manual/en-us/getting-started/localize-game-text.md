---
translation_of: Docs/Manual/zh-cn/getting-started/localize-game-text.md
translation_source_hash: sha256:21f0fc3b03b0bc059282abd953e5a1b03c61534e075c72ed3dc32187378a6159
---

# Switch Languages and Translate Game Text

This tutorial adds English and Chinese switching to the main menu, settings page, and dynamic progress text. Godot imports PO files and automatically translates Controls. LocalizationService queries loaded locales and dynamic text, while SettingsService applies and saves the player's choice.

Use stable game-owned IDs such as `UI.MAIN_MENU.START` as translation keys instead of English copy. Changing the English wording later will not disconnect game code from other locales.

## 1. Create PO translation files

Create this directory:

```text
res://Localization/
├─ game.en.po
└─ game.zh_CN.po
```

Example `game.en.po`:

```po
msgid ""
msgstr ""
"Language: en\n"
"Content-Type: text/plain; charset=UTF-8\n"
"Plural-Forms: nplurals=2; plural=(n != 1);\n"

msgid "UI.MAIN_MENU.TITLE"
msgstr "Main Menu"

msgid "UI.MAIN_MENU.START"
msgstr "Start Game"

msgid "UI.MAIN_MENU.SETTINGS"
msgstr "Settings"

msgid "UI.MAIN_MENU.QUIT"
msgstr "Quit"

msgid "UI.SETTINGS.TITLE"
msgstr "Settings"

msgid "UI.SETTINGS.SAVE"
msgstr "Save and Return"

msgid "UI.SETTINGS.BACK"
msgstr "Return without Saving"

msgid "UI.GAMEPLAY.RETURN"
msgstr "Return to Main Menu"

msgid "UI.GAMEPLAY.COMPLETE"
msgstr "Complete Run"

msgid "PROGRESS.COMPLETED.ONE"
msgid_plural "PROGRESS.COMPLETED.MANY"
msgstr[0] "Completed {count} run"
msgstr[1] "Completed {count} runs"
```

Use the same keys in `game.zh_CN.po`:

```po
msgid ""
msgstr ""
"Language: zh_CN\n"
"Content-Type: text/plain; charset=UTF-8\n"
"Plural-Forms: nplurals=1; plural=0;\n"

msgid "UI.MAIN_MENU.TITLE"
msgstr "主菜单"

msgid "UI.MAIN_MENU.START"
msgstr "开始游戏"

msgid "UI.MAIN_MENU.SETTINGS"
msgstr "设置"

msgid "UI.MAIN_MENU.QUIT"
msgstr "退出"

msgid "UI.SETTINGS.TITLE"
msgstr "设置"

msgid "UI.SETTINGS.SAVE"
msgstr "保存并返回"

msgid "UI.SETTINGS.BACK"
msgstr "返回但不保存"

msgid "UI.GAMEPLAY.RETURN"
msgstr "返回主菜单"

msgid "UI.GAMEPLAY.COMPLETE"
msgstr "完成本局"

msgid "PROGRESS.COMPLETED.ONE"
msgid_plural "PROGRESS.COMPLETED.MANY"
msgstr[0] "已完成 {count} 局"
```

A real project can add settings labels, confirmations, and error messages. Keep one PO file per locale so translation tools and collaboration platforms can maintain them.

## 2. Register translations in Godot

Open **Project → Project Settings → Localization → Translations** and add both PO files. Godot infers the locale from the filename and PO metadata.

Under **General → Internationalization → Locale**, set Fallback to `en`. The fallback is the project default and should provide complete core text.

Save and run the project again. LocalizationService reads the loaded locale list when GoDoRuntime initializes. Adding translation resources dynamically at runtime does not refresh that list.

## 3. Let ordinary Controls translate automatically

Replace visible text in existing scenes with translation keys:

```text
MainMenu/Title                 UI.MAIN_MENU.TITLE
MainMenu/StartButton           UI.MAIN_MENU.START
MainMenu/SettingsButton        UI.MAIN_MENU.SETTINGS
MainMenu/QuitButton            UI.MAIN_MENU.QUIT
SettingsView/Title             UI.SETTINGS.TITLE
SettingsView/SaveButton        UI.SETTINGS.SAVE
SettingsView/BackButton        UI.SETTINGS.BACK
GameplayHud/ReturnButton       UI.GAMEPLAY.RETURN
GameplayHud/CompleteButton     UI.GAMEPLAY.COMPLETE
```

Keep **Auto Translate** enabled on these Controls. Godot updates them when the locale changes, so they do not need individual event listeners.

Disable Auto Translate for player names, save names, and other user input. Otherwise, content that happens to equal a translation key could be replaced.

## 4. List available languages in Settings

Add this node to `SettingsView.tscn`:

```text
Language (OptionButton)
```

Add fields to `SettingsView.cs`:

```csharp
[Export] private OptionButton? _language;

private ILocalizationService? _localization;
```

After validating references in `_Ready()`, populate choices before subscribing:

```csharp
_settings = Services.Get<ISettingsService>();
_localization = Services.Get<ILocalizationService>();

for (int i = 0; i < _localization.AvailableLocales.Count; i++)
{
    LocalizationLocale locale = _localization.AvailableLocales[i];
    _language!.AddItem(locale.DisplayName);

    if (locale.Code == _settings.Current.Locale)
        _language.Select(i);
}

_language!.ItemSelected += OnLanguageSelected;
```

Unsubscribe in `_ExitTree()`:

```csharp
if (_language is not null)
    _language.ItemSelected -= OnLanguageSelected;
```

Apply the selection:

```csharp
private void OnLanguageSelected(long index)
{
    if (_localization is null ||
        index < 0 ||
        index >= _localization.AvailableLocales.Count)
    {
        return;
    }

    string locale = _localization.AvailableLocales[(int)index].Code;
    _settings!.SetLocale(locale);
}
```

`SetLocale()` applies the language immediately and updates the in-memory snapshot, but does not write automatically. The previous tutorial's “Save and Return” calls `ISettingsService.Save()` and stores language together with volume.

Choices come from `AvailableLocales`, so a normal selection cannot be unsupported. An empty or unloaded locale throws `ArgumentException` and leaves the current language unchanged.

## 5. Refresh dynamic progress text

Static Controls can translate automatically, but “Completed 3 runs” contains runtime data and must be queried and formatted explicitly.

Add this node to `MainMenu.tscn`:

```text
ProgressLabel (Label)
```

Add fields to `MainMenu.cs`:

```csharp
[Export] private Label? _progressLabel;

private ILocalizationService? _localization;
private int _completedRuns;
```

Obtain the service and bind the locale event in `_Ready()`:

```csharp
_localization = Services.Get<ILocalizationService>();
EventChannel.Bind<LocaleChangedEvent>(this, OnLocaleChanged);
RefreshProgress();
```

The Node is already inside the scene tree when `Bind` runs. EventChannel removes the listener automatically on tree exit, so there is no manual `Off()` call.

Add a public update entry point and refresh logic:

```csharp
public void ShowProgress(int completedRuns)
{
    _completedRuns = completedRuns;
    RefreshProgress();
}

private void OnLocaleChanged(LocaleChangedEvent _)
{
    RefreshProgress();
}

private void RefreshProgress()
{
    if (_progressLabel is null || _localization is null)
        return;

    string template = _localization.TranslatePlural(
        "PROGRESS.COMPLETED.ONE",
        "PROGRESS.COMPLETED.MANY",
        _completedRuns);

    _progressLabel.Text = template.Replace(
        "{count}",
        _completedRuns.ToString());
}
```

Pass data after `MainMenuProcedure.EnterAsync()` opens the interface:

```csharp
_mainMenu = _ui.Open(MainMenuKey, UiLayer.Scene);

if (_mainMenu is MainMenu mainMenu)
    mainMenu.ShowProgress(_progress.CompletedRuns);
```

Do not translate and format unchanged text every frame. Refresh only when its data or Locale changes.

## 6. Run and verify

Confirm that:

1. The first run uses the project's fallback locale.
2. Settings lists English and Chinese.
3. Menu text, buttons, and dynamic progress change immediately with the selection.
4. Saving settings and restarting preserves the selected language.
5. Temporarily removing an entry displays the source key without crashing or flooding logs on a hot query path.

Also check manually in Godot that:

- Font fallback contains every required English and Chinese character.
- Longer translations do not squeeze buttons or become clipped.
- Layout remains usable with pseudolocalization enabled.
- When adding an RTL locale such as Arabic, icon direction, focus order, margins, and custom drawing remain correct.

For exact members, see <xref:GoDo.ILocalizationService>, <xref:GoDo.LocalizationLocale>, <xref:GoDo.LocaleChangedEvent>, and <xref:GoDo.ISettingsService>.
