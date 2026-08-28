using System;

#nullable enable

namespace GoDo;

/// <summary>拥有一组离散输入 Binding 的可乱序释放路由层。</summary>
public sealed class InputRouteScope : IDisposable
{
    private InputActionRouter? _owner;
    private readonly ulong _token;

    internal InputRouteScope(InputActionRouter owner, ulong token)
    {
        _owner = owner;
        _token = token;
    }

    /// <summary>绑定一个 Action 的非空 Transition 范围。</summary>
    /// <param name="action">要路由的已注册 Action。</param>
    /// <param name="transitions">触发处理器的一个或多个 Transition。</param>
    /// <param name="handler">返回传播决定的同步主线程处理器。</param>
    /// <returns>可独立、幂等、乱序释放的 Binding 句柄。</returns>
    /// <exception cref="ArgumentException"><paramref name="action"/> 是默认 ID，或 Transition 为空。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="transitions"/> 包含未知位。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InputOperationException">Scope 已释放、Router 已关闭、Action 未注册，或同 Scope 范围重叠。</exception>
    public InputRouteBinding Bind(
        InputActionId action,
        InputActionTransitions transitions,
        InputRouteHandler handler)
    {
        InputActionRouter? owner = _owner;
        if (owner == null)
            throw new InputOperationException("InputRouteScope 已释放。");
        return owner.Bind(_token, action, transitions, handler);
    }

    /// <summary>释放 Scope 及其全部 Binding；重复调用为空操作。</summary>
    public void Dispose()
    {
        InputActionRouter? owner = _owner;
        _owner = null;
        owner?.ReleaseScope(_token);
    }
}
