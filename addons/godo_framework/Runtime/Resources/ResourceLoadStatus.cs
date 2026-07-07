namespace GoDo;

/// <summary>ResourceHub 异步加载操作状态。</summary>
public enum ResourceLoadStatus
{
    /// <summary>Godot 后台线程仍在加载。</summary>
    Loading,

    /// <summary>资源已成功加载并通过类型检查。</summary>
    Completed,

    /// <summary>请求、加载或类型检查失败。</summary>
    Failed,
}
