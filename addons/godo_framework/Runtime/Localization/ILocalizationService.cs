using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的本地化查询服务。</summary>
public interface ILocalizationService
{
    /// <summary>项目配置的默认 Locale。</summary>
    string DefaultLocale { get; }

    /// <summary>当前已应用的规范 Locale。</summary>
    string CurrentLocale { get; }

    /// <summary>项目已加载、可供选择的 Locale。</summary>
    IReadOnlyList<LocalizationLocale> AvailableLocales { get; }

    /// <summary>Godot 主翻译域当前是否启用伪本地化。</summary>
    bool IsPseudolocalizationEnabled { get; }

    /// <summary>判断 Locale 是否可由当前项目使用。</summary>
    bool IsLocaleSupported(string locale);

    /// <summary>查询当前 Locale 下的翻译。</summary>
    string Translate(string key, string? context = null);

    /// <summary>查询当前 Locale 下与数量对应的复数翻译。</summary>
    string TranslatePlural(string singularKey, string pluralKey, int count, string? context = null);
}
