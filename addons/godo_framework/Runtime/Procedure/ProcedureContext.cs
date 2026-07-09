using System;

#nullable enable

namespace GoDo;

/// <summary>Procedure 进入和退出时可用的框架上下文。</summary>
public sealed class ProcedureContext
{
    private readonly Action<IProcedure> _requestChange;

    internal ProcedureContext(Action<IProcedure> requestChange)
    {
        _requestChange = requestChange;
    }

    /// <summary>获取已注册的长期框架服务。</summary>
    public TService GetService<TService>() where TService : class => Services.Get<TService>();

    /// <summary>尝试获取已注册的长期框架服务。</summary>
    public bool TryGetService<TService>(out TService? service) where TService : class =>
        Services.TryGet(out service);

    /// <summary>
    /// 请求在当前流程切换安全结束后进入目标流程。
    /// <para>本方法不会递归执行切换；ProcedureService 会串行处理请求。</para>
    /// </summary>
    public void RequestChange(IProcedure next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _requestChange(next);
    }
}
