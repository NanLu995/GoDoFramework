using Godot;

namespace GoDo.GuideInput;

/// <summary>把稳定的 GoDo Binding ID 映射到一个 GUIDE Context/Action 输入槽位。</summary>
[GlobalClass]
public sealed partial class GuideInputBindingDefinition : Resource
{
    /// <summary>业务层使用的稳定 Binding ID。</summary>
    [Export]
    public string BindingId { get; set; } = string.Empty;

    /// <summary>目标 Context 的 GoDo 语义 ID。</summary>
    [Export]
    public string ContextId { get; set; } = string.Empty;

    /// <summary>目标 Action 的 GoDo 语义 ID。</summary>
    [Export]
    public string ActionId { get; set; } = string.Empty;

    /// <summary>目标 GUIDE Action Mapping 中的原始输入槽位索引。</summary>
    [Export(PropertyHint.Range, "0,64,1")]
    public int MappingIndex { get; set; }
}
