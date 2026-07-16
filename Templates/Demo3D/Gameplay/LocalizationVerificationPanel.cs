using System;
using System.Globalization;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>Demo3D 中用于人工验收本地化、伪本地化与 RTL 的最小面板。</summary>
public sealed partial class LocalizationVerificationPanel : PanelContainer
{
    private const string DynamicTextKey = "DEMO3D.LOCALIZATION.DYNAMIC_TEXT";
    private const string SingularCountKey = "DEMO3D.LOCALIZATION.CORE_COUNT";
    private const string PluralCountKey = "DEMO3D.LOCALIZATION.CORE_COUNT_PLURAL";

    [Export] public NodePath DynamicLabelPath { get; set; } = null!;
    [Export] public NodePath PluralLabelPath { get; set; } = null!;
    [Export] public NodePath LocaleStatusLabelPath { get; set; } = null!;
    [Export] public NodePath OperationStatusLabelPath { get; set; } = null!;
    [Export] public NodePath EnglishButtonPath { get; set; } = null!;
    [Export] public NodePath FrenchButtonPath { get; set; } = null!;
    [Export] public NodePath ArabicButtonPath { get; set; } = null!;
    [Export] public NodePath DecreaseButtonPath { get; set; } = null!;
    [Export] public NodePath IncreaseButtonPath { get; set; } = null!;
    [Export] public NodePath SaveButtonPath { get; set; } = null!;
    [Export] public NodePath PseudolocalizationButtonPath { get; set; } = null!;

    private Label? _dynamicLabel;
    private Label? _pluralLabel;
    private Label? _localeStatusLabel;
    private Label? _operationStatusLabel;
    private Button? _englishButton;
    private Button? _frenchButton;
    private Button? _arabicButton;
    private Button? _decreaseButton;
    private Button? _increaseButton;
    private Button? _saveButton;
    private Button? _pseudolocalizationButton;
    private ILocalizationService? _localization;
    private ISettingsService? _settings;
    private int _count = 2;

    /// <inheritdoc />
    public override void _Ready()
    {
        _dynamicLabel = RequireNode<Label>(DynamicLabelPath, "动态翻译标签");
        _pluralLabel = RequireNode<Label>(PluralLabelPath, "复数标签");
        _localeStatusLabel = RequireNode<Label>(LocaleStatusLabelPath, "Locale 状态标签");
        _operationStatusLabel = RequireNode<Label>(OperationStatusLabelPath, "操作状态标签");
        _englishButton = RequireNode<Button>(EnglishButtonPath, "英语按钮");
        _frenchButton = RequireNode<Button>(FrenchButtonPath, "法语按钮");
        _arabicButton = RequireNode<Button>(ArabicButtonPath, "阿拉伯语按钮");
        _decreaseButton = RequireNode<Button>(DecreaseButtonPath, "数量减少按钮");
        _increaseButton = RequireNode<Button>(IncreaseButtonPath, "数量增加按钮");
        _saveButton = RequireNode<Button>(SaveButtonPath, "保存语言按钮");
        _pseudolocalizationButton = RequireNode<Button>(PseudolocalizationButtonPath, "伪本地化按钮");

        _localization = Services.Get<ILocalizationService>();
        _settings = Services.Get<ISettingsService>();

        EventChannel.Bind<LocaleChangedEvent>(this, OnLocaleChanged);
        _englishButton.Pressed += OnEnglishPressed;
        _frenchButton.Pressed += OnFrenchPressed;
        _arabicButton.Pressed += OnArabicPressed;
        _decreaseButton.Pressed += OnDecreasePressed;
        _increaseButton.Pressed += OnIncreasePressed;
        _saveButton.Pressed += OnSavePressed;
        _pseudolocalizationButton.Pressed += OnPseudolocalizationPressed;
        RefreshText();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (IsInstanceValid(_englishButton))
            _englishButton!.Pressed -= OnEnglishPressed;
        if (IsInstanceValid(_frenchButton))
            _frenchButton!.Pressed -= OnFrenchPressed;
        if (IsInstanceValid(_arabicButton))
            _arabicButton!.Pressed -= OnArabicPressed;
        if (IsInstanceValid(_decreaseButton))
            _decreaseButton!.Pressed -= OnDecreasePressed;
        if (IsInstanceValid(_increaseButton))
            _increaseButton!.Pressed -= OnIncreasePressed;
        if (IsInstanceValid(_saveButton))
            _saveButton!.Pressed -= OnSavePressed;
        if (IsInstanceValid(_pseudolocalizationButton))
            _pseudolocalizationButton!.Pressed -= OnPseudolocalizationPressed;

        _localization = null;
        _settings = null;
    }

    private void OnLocaleChanged(LocaleChangedEvent evt)
    {
        _operationStatusLabel!.Text = $"Locale changed: {evt.PreviousLocale} -> {evt.CurrentLocale}";
        RefreshText();
    }

    private void OnEnglishPressed() => ApplyLocale("en");

    private void OnFrenchPressed() => ApplyLocale("fr");

    private void OnArabicPressed() => ApplyLocale("ar");

    private void OnDecreasePressed()
    {
        if (_count > 0)
            _count--;
        RefreshText();
    }

    private void OnIncreasePressed()
    {
        _count++;
        RefreshText();
    }

    private void OnSavePressed()
    {
        try
        {
            _settings!.Save();
            _operationStatusLabel!.Text = $"Saved locale: {_settings.Current.Locale}";
        }
        catch (Exception exception)
        {
            _operationStatusLabel!.Text = $"Save failed: {exception.Message}";
        }
    }

    private void OnPseudolocalizationPressed()
    {
        TranslationServer.PseudolocalizationEnabled = !TranslationServer.PseudolocalizationEnabled;
        _operationStatusLabel!.Text = "Pseudolocalization toggled for this process.";
        RefreshText();
    }

    private void ApplyLocale(string locale)
    {
        try
        {
            _settings!.SetLocale(locale);
        }
        catch (Exception exception)
        {
            _operationStatusLabel!.Text = $"Locale failed: {exception.Message}";
        }
    }

    private void RefreshText()
    {
        if (_localization == null)
            return;

        _dynamicLabel!.Text = _localization.Translate(DynamicTextKey);
        _pluralLabel!.Text = _localization
            .TranslatePlural(SingularCountKey, PluralCountKey, _count)
            .Replace("%d", _count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        _localeStatusLabel!.Text =
            $"Locale: {_localization.CurrentLocale} | RTL: {IsLayoutRtl()} | Pseudo: {_localization.IsPseudolocalizationEnabled}";
        _pseudolocalizationButton!.Text = _localization.IsPseudolocalizationEnabled
            ? "Disable pseudolocalization"
            : "Enable pseudolocalization";
    }

    private T RequireNode<T>(NodePath path, string description)
        where T : Node
    {
        T? node = GetNodeOrNull<T>(path);
        if (!IsInstanceValid(node))
            throw new InvalidOperationException($"LocalizationVerificationPanel 缺少{description}引用。");

        return node!;
    }
}
