using Godot;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>把一个 GoDo 语义 Context ID 映射到 G.U.I.D.E Mapping Context Resource。</summary>
[GlobalClass]
public sealed partial class GuideInputContextBinding : Resource
{
    /// <summary>GoDo 业务语义 Context ID。</summary>
    [Export]
    public string ContextId { get; set; } = string.Empty;

    /// <summary>由 G.U.I.D.E 编辑器创建的 GUIDEMappingContext Resource。</summary>
    [Export]
    public Resource GuideContextResource { get; set; } = null!;
}
