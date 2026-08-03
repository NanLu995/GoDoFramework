#nullable enable

namespace GoDo;

/// <summary>标识主内容场景切换失败时所在的生命周期阶段。</summary>
public enum SceneChangePhase
{
    /// <summary>异常由旧版构造函数或无法确定阶段的外部代码创建。</summary>
    Unknown,

    /// <summary>正在异步加载目标 PackedScene。</summary>
    Loading,

    /// <summary>正在从目标 PackedScene 创建场景节点。</summary>
    Instantiating,

    /// <summary>正在将新场景加入场景树并提交为当前场景。</summary>
    Committing,
}
