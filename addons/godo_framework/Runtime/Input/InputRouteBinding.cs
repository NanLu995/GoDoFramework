using System;

#nullable enable

namespace GoDo;

/// <summary>拥有一个 Input Route Binding 的可乱序释放生命周期句柄。</summary>
public sealed class InputRouteBinding : IDisposable
{
    private InputActionRouter? _owner;
    private readonly ulong _token;

    internal InputRouteBinding(InputActionRouter owner, ulong token)
    {
        _owner = owner;
        _token = token;
    }

    /// <summary>释放 Binding；重复调用或 Router 已关闭时为空操作。</summary>
    public void Dispose()
    {
        InputActionRouter? owner = _owner;
        _owner = null;
        owner?.ReleaseBinding(_token);
    }
}
