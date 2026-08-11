using System;

#nullable enable

namespace GoDo;

/// <summary>用于定位 UI 配置的业务语义标识。</summary>
public readonly struct UiId : IEquatable<UiId>
{
    private readonly string? _value;

    /// <summary>去除首尾空白后的标识文本。</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>当前标识是否包含有效值。</summary>
    public bool IsValid => !string.IsNullOrEmpty(_value);

    private UiId(string value)
    {
        _value = value;
    }

    /// <summary>创建并验证一个区分大小写的 UI 标识。</summary>
    /// <param name="value">待验证的业务语义文本；创建前会移除首尾空白。</param>
    /// <returns>包含去除首尾空白后文本的 UI 标识。</returns>
    /// <exception cref="ArgumentException">标识为 null、空字符串或仅包含空白。</exception>
    public static UiId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("UI 标识不能为空。", nameof(value));

        return new UiId(value.Trim());
    }

    /// <summary>按区分大小写的标识文本比较。</summary>
    /// <param name="other">要与当前标识比较的 UI 标识。</param>
    /// <returns>两个规范化文本完全相同时为 <see langword="true"/>。</returns>
    public bool Equals(UiId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UiId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>判断两个 UI 标识是否相同。</summary>
    /// <param name="left">左侧 UI 标识。</param>
    /// <param name="right">右侧 UI 标识。</param>
    /// <returns>两个标识相同时为 <see langword="true"/>。</returns>
    public static bool operator ==(UiId left, UiId right) => left.Equals(right);

    /// <summary>判断两个 UI 标识是否不同。</summary>
    /// <param name="left">左侧 UI 标识。</param>
    /// <param name="right">右侧 UI 标识。</param>
    /// <returns>两个标识不同时为 <see langword="true"/>。</returns>
    public static bool operator !=(UiId left, UiId right) => !left.Equals(right);
}
