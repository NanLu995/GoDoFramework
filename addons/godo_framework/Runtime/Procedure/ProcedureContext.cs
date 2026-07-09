#nullable enable

namespace GoDo;

/// <summary>Procedure 进入和退出时可用的框架上下文。</summary>
public sealed class ProcedureContext
{
    /// <summary>获取已注册的长期框架服务。</summary>
    public TService GetService<TService>() where TService : class => Services.Get<TService>();

    /// <summary>尝试获取已注册的长期框架服务。</summary>
    public bool TryGetService<TService>(out TService? service) where TService : class =>
        Services.TryGet(out service);
}
