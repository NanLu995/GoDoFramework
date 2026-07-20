using System;

#nullable enable

namespace GoDo;

/// <summary>描述一张由 DataTable 生成代码负责解码的运行时数据表。</summary>
public sealed class DataTableDefinition
{
    private readonly Func<string, object> _loader;

    /// <summary>
    /// 创建数据表描述。
    /// </summary>
    /// <param name="id">与 Manifest 及二进制头一致的稳定表 ID。</param>
    /// <param name="artifact">数据集运行时目录内的二进制文件名。</param>
    /// <param name="loader">读取指定 Godot 路径并返回强类型只读表的生成代码委托。</param>
    /// <exception cref="ArgumentException">ID 或文件名为空，或者文件名包含目录分隔符。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="loader"/> 为 <see langword="null"/>。</exception>
    public DataTableDefinition(string id, string artifact, Func<string, object> loader)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("DataTable 表 ID 不能为空。", nameof(id));
        if (string.IsNullOrWhiteSpace(artifact) ||
            artifact is "." or ".." ||
            artifact.Contains('/') ||
            artifact.Contains('\\') ||
            artifact.Contains(':'))
        {
            throw new ArgumentException("DataTable 产物必须是运行时目录内的文件名。", nameof(artifact));
        }

        Id = id;
        Artifact = artifact;
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    /// <summary>稳定表 ID。</summary>
    public string Id { get; }

    /// <summary>运行时目录内的二进制文件名。</summary>
    public string Artifact { get; }

    internal object Load(string path) => _loader(path);
}
