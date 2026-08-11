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
    /// <param name="value">区分大小写且不包含首尾空白的业务语义值。</param>
    /// <returns>包含原始字符串的 Binding ID。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空、仅含空白或包含首尾空白。</exception>
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
    /// <param name="left">左侧 Binding ID。</param>
    /// <param name="right">右侧 Binding ID。</param>
    /// <returns>两个原始值按区分大小写的序号比较相同时为 <see langword="true"/>。</returns>
    public static bool operator ==(InputBindingId left, InputBindingId right) => left.Equals(right);

    /// <summary>比较两个 Binding ID 是否不相等。</summary>
    /// <param name="left">左侧 Binding ID。</param>
    /// <param name="right">右侧 Binding ID。</param>
    /// <returns>两个原始值不同时为 <see langword="true"/>。</returns>
    public static bool operator !=(InputBindingId left, InputBindingId right) => !left.Equals(right);
}
