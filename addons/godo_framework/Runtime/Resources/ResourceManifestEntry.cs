using Godot;

#nullable enable

namespace GoDo;

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
