using Godot;
using Godot.Collections;

namespace GoDo.GuideInput;

/// <summary>声明游戏语义 ID 与 G.U.I.D.E Resource 的固定运行时映射。</summary>
[GlobalClass]
public sealed partial class GuideInputProfile : Resource
{
    /// <summary>按固定采样顺序排列的 Action 映射。</summary>
    [Export]
    public Array<GuideInputActionBinding> Actions { get; set; } = new();

    /// <summary>可由 InputService Context 栈激活的 Mapping Context 映射。</summary>
    [Export]
    public Array<GuideInputContextBinding> Contexts { get; set; } = new();
}
