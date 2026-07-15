using System;

#nullable enable

namespace GoDo;

/// <summary>跨输入后端稳定标识一个可重绑定输入槽位。</summary>
public readonly struct InputBindingId : IEquatable<InputBindingId>
{
    private readonly string? _value;

    /// <summary>ID 的原始值；默认值返回空字符串。</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>当前值是否为未初始化的默认值。</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    private InputBindingId(string value)
    {
        _value = value;
    }

    /// <summary>从非空、无首尾空白的业务语义字符串创建 Binding ID。</summary>
    public static InputBindingId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("输入 Binding ID 不能包含首尾空白。", nameof(value));

        return new InputBindingId(value);
    }

    /// <inheritdoc />
    public bool Equals(InputBindingId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is InputBindingId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>比较两个 Binding ID 是否相等。</summary>
    public static bool operator ==(InputBindingId left, InputBindingId right) => left.Equals(right);

    /// <summary>比较两个 Binding ID 是否不相等。</summary>
    public static bool operator !=(InputBindingId left, InputBindingId right) => !left.Equals(right);
}
