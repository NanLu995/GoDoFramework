using Godot;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>把一个 GoDo 语义 Action ID 映射到 G.U.I.D.E Action Resource。</summary>
[GlobalClass]
public sealed partial class GuideInputActionBinding : Resource
{
    /// <summary>GoDo 业务语义 Action ID。</summary>
    [Export]
    public string ActionId { get; set; } = string.Empty;

    /// <summary>由 G.U.I.D.E 编辑器创建的 GUIDEAction Resource。</summary>
    [Export]
    public Resource GuideActionResource { get; set; } = null!;
}
