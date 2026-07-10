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

/// <summary>语义资源清单中的单条映射记录。</summary>
[GlobalClass]
public partial class ResourceManifestEntry : Resource
{
    /// <summary>业务语义 ID，例如 <c>sword_iron</c> 或 <c>ui/icon_close</c>。</summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>资源定位串，支持 <c>res://</c> 与 <c>uid://</c>。</summary>
    [Export]
    public string Locator { get; set; } = string.Empty;
}
