using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>
/// 面向业务层的本地化查询服务。访问 Godot TranslationServer 的查询成员只能在 GoDoRuntime 所在的主线程调用。
/// </summary>
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
    /// <param name="locale">要规范化并匹配默认语言或已加载翻译的 Locale；空白值视为不支持。</param>
    /// <returns>Locale 是默认语言或能由已加载翻译进行非精确匹配时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    bool IsLocaleSupported(string locale);

    /// <summary>查询当前 Locale 下的翻译。</summary>
    /// <param name="key">非空白的稳定翻译键。</param>
    /// <param name="context">可选翻译上下文；<see langword="null"/> 按无上下文查询。</param>
    /// <returns>当前 Locale 对应的翻译；缺失时沿用 Godot 行为返回源键。</returns>
    /// <exception cref="System.ArgumentException"><paramref name="key"/> 为空或仅包含空白字符。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    string Translate(string key, string? context = null);

    /// <summary>查询当前 Locale 下与数量对应的复数翻译。</summary>
    /// <param name="singularKey">非空白的单数翻译键。</param>
    /// <param name="pluralKey">非空白的复数翻译键。</param>
    /// <param name="count">交给 Godot 复数规则选择形式的数量。</param>
    /// <param name="context">可选翻译上下文；<see langword="null"/> 按无上下文查询。</param>
    /// <returns>当前 Locale 和数量对应的翻译；缺失时沿用 Godot 的源键回退行为。</returns>
    /// <exception cref="System.ArgumentException"><paramref name="singularKey"/> 或 <paramref name="pluralKey"/> 为空或仅包含空白字符。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    string TranslatePlural(string singularKey, string pluralKey, int count, string? context = null);
}
