using System;

#nullable enable

namespace GoDo;

/// <summary>定义可由 <see cref="StateMachine{TContext,TState}"/> 管理的同步状态生命周期。</summary>
/// <typeparam name="TContext">状态共享的业务上下文类型。</typeparam>
public interface IState<in TContext>
{
    /// <summary>
    /// 在状态成为当前状态后进入该状态。
    /// <para>实现抛出异常前必须自行撤销未完成的初始化；状态机不会为失败的 Enter 调用 Exit。</para>
    /// </summary>
    /// <param name="context">状态机持有并在生命周期内复用的上下文。</param>
    void Enter(TContext context);

    /// <summary>
    /// 在状态不再是当前状态前退出该状态。
    /// <para>退出失败后状态机进入终止故障状态，并且不会重试本次 Exit。</para>
    /// </summary>
    /// <param name="context">状态机持有并在生命周期内复用的上下文。</param>
    void Exit(TContext context);
}
