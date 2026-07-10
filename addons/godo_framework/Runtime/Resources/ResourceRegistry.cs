using System;
using System.Collections.Generic;
using Godot;

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

    /// <summary>清空并从给定清单重新加载映射表。</summary>
    public static void Load(ResourceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        _map.Clear();
        AddManifest(manifest);
        _loaded = true;
    }

    /// <summary>清空并按顺序合并加载多个清单；重复 ID 以后者覆盖前者。</summary>
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

    /// <summary>按语义 ID 解析资源键；未加载或找不到 ID 时抛出异常。</summary>
    public static ResourceKey Resolve(string id)
    {
        if (!_loaded)
            throw new InvalidOperationException("ResourceRegistry 尚未加载任何 ResourceManifest。");

        if (!_map.TryGetValue(id, out ResourceKey key))
            throw new KeyNotFoundException($"ResourceRegistry 中未找到语义 ID 对应的资源：{id}");

        return key;
    }

    /// <summary>按语义 ID 尝试解析资源键；未加载或找不到 ID 时返回 false。</summary>
    public static bool TryResolve(string id, out ResourceKey key)
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
                GD.PushWarning("ResourceManifest 中存在 null 记录，已跳过。");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Id))
            {
                GD.PushWarning($"ResourceManifest 中存在空 Id 的记录，Locator: {entry.Locator}，已跳过。");
                continue;
            }

            if (_map.ContainsKey(entry.Id))
                GD.PushWarning($"ResourceManifest 中存在重复 Id: {entry.Id}，后者覆盖前者。");

            _map[entry.Id] = ResourceKey.Create(entry.Locator);
        }
    }
}
