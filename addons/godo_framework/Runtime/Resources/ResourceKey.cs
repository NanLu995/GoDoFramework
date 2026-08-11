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

    /// <summary>创建并验证一个 <c>res://</c> 路径键或 <c>uid://</c> UID 键。</summary>
    /// <param name="path">待验证的定位串；首尾空白会被移除，路径中的反斜杠会转换为正斜杠。</param>
    /// <returns>包含规范化定位串的资源键。</returns>
    /// <exception cref="ArgumentException">
    /// 定位串为空、前缀不受支持、没有指向具体资源，或包含重复分隔符、当前目录段或父目录跳转。
    /// </exception>
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

    /// <summary>
    /// 通过路径语义入口创建资源键；当前行为与 <see cref="Create(string)"/> 相同，不额外限制定位串前缀。
    /// </summary>
    /// <param name="resPath">待验证的资源定位串，通常为 <c>res://</c> 路径。</param>
    /// <returns>经过 <see cref="Create(string)"/> 验证和规范化的资源键。</returns>
    /// <exception cref="ArgumentException">定位串不符合 <see cref="Create(string)"/> 的规则。</exception>
    public static ResourceKey FromPath(string resPath) => Create(resPath);

    /// <summary>
    /// 通过 UID 语义入口创建资源键；当前行为与 <see cref="Create(string)"/> 相同，不额外限制定位串前缀。
    /// </summary>
    /// <param name="uidText">待验证的资源定位串，通常为包含非空标识的 <c>uid://</c> 字符串。</param>
    /// <returns>经过 <see cref="Create(string)"/> 验证的资源键。</returns>
    /// <exception cref="ArgumentException">定位串不符合 <see cref="Create(string)"/> 的规则。</exception>
    public static ResourceKey FromUid(string uidText) => Create(uidText);

    /// <summary>
    /// 尝试将 <c>res://</c> 路径解析为 Godot UID 资源键。
    /// <para>找不到 UID 时返回原始路径形式的资源键。</para>
    /// </summary>
    /// <param name="resPath">待查询 Godot 资源 UID 表的规范化 <c>res://</c> 资源路径。</param>
    /// <returns>找到 UID 时返回 UID 键，否则返回经过验证的原始路径键。</returns>
    /// <exception cref="ArgumentException"><paramref name="resPath"/> 不符合资源键规则。</exception>
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

    /// <summary>按规范化后的定位串执行区分大小写的比较。</summary>
    /// <param name="other">要与当前键比较的资源键。</param>
    /// <returns>两个键的定位串完全相同时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    public bool Equals(ResourceKey other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ResourceKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>判断两个资源键的定位串是否完全相同。</summary>
    /// <param name="left">左侧资源键。</param>
    /// <param name="right">右侧资源键。</param>
    /// <returns>两个键相同时为 <see langword="true"/>。</returns>
    public static bool operator ==(ResourceKey left, ResourceKey right) => left.Equals(right);

    /// <summary>判断两个资源键的定位串是否不同。</summary>
    /// <param name="left">左侧资源键。</param>
    /// <param name="right">右侧资源键。</param>
    /// <returns>两个键不同时为 <see langword="true"/>。</returns>
    public static bool operator !=(ResourceKey left, ResourceKey right) => !left.Equals(right);
}
