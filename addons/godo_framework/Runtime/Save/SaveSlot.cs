using System;

#nullable enable

namespace GoDo;

/// <summary>经过验证的存档槽位标识。</summary>
public readonly struct SaveSlot : IEquatable<SaveSlot>
{
    private const int MaxLength = 64;
    private readonly string? _value;

    /// <summary>槽位名称。</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>是否已经通过 Create 初始化。</summary>
    public bool IsValid => !string.IsNullOrEmpty(_value);

    private SaveSlot(string value)
    {
        _value = value;
    }

    /// <summary>创建仅包含 ASCII 字母、数字、下划线或连字符的槽位。</summary>
    public static SaveSlot Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("存档槽位不能为空。", nameof(value));
        if (value.Length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(value), $"存档槽位不能超过 {MaxLength} 个字符。");

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            bool allowed =
                character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9' or
                '_' or '-';

            if (!allowed)
            {
                throw new ArgumentException(
                    "存档槽位只能包含 ASCII 字母、数字、下划线或连字符。",
                    nameof(value));
            }
        }

        return new SaveSlot(value);
    }

    /// <inheritdoc />
    public bool Equals(SaveSlot other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SaveSlot other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>判断两个槽位是否相等。</summary>
    public static bool operator ==(SaveSlot left, SaveSlot right) => left.Equals(right);

    /// <summary>判断两个槽位是否不相等。</summary>
    public static bool operator !=(SaveSlot left, SaveSlot right) => !left.Equals(right);
}
