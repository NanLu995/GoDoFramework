using Godot;
using Godot.Collections;

namespace GoDo.GuideInput;

/// <summary>声明游戏语义 ID 与 G.U.I.D.E Resource 的固定运行时映射。</summary>
/// <remarks>
/// 此 Resource 只在后端安装时解析，初始化后不支持动态修改。Action、Context 和可重绑定槽位
/// 必须使用稳定且互不冲突的语义 ID；校验失败会使 InputService 保持未就绪。
/// </remarks>
[GlobalClass]
public sealed partial class GuideInputProfile : Resource
{
    /// <summary>按固定采样顺序排列的 Action 映射。</summary>
    [Export]
    public Array<GuideInputActionBinding> Actions { get; set; } = new();

    /// <summary>可由 InputService Context 栈激活的 Mapping Context 映射。</summary>
    [Export]
    public Array<GuideInputContextBinding> Contexts { get; set; } = new();

    /// <summary>显式公开给业务层的稳定运行时重绑定槽位。</summary>
    [Export]
    public Array<GuideInputBindingDefinition> Bindings { get; set; } = new();
}
