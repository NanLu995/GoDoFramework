using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>按唯一键索引的一组只读配置项。</summary>
/// <typeparam name="TKey">不可为 <see langword="null"/> 的唯一键类型。</typeparam>
/// <typeparam name="TEntry">配置项类型；引用类型条目不能为 <see langword="null"/>。</typeparam>
public sealed class ConfigTable<TKey, TEntry> where TKey : notnull
{
    private readonly Dictionary<TKey, TEntry> _entries;

    /// <summary>配置项数量。</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// 从配置项序列创建索引；空项、空键或重复键会立即失败。
    /// </summary>
    /// <param name="entries">要枚举一次并建立索引的配置项序列。</param>
    /// <param name="keySelector">从每项提取唯一键的函数。</param>
    /// <param name="comparer">可选键比较器；为 <see langword="null"/> 时使用 <see cref="EqualityComparer{T}.Default"/>。</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> 或 <paramref name="keySelector"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException">序列包含空引用条目、选择器返回空键，或按比较器发现重复键。</exception>
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
    /// <param name="key">要查询的非空键。</param>
    /// <returns>与键匹配的配置项引用或值。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="KeyNotFoundException">索引中不存在该键。</exception>
    public TEntry Get(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_entries.TryGetValue(key, out TEntry? entry))
            return entry;

        throw new KeyNotFoundException($"配置键不存在：{key}。");
    }

    /// <summary>尝试获取指定键的配置项。</summary>
    /// <param name="key">要查询的非空键。</param>
    /// <param name="entry">找到时为匹配项；缺失时为 <see langword="default"/>。</param>
    /// <returns>找到匹配项时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> 为 <see langword="null"/>。</exception>
    public bool TryGet(TKey key, out TEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entries.TryGetValue(key, out entry);
    }
}
