#nullable enable

namespace GoDo;

/// <summary>为需要由状态机显式更新的状态提供可选契约。</summary>
/// <typeparam name="TContext">状态共享的业务上下文类型。</typeparam>
public interface IUpdatableState<in TContext>
{
    /// <summary>更新当前状态。</summary>
    /// <param name="context">状态机持有并在生命周期内复用的上下文。</param>
    /// <param name="deltaSeconds">调用方提供的更新时间间隔（秒）；状态机不修改或验证该值。</param>
    void Update(TContext context, double deltaSeconds);
}
