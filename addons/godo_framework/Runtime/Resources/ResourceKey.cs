using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// GoDo 资源定位键。支持规范化的 <c>res://</c> 绝对路径与 Godot <c>uid://</c> 资源 UID。
/// </summary>
public readonly struct ResourceKey : IEquatable<ResourceKey>
{
    private readonly string? _value;

    /// <summary>规范化后的 Godot 资源路径。</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>当前键是否包含有效值。</summary>
    public bool IsValid => !string.IsNullOrEmpty(_value);

    /// <summary>当前键是否使用 Godot UID 定位。</summary>
    public bool IsUid => _value?.StartsWith("uid://", StringComparison.Ordinal) == true;

    private ResourceKey(string value)
    {
        _value = value;
    }

    /// <summary>创建并验证一个资源键。</summary>
    public static ResourceKey Create(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("资源路径不能为空。", nameof(path));

        string trimmedPath = path.Trim();
        if (trimmedPath.StartsWith("uid://", StringComparison.Ordinal))
            return CreateUid(trimmedPath, nameof(path));

        string normalizedPath = trimmedPath.Replace('\\', '/');
        if (!normalizedPath.StartsWith("res://", StringComparison.Ordinal))
            throw new ArgumentException("资源路径必须是以 res:// 或 uid:// 开头的定位串。", nameof(path));

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

    /// <summary>创建并验证一个 <c>res://</c> 路径资源键。</summary>
    public static ResourceKey FromPath(string resPath) => Create(resPath);

    /// <summary>创建并验证一个 <c>uid://</c> 资源键。</summary>
    public static ResourceKey FromUid(string uidText) => Create(uidText);

    /// <summary>
    /// 尝试将 <c>res://</c> 路径解析为 Godot UID 资源键。
    /// <para>找不到 UID 时返回原始路径形式的资源键。</para>
    /// </summary>
    public static ResourceKey ResolveUid(string resPath)
    {
        ResourceKey pathKey = FromPath(resPath);
        long id = ResourceLoader.GetResourceUid(pathKey.Value);
        if (id == ResourceUid.InvalidId)
            return pathKey;

        return FromUid(ResourceUid.IdToText(id));
    }

    private static ResourceKey CreateUid(string uidText, string parameterName)
    {
        if (uidText.Length == "uid://".Length)
            throw new ArgumentException("UID 资源键必须包含非空标识。", parameterName);

        return new ResourceKey(uidText);
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
