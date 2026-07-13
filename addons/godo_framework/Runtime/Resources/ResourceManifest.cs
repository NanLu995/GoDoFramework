using Godot;
using Godot.Collections;

#nullable enable

namespace GoDo;

/// <summary>
/// 语义资源清单，用于将业务 ID 映射到 <c>res://</c> 或 <c>uid://</c> 定位串。
/// </summary>
[GlobalClass]
public partial class ResourceManifest : Resource
{
    /// <summary>清单条目集合。</summary>
    [Export]
    public Array<ResourceManifestEntry> Entries { get; set; } = new();
}
