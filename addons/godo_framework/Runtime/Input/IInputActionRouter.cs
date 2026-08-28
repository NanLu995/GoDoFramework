namespace GoDo;

/// <summary>按生命周期 Scope 从顶层向下消费离散输入 Action 的路由服务。</summary>
/// <remarks>Scope 与 Binding 的创建、释放以及所有处理器调用都限制在 Godot 主线程。</remarks>
public interface IInputActionRouter
{
    /// <summary>创建后进先派发的路由 Scope。</summary>
    /// <param name="debugName">用于诊断与异常报告的非空稳定名称。</param>
    /// <returns>拥有该 Scope 生命周期的句柄。</returns>
    /// <exception cref="System.ArgumentException"><paramref name="debugName"/> 为空或包含首尾空白。</exception>
    /// <exception cref="InputOperationException">Router 已关闭。</exception>
    InputRouteScope PushScope(string debugName);
}

/// <summary>处理一个 Action 在当前采样帧命中的完整 Transition 集合。</summary>
/// <param name="state">可安全保存的完整 Action 值快照。</param>
/// <param name="matchedTransitions">当前 Binding 实际命中的非空 Transition 集合。</param>
/// <returns>是否消费当前 Action。</returns>
public delegate InputRouteResult InputRouteHandler(
    InputActionFrameState state,
    InputActionTransitions matchedTransitions);
