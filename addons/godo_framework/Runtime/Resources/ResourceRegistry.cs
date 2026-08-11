using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>
/// 语义资源注册表，将业务 ID 解析为 ResourceKey。
/// <para>业务代码可通过 Resolve 获取资源键，再交给 ResourceHub 加载。</para>
/// </summary>
public static class ResourceRegistry
{
    private static readonly Dictionary<string, ResourceKey> _map = new(StringComparer.Ordinal);
    private static bool _loaded;

    /// <summary>当前已加载的语义 ID 数量，主要用于测试与诊断。</summary>
    public static int Count => _map.Count;

    /// <summary>清空现有映射，再按清单顺序加载语义 ID。</summary>
    /// <param name="manifest">要加载的资源清单。</param>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">清单中的非空条目包含无效资源定位串。</exception>
    /// <remarks>空 ID 和 null 条目会记录 Warning 并跳过；重复 ID 记录 Warning 并以后者覆盖前者。</remarks>
    public static void Load(ResourceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        _map.Clear();
        AddManifest(manifest);
        _loaded = true;
    }

    /// <summary>清空现有映射，再按枚举顺序合并多个清单；重复 ID 以后者覆盖前者。</summary>
    /// <param name="manifests">按覆盖优先级从低到高排列的资源清单序列。</param>
    /// <exception cref="ArgumentNullException"><paramref name="manifests"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">序列包含 null 清单，或清单中的非空条目包含无效资源定位串。</exception>
    public static void LoadMerge(IEnumerable<ResourceManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);

        _map.Clear();
        foreach (ResourceManifest manifest in manifests)
        {
            if (manifest is null)
                throw new ArgumentException("ResourceManifest 集合不能包含 null。", nameof(manifests));

            AddManifest(manifest);
        }

        _loaded = true;
    }

    /// <summary>按语义 ID 获取必需资源的键。</summary>
    /// <param name="id">区分大小写的业务语义 ID。</param>
    /// <returns>当前注册表中与 <paramref name="id"/> 对应的资源键。</returns>
    /// <exception cref="InvalidOperationException">尚未调用 <see cref="Load"/> 或 <see cref="LoadMerge"/>。</exception>
    /// <exception cref="KeyNotFoundException">注册表中不存在指定 ID。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> 为 <see langword="null"/>。</exception>
    public static ResourceKey GetKey(string id)
    {
        if (!_loaded)
            throw new InvalidOperationException("ResourceRegistry 尚未加载任何 ResourceManifest。");

        if (!_map.TryGetValue(id, out ResourceKey key))
            throw new KeyNotFoundException($"ResourceRegistry 中未找到语义 ID 对应的资源：{id}");

        return key;
    }

    /// <summary>按语义 ID 尝试获取可选资源的键；未加载或找不到 ID 时不抛出缺失异常。</summary>
    /// <param name="id">区分大小写的业务语义 ID。</param>
    /// <param name="key">成功时为匹配的资源键；失败时为默认的无效资源键。</param>
    /// <returns>注册表已加载且包含指定 ID 时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException">注册表已加载且 <paramref name="id"/> 为 <see langword="null"/>。</exception>
    public static bool TryGetKey(string id, out ResourceKey key)
    {
        if (!_loaded)
        {
            key = default;
            return false;
        }

        return _map.TryGetValue(id, out key);
    }

    /// <summary>清空映射表，供测试或显式重新加载使用。</summary>
    public static void Clear()
    {
        _map.Clear();
        _loaded = false;
    }

    private static void AddManifest(ResourceManifest manifest)
    {
        foreach (ResourceManifestEntry? entry in manifest.Entries)
        {
            if (entry is null)
            {
                ErrorHub.Warn("ResourceManifest 中存在 null 记录，已跳过。", nameof(ResourceRegistry));
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                ErrorHub.Warn(
                    "ResourceManifest 中存在空 Id 的记录，已跳过。",
                    nameof(ResourceRegistry),
                    entry.Locator);
                continue;
            }

            if (_map.ContainsKey(entry.Id))
            {
                ErrorHub.Warn(
                    "ResourceManifest 中存在重复 Id，后者覆盖前者。",
                    nameof(ResourceRegistry),
                    entry.Id);
            }

            _map[entry.Id] = ResourceKey.Create(entry.Locator);
        }
    }
}
