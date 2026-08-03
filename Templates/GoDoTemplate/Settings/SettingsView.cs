using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 管理模板的音量和语言设置界面。
/// <para>滑块和语言选择会立即应用到运行时；只有 Save 按钮会把当前快照写入 Settings 固定槽位。</para>
/// </summary>
public sealed partial class SettingsView : Control
{
    [Export] public NodePath MasterSliderPath { get; set; } = null!;
    [Export] public NodePath BgmSliderPath { get; set; } = null!;
    [Export] public NodePath SfxSliderPath { get; set; } = null!;
    [Export] public NodePath EnglishButtonPath { get; set; } = null!;
    [Export] public NodePath ChineseButtonPath { get; set; } = null!;
    [Export] public NodePath SaveButtonPath { get; set; } = null!;
    [Export] public NodePath BackButtonPath { get; set; } = null!;
    [Export] public NodePath StatusLabelPath { get; set; } = null!;

    private HSlider? _masterSlider;
    private HSlider? _bgmSlider;
    private HSlider? _sfxSlider;
    private Button? _englishButton;
    private Button? _chineseButton;
    private Button? _saveButton;
    private Button? _backButton;
    private Label? _statusLabel;
    private ISettingsService? _settings;
    private ILocalizationService? _localization;
    private bool _isRefreshing;

    public override void _Ready()
    {
        _masterSlider = RequireNode<HSlider>(MasterSliderPath, "Master 音量滑块");
        _bgmSlider = RequireNode<HSlider>(BgmSliderPath, "BGM 音量滑块");
        _sfxSlider = RequireNode<HSlider>(SfxSliderPath, "SFX 音量滑块");
        _englishButton = RequireNode<Button>(EnglishButtonPath, "英语按钮");
        _chineseButton = RequireNode<Button>(ChineseButtonPath, "中文按钮");
        _saveButton = RequireNode<Button>(SaveButtonPath, "保存按钮");
        _backButton = RequireNode<Button>(BackButtonPath, "返回按钮");
        _statusLabel = RequireNode<Label>(StatusLabelPath, "状态标签");
        _settings = Services.Get<ISettingsService>();
        _localization = Services.Get<ILocalizationService>();

        _masterSlider.ValueChanged += OnMasterVolumeChanged;
        _bgmSlider.ValueChanged += OnBgmVolumeChanged;
        _sfxSlider.ValueChanged += OnSfxVolumeChanged;
        _englishButton.Pressed += OnEnglishPressed;
        _chineseButton.Pressed += OnChinesePressed;
        _saveButton.Pressed += OnSavePressed;
        _backButton.Pressed += OnBackPressed;
        EventChannel.Bind<LocaleChangedEvent>(this, OnLocaleChanged);
        Refresh();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_masterSlider))
            _masterSlider!.ValueChanged -= OnMasterVolumeChanged;
        if (GodotObject.IsInstanceValid(_bgmSlider))
            _bgmSlider!.ValueChanged -= OnBgmVolumeChanged;
        if (GodotObject.IsInstanceValid(_sfxSlider))
            _sfxSlider!.ValueChanged -= OnSfxVolumeChanged;
        if (GodotObject.IsInstanceValid(_englishButton))
            _englishButton!.Pressed -= OnEnglishPressed;
        if (GodotObject.IsInstanceValid(_chineseButton))
            _chineseButton!.Pressed -= OnChinesePressed;
        if (GodotObject.IsInstanceValid(_saveButton))
            _saveButton!.Pressed -= OnSavePressed;
        if (GodotObject.IsInstanceValid(_backButton))
            _backButton!.Pressed -= OnBackPressed;

        _settings = null;
        _localization = null;
    }

    /// <summary>从当前 Settings 快照刷新所有设置控件。</summary>
    public void Refresh()
    {
        if (_settings == null || _localization == null)
            return;

        _isRefreshing = true;
        try
        {
            SettingsSnapshot current = _settings.Current;
            _masterSlider!.Value = current.MasterVolume;
            _bgmSlider!.Value = current.BgmVolume;
            _sfxSlider!.Value = current.SfxVolume;
            _englishButton!.Disabled = !_localization.IsLocaleSupported("en");
            _chineseButton!.Disabled = !_localization.IsLocaleSupported("zh_CN");
            _statusLabel!.Text = _localization.Translate("TEMPLATE.SETTINGS.CURRENT_LOCALE") +
                $": {_localization.CurrentLocale}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void OnMasterVolumeChanged(double value)
    {
        if (!_isRefreshing)
            _settings!.SetMasterVolume((float)value);
    }

    private void OnBgmVolumeChanged(double value)
    {
        if (!_isRefreshing)
            _settings!.SetBgmVolume((float)value);
    }

    private void OnSfxVolumeChanged(double value)
    {
        if (!_isRefreshing)
            _settings!.SetSfxVolume((float)value);
    }

    private void OnEnglishPressed() => SetLocale("en");

    private void OnChinesePressed() => SetLocale("zh_CN");

    private void OnSavePressed()
    {
        try
        {
            _settings!.Save();
            _statusLabel!.Text = _localization!.Translate("TEMPLATE.SETTINGS.SAVED");
        }
        catch (Exception exception)
        {
            StarterLog.Settings.Error(exception, "Save");
            _statusLabel!.Text = _localization!.Translate("TEMPLATE.SETTINGS.SAVE_FAILED");
        }
    }

    private void OnBackPressed() => EventChannel.Emit<SettingsCloseSelectedEvent>();

    private void OnLocaleChanged(LocaleChangedEvent _)
    {
        Refresh();
    }

    private void SetLocale(string locale)
    {
        try
        {
            _settings!.SetLocale(locale);
            Refresh();
        }
        catch (Exception exception)
        {
            StarterLog.Settings.Error(exception, "SetLocale");
            _statusLabel!.Text = _localization!.Translate("TEMPLATE.SETTINGS.LOCALE_FAILED");
        }
    }

    private T RequireNode<T>(NodePath path, string description)
        where T : Node
    {
        T? node = GetNodeOrNull<T>(path);
        if (!GodotObject.IsInstanceValid(node))
            throw new InvalidOperationException($"SettingsView 缺少{description}引用。");

        return node!;
    }
}
