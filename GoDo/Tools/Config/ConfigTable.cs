using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>按唯一键索引的一组只读配置项。</summary>
public sealed class ConfigTable<TKey, TEntry> where TKey : notnull
{
    private readonly Dictionary<TKey, TEntry> _entries;

    /// <summary>配置项数量。</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// 从配置项序列创建索引；空项、空键或重复键会立即失败。
    /// </summary>
    public ConfigTable(
        IEnumerable<TEntry> entries,
        Func<TEntry, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(keySelector);

        _entries = new Dictionary<TKey, TEntry>(comparer);
        int index = 0;
        foreach (TEntry entry in entries)
        {
            if (entry is null)
                throw new ArgumentException($"配置项索引 {index} 不能为 null。", nameof(entries));

            TKey key = keySelector(entry);
            if (key is null)
                throw new ArgumentException($"配置项索引 {index} 的键不能为 null。", nameof(entries));

            if (!_entries.TryAdd(key, entry))
                throw new ArgumentException($"配置键重复：{key}。", nameof(entries));

            index++;
        }
    }

    /// <summary>获取指定键的配置项；不存在时抛出 KeyNotFoundException。</summary>
    public TEntry Get(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_entries.TryGetValue(key, out TEntry? entry))
            return entry;

        throw new KeyNotFoundException($"配置键不存在：{key}。");
    }

    /// <summary>尝试获取指定键的配置项。</summary>
    public bool TryGet(TKey key, out TEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entries.TryGetValue(key, out entry);
    }
}
