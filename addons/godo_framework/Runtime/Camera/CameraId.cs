using System;

#nullable enable

namespace GoDo;

/// <summary>用于在业务代码中稳定标识主镜头 Rig 的语义 ID。</summary>
public readonly struct CameraId : IEquatable<CameraId>
{
    private readonly string? _value;

    /// <summary>ID 的原始值；默认值返回空字符串。</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>当前值是否为未初始化的默认值。</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    private CameraId(string value)
    {
        _value = value;
    }

    /// <summary>从非空、无首尾空白的业务语义字符串创建镜头 ID。</summary>
    /// <param name="value">区分大小写且不包含首尾空白的业务语义 ID。</param>
    /// <returns>包含原始字符串的镜头 ID。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为空、仅含空白或包含首尾空白。</exception>
    public static CameraId Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("镜头 ID 不能包含首尾空白。", nameof(value));

        return new CameraId(value);
    }

    /// <inheritdoc />
    public bool Equals(CameraId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CameraId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>使用区分大小写的序号比较判断两个镜头 ID 是否相等。</summary>
    /// <param name="left">左侧镜头 ID。</param>
    /// <param name="right">右侧镜头 ID。</param>
    /// <returns>两个 ID 的原始值完全相同时为 <see langword="true"/>。</returns>
    public static bool operator ==(CameraId left, CameraId right) => left.Equals(right);

    /// <summary>使用区分大小写的序号比较判断两个镜头 ID 是否不相等。</summary>
    /// <param name="left">左侧镜头 ID。</param>
    /// <param name="right">右侧镜头 ID。</param>
    /// <returns>两个 ID 的原始值不同时为 <see langword="true"/>。</returns>
    public static bool operator !=(CameraId left, CameraId right) => !left.Equals(right);
}
