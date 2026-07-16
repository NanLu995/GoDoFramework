namespace GoDo;

/// <summary>一个可供用户选择的项目 Locale。</summary>
public readonly record struct LocalizationLocale
{
    /// <summary>Godot 规范化后的 Locale 代码。</summary>
    public string Code { get; }

    /// <summary>Godot 提供的 Locale 可读名称。</summary>
    public string DisplayName { get; }

    /// <summary>是否为项目默认 Locale。</summary>
    public bool IsDefault { get; }

    /// <summary>创建一个可用 Locale 描述。</summary>
    public LocalizationLocale(string code, string displayName, bool isDefault)
    {
        Code = code;
        DisplayName = displayName;
        IsDefault = isDefault;
    }
}
