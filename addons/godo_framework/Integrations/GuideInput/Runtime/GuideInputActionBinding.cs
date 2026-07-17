using Godot;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>把一个 GoDo 语义 Action ID 映射到 G.U.I.D.E Action Resource。</summary>
/// <remarks>
/// 仅供 <see cref="GuideInputProfile"/> 在安装后端时读取。业务代码应使用
/// <see cref="IInputService"/> 与 <see cref="InputActionId"/>，不得保存第三方 Action 类型。
/// 空值、重复 ID 或重复 Resource 会使后端安装失败。
/// </remarks>
[GlobalClass]
public sealed partial class GuideInputActionBinding : Resource
{
    /// <summary>GoDo 业务语义 Action ID。</summary>
    /// <remarks>必须为非空稳定 ID，并且在同一 Profile 内唯一。</remarks>
    [Export]
    public string ActionId { get; set; } = string.Empty;

    /// <summary>由 G.U.I.D.E 编辑器创建的 GUIDEAction Resource。</summary>
    /// <remarks>不能为空；其值类型在后端初始化后固定，运行时不得替换。</remarks>
    [Export]
    public Resource GuideActionResource { get; set; } = null!;
}
