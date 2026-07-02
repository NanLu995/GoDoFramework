namespace GoDo;

/// <summary>
/// 可池化节点的可选生命周期接口。
/// 节点负责在回调中初始化和清理自身业务状态，Pool 不猜测需要重置哪些字段。
/// </summary>
public interface IPoolable
{
    /// <summary>节点加入目标父节点后调用。</summary>
    void OnAcquire();

    /// <summary>节点从场景树移除前调用。</summary>
    void OnRelease();
}
