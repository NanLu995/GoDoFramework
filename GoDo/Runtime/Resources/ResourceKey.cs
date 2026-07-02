using System;

#nullable enable

namespace GoDo;

/// <summary>
/// GoDo 资源定位键。首版仅接受规范化的 <c>res://</c> 绝对路径，
/// 为未来迁移到 Godot Resource UID 保留统一 API 边界。
/// </summary>
public readonly struct ResourceKey : IEquatable<ResourceKey>
{
    private readonly string? _value;

    /// <summary>规范化后的 Godot 资源路径。</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>当前键是否包含有效值。</summary>
    public bool IsValid => !string.IsNullOrEmpty(_value);

    private ResourceKey(string value)
    {
        _value = value;
    }

    /// <summary>创建并验证一个资源键。</summary>
    public static ResourceKey Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("资源路径不能为空。", nameof(path));

        string normalizedPath = path.Trim().Replace('\\', '/');
        if (!normalizedPath.StartsWith("res://", StringComparison.Ordinal))
            throw new ArgumentException("资源路径必须是以 res:// 开头的绝对路径。", nameof(path));

        string relativePath = normalizedPath[6..];
        if (relativePath.Length == 0 ||
            relativePath.StartsWith("/", StringComparison.Ordinal) ||
            relativePath.StartsWith("./", StringComparison.Ordinal) ||
            relativePath.Contains("//", StringComparison.Ordinal) ||
            relativePath.Contains("/./", StringComparison.Ordinal) ||
            relativePath.EndsWith("/.", StringComparison.Ordinal) ||
            relativePath.EndsWith("/", StringComparison.Ordinal))
        {
            throw new ArgumentException("资源路径必须是规范化的具体资源文件路径。", nameof(path));
        }

        if (relativePath.StartsWith("../", StringComparison.Ordinal) ||
            relativePath.Contains("/../", StringComparison.Ordinal) ||
            relativePath.EndsWith("/..", StringComparison.Ordinal))
        {
            throw new ArgumentException("资源路径不能包含父目录跳转。", nameof(path));
        }

        return new ResourceKey(normalizedPath);
    }

    /// <summary>按规范化后的区分大小写路径比较。</summary>
    public bool Equals(ResourceKey other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ResourceKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>判断两个资源键是否相同。</summary>
    public static bool operator ==(ResourceKey left, ResourceKey right) => left.Equals(right);

    /// <summary>判断两个资源键是否不同。</summary>
    public static bool operator !=(ResourceKey left, ResourceKey right) => !left.Equals(right);
}
