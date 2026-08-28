using System;

#nullable enable

namespace GoDo;

/// <summary>拥有一个临时 Input Context Entry 的可乱序释放生命周期句柄。</summary>
/// <remarks>Dispose 幂等；SetBaseContext 或 InputService Shutdown 后再次释放为空操作。</remarks>
public sealed class InputContextLease : IDisposable
{
    private InputService? _owner;
    private readonly ulong _token;

    internal InputContextLease(InputService owner, ulong token)
    {
        _owner = owner;
        _token = token;
    }

    /// <summary>释放对应 Token 的临时 Context；后端应用失败时 Lease 仍有效并可重试。</summary>
    /// <exception cref="InputOperationException">后端拒绝应用释放后的 Context 集合。</exception>
    public void Dispose()
    {
        InputService? owner = _owner;
        if (owner == null)
            return;

        if (owner.ReleaseContextLease(_token))
            _owner = null;
    }
}
