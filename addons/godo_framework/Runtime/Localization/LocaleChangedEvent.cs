namespace GoDo;

/// <summary>当前 Locale 成功切换后供业务与框架观察的事实事件。</summary>
public readonly struct LocaleChangedEvent : IEventMessage
{
    /// <summary>切换前的规范 Locale。</summary>
    public string PreviousLocale { get; }

    /// <summary>切换后的规范 Locale。</summary>
    public string CurrentLocale { get; }

    /// <summary>创建一个语言变更事实事件。</summary>
    /// <param name="previousLocale">切换前的 Locale；构造器原样保存，不执行规范化。</param>
    /// <param name="currentLocale">切换后的 Locale；构造器原样保存，不执行规范化。</param>
    public LocaleChangedEvent(string previousLocale, string currentLocale)
    {
        PreviousLocale = previousLocale;
        CurrentLocale = currentLocale;
    }
}
