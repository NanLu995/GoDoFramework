using System;
using System.Collections.Generic;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>复用 Godot TranslationServer 的项目本地化查询与语言切换服务。</summary>
public sealed class LocalizationService : ILocalizationService
{
    private const string DefaultLocaleSetting = "internationalization/locale/fallback";
    private const string FallbackDefaultLocale = "en";

    private readonly IReadOnlyList<LocalizationLocale> _availableLocales;
    private readonly string _defaultLocale;
    private string _currentLocale;

    /// <summary>创建并应用项目配置的默认 Locale。</summary>
    public LocalizationService()
    {
        MainThreadGuard.VerifyAccess();
        _defaultLocale = GetDefaultLocale();
        _availableLocales = Array.AsReadOnly(CreateAvailableLocales(_defaultLocale));

        TranslationServer.SetLocale(_defaultLocale);
        _currentLocale = TranslationServer.GetLocale();
    }

    /// <inheritdoc />
    public string DefaultLocale => _defaultLocale;

    /// <inheritdoc />
    public string CurrentLocale => _currentLocale;

    /// <inheritdoc />
    public IReadOnlyList<LocalizationLocale> AvailableLocales => _availableLocales;

    /// <inheritdoc />
    public bool IsPseudolocalizationEnabled
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            return TranslationServer.PseudolocalizationEnabled;
        }
    }

    /// <inheritdoc />
    public bool IsLocaleSupported(string locale)
    {
        MainThreadGuard.VerifyAccess();
        return !string.IsNullOrWhiteSpace(locale) && CanResolveLocale(StandardizeLocale(locale));
    }

    /// <inheritdoc />
    public string Translate(string key, string? context = null)
    {
        MainThreadGuard.VerifyAccess();
        ValidateKey(key, nameof(key));
        return TranslationServer.Translate(new StringName(key), ToStringName(context)).ToString();
    }

    /// <inheritdoc />
    public string TranslatePlural(string singularKey, string pluralKey, int count, string? context = null)
    {
        MainThreadGuard.VerifyAccess();
        ValidateKey(singularKey, nameof(singularKey));
        ValidateKey(pluralKey, nameof(pluralKey));
        return TranslationServer.TranslatePlural(
            new StringName(singularKey),
            new StringName(pluralKey),
            count,
            ToStringName(context)).ToString();
    }

    internal string ApplyLocale(string locale)
    {
        MainThreadGuard.VerifyAccess();
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException("Locale 不能为空。", nameof(locale));

        string standardized = StandardizeLocale(locale);
        if (!CanResolveLocale(standardized))
        {
            throw new ArgumentException(
                $"Locale 未加载或不受项目支持: {locale}。",
                nameof(locale));
        }

        if (string.Equals(_currentLocale, standardized, StringComparison.Ordinal))
            return _currentLocale;

        string previous = _currentLocale;
        TranslationServer.SetLocale(standardized);
        _currentLocale = TranslationServer.GetLocale();
        EventChannel.Emit(new LocaleChangedEvent(previous, _currentLocale));
        return _currentLocale;
    }

    private static string GetDefaultLocale()
    {
        Variant configured = ProjectSettings.GetSetting(DefaultLocaleSetting, "");
        string locale = configured.AsString();
        return string.IsNullOrWhiteSpace(locale)
            ? FallbackDefaultLocale
            : StandardizeLocale(locale);
    }

    private static LocalizationLocale[] CreateAvailableLocales(string defaultLocale)
    {
        string[] loadedLocales = TranslationServer.GetLoadedLocales();
        var locales = new List<LocalizationLocale>(loadedLocales.Length + 1)
        {
            new(defaultLocale, TranslationServer.GetLocaleName(defaultLocale), true),
        };
        for (int i = 0; i < loadedLocales.Length; i++)
        {
            string code = StandardizeLocale(loadedLocales[i]);
            bool isDefault = string.Equals(code, defaultLocale, StringComparison.Ordinal);
            if (isDefault)
                continue;

            locales.Add(new LocalizationLocale(code, TranslationServer.GetLocaleName(code), false));
        }

        locales.Sort(static (left, right) => string.CompareOrdinal(left.Code, right.Code));
        return locales.ToArray();
    }

    private bool CanResolveLocale(string locale)
    {
        return string.Equals(locale, _defaultLocale, StringComparison.Ordinal) ||
            TranslationServer.HasTranslationForLocale(locale, exact: false);
    }

    private static string StandardizeLocale(string locale) => TranslationServer.StandardizeLocale(locale);

    private static StringName ToStringName(string? value) =>
        new(value ?? string.Empty);

    private static void ValidateKey(string key, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("翻译键不能为空。", parameterName);
    }
}
