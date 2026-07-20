using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>描述一个可由 <see cref="IDataTableService"/> 事务加载的数据集。</summary>
public sealed class DataTableSetDefinition
{
    private readonly IReadOnlyList<DataTableDefinition> _tables;

    /// <summary>
    /// 创建数据集描述。
    /// </summary>
    /// <param name="id">与 Manifest 一致的稳定数据集 ID。</param>
    /// <param name="formatVersion">编译产物格式版本。</param>
    /// <param name="protocolVersion">跨端协议版本。</param>
    /// <param name="tables">由同一份 Schema 生成的表描述。</param>
    /// <exception cref="ArgumentException">参数无效，或者表 ID、产物文件名存在重复。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="tables"/> 为 <see langword="null"/>。</exception>
    public DataTableSetDefinition(
        string id,
        int formatVersion,
        int protocolVersion,
        IReadOnlyList<DataTableDefinition> tables)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("DataTable 数据集 ID 不能为空。", nameof(id));
        if (formatVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(formatVersion));
        if (protocolVersion < 1)
            throw new ArgumentOutOfRangeException(nameof(protocolVersion));
        ArgumentNullException.ThrowIfNull(tables);

        var tableIds = new HashSet<string>(StringComparer.Ordinal);
        var artifacts = new HashSet<string>(StringComparer.Ordinal);
        var copiedTables = new DataTableDefinition[tables.Count];
        for (int index = 0; index < tables.Count; index++)
        {
            DataTableDefinition table = tables[index] ??
                throw new ArgumentException("DataTable 表描述不能为 null。", nameof(tables));
            if (!tableIds.Add(table.Id))
                throw new ArgumentException($"DataTable 表 ID 重复：{table.Id}", nameof(tables));
            if (!artifacts.Add(table.Artifact))
                throw new ArgumentException($"DataTable 产物文件名重复：{table.Artifact}", nameof(tables));
            copiedTables[index] = table;
        }

        Id = id;
        FormatVersion = formatVersion;
        ProtocolVersion = protocolVersion;
        _tables = copiedTables;
    }

    /// <summary>稳定数据集 ID。</summary>
    public string Id { get; }

    /// <summary>编译产物格式版本。</summary>
    public int FormatVersion { get; }

    /// <summary>跨端协议版本。</summary>
    public int ProtocolVersion { get; }

    /// <summary>Schema 中定义的全部表。</summary>
    public IReadOnlyList<DataTableDefinition> Tables => _tables;
}
