# 切换语言与翻译游戏文本

本教程为主菜单、设置页和动态进度文本加入中英文切换。Godot 负责导入 PO 文件和自动翻译 Control，LocalizationService 负责查询已加载语言与动态文本，SettingsService 负责应用并保存玩家选择。

翻译键使用稳定的业务 ID，例如 `UI.MAIN_MENU.START`，不要直接把英文文案当作键。以后修改英文措辞时，业务代码和其他语言不会因此失去对应关系。

## 1. 创建 PO 翻译文件

创建目录：

```text
res://Localization/
├─ game.en.po
└─ game.zh_CN.po
```

`game.en.po` 示例：

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

`game.zh_CN.po` 使用相同键：

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

真实项目可以继续补充设置页标签、确认框和错误提示。每种 Locale 使用一个 PO 文件，便于翻译工具和协作平台维护。

## 2. 在 Godot 中注册翻译

打开 **项目 → 项目设置 → 本地化 → 翻译**，加入两个 PO 文件。Godot 会从文件名和 PO 元数据识别 Locale。

再到 **常规 → Internationalization → Locale**，把 Fallback 设置为 `en`。Fallback 是项目默认语言，应提供完整核心文本。

保存项目后重新运行。LocalizationService 在 GoDoRuntime 初始化时读取已加载语言列表；运行过程中动态增加翻译资源不会自动刷新该列表。

## 3. 让普通 Control 自动翻译

把场景中原来的可见文字替换为翻译键：

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

保持这些 Control 的 **Auto Translate** 启用。Locale 变化时，Godot 会自动更新它们，不需要逐个监听事件。

玩家名称、存档名等用户输入不应自动翻译；对应 Control 应关闭 Auto Translate，避免内容恰好与翻译键相同时被替换。

## 4. 在设置页列出可用语言

在 `SettingsView.tscn` 增加：

```text
Language (OptionButton)
```

在 `SettingsView.cs` 增加字段：

```csharp
[Export] private OptionButton? _language;

private ILocalizationService? _localization;
```

在 `_Ready()` 完成引用检查后，先填充选项，再订阅：

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

在 `_ExitTree()` 对称解绑：

```csharp
if (_language is not null)
    _language.ItemSelected -= OnLanguageSelected;
```

实现切换：

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

`SetLocale()` 会立即应用语言并更新内存快照，但不会自动写盘。上一教程的“保存并返回”会调用 `ISettingsService.Save()`，把语言和音量一起保存。

选项来自 `AvailableLocales`，因此正常不会传入不受支持的值。空值或未加载的 Locale 会抛出 `ArgumentException`，并保持当前语言不变。

## 5. 刷新动态进度文本

静态 Control 可以自动翻译，但“已完成 3 局”包含运行时数据，需要主动查询并格式化。

在 `MainMenu.tscn` 增加：

```text
ProgressLabel (Label)
```

在 `MainMenu.cs` 增加：

```csharp
[Export] private Label? _progressLabel;

private ILocalizationService? _localization;
private int _completedRuns;
```

在 `_Ready()` 中获取服务并绑定语言变更事件：

```csharp
_localization = Services.Get<ILocalizationService>();
EventChannel.Bind<LocaleChangedEvent>(this, OnLocaleChanged);
RefreshProgress();
```

`Bind` 的 Node 已经在场景树中，退出时会自动解绑，不需要手动调用 `Off()`。

增加公开更新入口和刷新逻辑：

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

在 `MainMenuProcedure.EnterAsync()` 打开界面后传入数据：

```csharp
_mainMenu = _ui.Open(MainMenuKey, UiLayer.Scene);

if (_mainMenu is MainMenu mainMenu)
    mainMenu.ShowProgress(_progress.CompletedRuns);
```

不要在 `_Process()` 中每帧重复翻译和格式化没有变化的文本。只在数据或 Locale 改变时刷新。

## 6. 运行并验证

确认以下行为：

1. 第一次启动使用项目 Fallback 语言。
2. 设置页列出 English 与中文。
3. 切换语言后，菜单、按钮和动态进度立即变化。
4. 保存设置并重启，仍使用上次选择的语言。
5. 临时删除某个翻译条目时显示源键，不崩溃也不在热路径刷错误日志。

还应在 Godot 中手动检查：

- 中英文字体 fallback 是否包含所需字符。
- 较长文本是否挤压按钮或被截断。
- 开启伪本地化后布局是否仍可用。
- 未来加入阿拉伯语等 RTL 语言时，图标方向、焦点顺序、边距和自定义绘制是否正确。

精确接口可查询 <xref:GoDo.ILocalizationService>、<xref:GoDo.LocalizationLocale>、<xref:GoDo.LocaleChangedEvent> 和 <xref:GoDo.ISettingsService>。
