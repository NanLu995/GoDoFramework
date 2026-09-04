using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable

namespace GoDo;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DataTableManifest))]
internal sealed partial class DataTableManifestJsonContext : JsonSerializerContext
{
}

internal sealed class DataTableManifest
{
    [JsonPropertyName("data_set_id")]
    public string DataSetId { get; set; } = string.Empty;

    [JsonPropertyName("format_version")]
    public int FormatVersion { get; set; }

    [JsonPropertyName("protocol_version")]
    public int ProtocolVersion { get; set; }

    [JsonPropertyName("tables")]
    public List<DataTableManifestTable> Tables { get; set; } = [];
}

internal sealed class DataTableManifestTable
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("artifact")]
    public string Artifact { get; set; } = string.Empty;
}
