using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GodotFileAccess = Godot.FileAccess;

#nullable enable

namespace GoDo;

/// <summary>
/// 按业务请求加载、缓存和卸载 DataTable 数据集。
/// <para>服务只在完整加载后发布表，框架启动时不会自动读取任何业务数据。</para>
/// </summary>
public sealed partial class DataTableService : Node, IDataTableService
{
    private readonly Dictionary<string, LoadedDataSet> _loadedDataSets =
        new(StringComparer.Ordinal);
    private readonly HashSet<string> _loadingDataSets = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _shutdownCancellation = new();

    /// <inheritdoc />
    public async Task LoadAsync(
        DataTableSetDefinition definition,
        string runtimeDirectory,
        Action<DataTableLoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
            throw new ArgumentException("DataTable 运行时目录不能为空。", nameof(runtimeDirectory));

        string normalizedDirectory = runtimeDirectory.TrimEnd('/', '\\');
        if (_loadedDataSets.TryGetValue(definition.Id, out LoadedDataSet? loaded))
        {
            if (!StringComparer.Ordinal.Equals(loaded.RuntimeDirectory, normalizedDirectory))
            {
                throw new DataTableLoadException(
                    definition.Id,
                    $"数据集已经从其他目录加载：{loaded.RuntimeDirectory}");
            }
            return;
        }
        if (!_loadingDataSets.Add(definition.Id))
            throw new DataTableLoadException(definition.Id, "数据集正在加载，不能重复发起请求。");

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdownCancellation.Token);
        CancellationToken token = linkedCancellation.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            Manifest manifest = ReadManifest(definition, normalizedDirectory);
            Dictionary<string, DataTableDefinition> definitions = IndexDefinitions(definition);
            var stagedTables = new Dictionary<string, object>(StringComparer.Ordinal);
            int total = manifest.Tables.Count;
            progress?.Invoke(new DataTableLoadProgress(definition.Id, 0, total, null));

            for (int index = 0; index < total; index++)
            {
                token.ThrowIfCancellationRequested();
                ManifestTable manifestTable = manifest.Tables[index];
                if (!definitions.TryGetValue(manifestTable.Id, out DataTableDefinition? tableDefinition))
                {
                    throw new DataTableLoadException(
                        definition.Id,
                        $"Manifest 包含生成代码无法识别的表：{manifestTable.Id}");
                }
                if (!StringComparer.Ordinal.Equals(tableDefinition.Artifact, manifestTable.Artifact))
                {
                    throw new DataTableLoadException(
                        definition.Id,
                        $"表 {manifestTable.Id} 的产物文件名与生成代码不一致。");
                }

                string artifactPath = JoinPath(normalizedDirectory, manifestTable.Artifact);
                object table;
                try
                {
                    table = tableDefinition.Load(artifactPath) ??
                        throw new InvalidDataException("生成的 DataTable 解码器返回了 null。");
                }
                catch (Exception exception) when (exception is not DataTableLoadException)
                {
                    throw new DataTableLoadException(
                        definition.Id,
                        $"加载表 {manifestTable.Id} 失败：{artifactPath}",
                        exception);
                }
                stagedTables.Add(manifestTable.Id, table);
                progress?.Invoke(new DataTableLoadProgress(
                    definition.Id,
                    index + 1,
                    total,
                    manifestTable.Id));

                if (index + 1 < total)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    MainThreadGuard.VerifyAccess();
                }
            }

            token.ThrowIfCancellationRequested();
            _loadedDataSets.Add(
                definition.Id,
                new LoadedDataSet(normalizedDirectory, stagedTables));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DataTableLoadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DataTableLoadException(
                definition.Id,
                $"加载 DataTable 数据集失败：{definition.Id}",
                exception);
        }
        finally
        {
            _loadingDataSets.Remove(definition.Id);
        }
    }

    /// <inheritdoc />
    public bool IsLoaded(string dataSetId)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetId);
        return _loadedDataSets.ContainsKey(dataSetId);
    }

    /// <inheritdoc />
    public TTable GetTable<TTable>(string dataSetId, string tableId) where TTable : class
    {
        MainThreadGuard.VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tableId);

        if (!_loadedDataSets.TryGetValue(dataSetId, out LoadedDataSet? dataSet))
            throw new InvalidOperationException($"DataTable 数据集尚未加载：{dataSetId}");
        if (!dataSet.Tables.TryGetValue(tableId, out object? table))
            throw new InvalidOperationException($"DataTable 表尚未加载：{dataSetId}/{tableId}");
        if (table is not TTable typedTable)
        {
            throw new InvalidOperationException(
                $"DataTable 表类型不匹配：{dataSetId}/{tableId}，实际类型为 {table.GetType().FullName}。");
        }
        return typedTable;
    }

    /// <inheritdoc />
    public bool Unload(string dataSetId)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSetId);
        if (_loadingDataSets.Contains(dataSetId))
            throw new InvalidOperationException($"DataTable 数据集正在加载，不能卸载：{dataSetId}");
        return _loadedDataSets.Remove(dataSetId);
    }

    internal void Shutdown()
    {
        MainThreadGuard.VerifyAccess();
        _shutdownCancellation.Cancel();
        _loadedDataSets.Clear();
    }

    private static Manifest ReadManifest(
        DataTableSetDefinition definition,
        string runtimeDirectory)
    {
        string manifestPath = JoinPath(runtimeDirectory, "manifest.json");
        using GodotFileAccess? file = GodotFileAccess.Open(manifestPath, GodotFileAccess.ModeFlags.Read);
        if (file == null)
        {
            throw new DataTableLoadException(
                definition.Id,
                $"无法打开 DataTable Manifest，Error={GodotFileAccess.GetOpenError()}：{manifestPath}");
        }
        ulong length = file.GetLength();
        if (length > 16 * 1024 * 1024)
            throw new DataTableLoadException(definition.Id, "DataTable Manifest 超过 16 MiB 读取上限。");
        byte[] bytes = file.GetBuffer(checked((long)length));
        if (bytes.Length != checked((int)length))
            throw new DataTableLoadException(definition.Id, $"DataTable Manifest 未完整读取：{manifestPath}");

        Manifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<Manifest>(bytes) ??
                throw new JsonException("Manifest 根对象为 null。");
        }
        catch (JsonException exception)
        {
            throw new DataTableLoadException(definition.Id, "DataTable Manifest JSON 无效。", exception);
        }

        if (!StringComparer.Ordinal.Equals(manifest.DataSetId, definition.Id))
            throw new DataTableLoadException(definition.Id, "DataTable Manifest 的 data_set_id 不匹配。");
        if (manifest.FormatVersion != definition.FormatVersion)
            throw new DataTableLoadException(definition.Id, "DataTable Manifest 的 format_version 不兼容。");
        if (manifest.ProtocolVersion != definition.ProtocolVersion)
            throw new DataTableLoadException(definition.Id, "DataTable Manifest 的 protocol_version 不兼容。");
        ValidateManifestTables(definition.Id, manifest.Tables);
        return manifest;
    }

    private static Dictionary<string, DataTableDefinition> IndexDefinitions(
        DataTableSetDefinition definition)
    {
        var result = new Dictionary<string, DataTableDefinition>(StringComparer.Ordinal);
        foreach (DataTableDefinition table in definition.Tables)
            result.Add(table.Id, table);
        return result;
    }

    private static void ValidateManifestTables(string dataSetId, IReadOnlyList<ManifestTable> tables)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var artifacts = new HashSet<string>(StringComparer.Ordinal);
        foreach (ManifestTable table in tables)
        {
            if (string.IsNullOrWhiteSpace(table.Id))
                throw new DataTableLoadException(dataSetId, "DataTable Manifest 包含空表 ID。");
            if (!ids.Add(table.Id))
                throw new DataTableLoadException(dataSetId, $"DataTable Manifest 表 ID 重复：{table.Id}");
            if (string.IsNullOrWhiteSpace(table.Artifact) ||
                table.Artifact is "." or ".." ||
                table.Artifact.Contains('/') ||
                table.Artifact.Contains('\\') ||
                table.Artifact.Contains(':'))
            {
                throw new DataTableLoadException(dataSetId, $"DataTable Manifest 产物路径无效：{table.Artifact}");
            }
            if (!artifacts.Add(table.Artifact))
            {
                throw new DataTableLoadException(
                    dataSetId,
                    $"DataTable Manifest 产物文件名重复：{table.Artifact}");
            }
        }
    }

    private static string JoinPath(string directory, string fileName)
    {
        if (directory.Contains("://", StringComparison.Ordinal))
            return $"{directory}/{fileName}";
        return Path.Combine(directory, fileName);
    }

    private sealed class LoadedDataSet
    {
        internal LoadedDataSet(string runtimeDirectory, Dictionary<string, object> tables)
        {
            RuntimeDirectory = runtimeDirectory;
            Tables = tables;
        }

        internal string RuntimeDirectory { get; }
        internal Dictionary<string, object> Tables { get; }
    }

    private sealed class Manifest
    {
        [JsonPropertyName("data_set_id")]
        public string DataSetId { get; set; } = string.Empty;

        [JsonPropertyName("format_version")]
        public int FormatVersion { get; set; }

        [JsonPropertyName("protocol_version")]
        public int ProtocolVersion { get; set; }

        [JsonPropertyName("tables")]
        public List<ManifestTable> Tables { get; set; } = [];
    }

    private sealed class ManifestTable
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("artifact")]
        public string Artifact { get; set; } = string.Empty;
    }
}
