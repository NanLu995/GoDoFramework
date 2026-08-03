#nullable enable

namespace GoDo;

/// <summary>标识 UI 打开失败时所在的生命周期阶段。</summary>
public enum UiOpenPhase
{
    /// <summary>异常由旧版构造函数或无法确定阶段的外部代码创建。</summary>
    Unknown,

    /// <summary>正在同步或异步加载 UI PackedScene。</summary>
    Loading,

    /// <summary>正在实例化 UI、检查根节点类型或准备复用实例。</summary>
    Preparing,

    /// <summary>正在将 UI 加入目标层或完成复用实例激活。</summary>
    Committing,
}
