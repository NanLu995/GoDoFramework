using System;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace GoDo;

/// <summary>
/// 单次 Procedure 激活在进入和退出期间使用的框架上下文。
/// <para>流程退出、进入失败或 ProcedureService 关闭后，该实例不再允许登记资源或请求流程切换。</para>
/// </summary>
public sealed class ProcedureContext
{
    private readonly Func<IProcedure, bool> _tryRequestChange;
    private readonly Action<IProcedure, string> _recordRequestRejection;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private List<Action>? _cleanupActions;
    private EventScope? _events;
    private bool _hasRequestedChange;
    private bool _isActive = true;

    internal ProcedureContext(
        Func<IProcedure, bool> tryRequestChange,
        Action<IProcedure, string> recordRequestRejection)
    {
        _tryRequestChange = tryRequestChange;
        _recordRequestRejection = recordRequestRejection;
        _lifetimeToken = _lifetimeCancellation.Token;
    }

    /// <summary>当前 Context 是否仍属于正在进入、已进入或正在退出的 Procedure 激活。</summary>
    public bool IsActive
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            return _isActive;
        }
    }

    /// <summary>
    /// 获取在当前 Procedure 激活结束时取消的生命周期令牌。
    /// <para>
    /// 正常退出清理、Enter 失败清理或 ProcedureService 关闭时会在 Godot 主线程取消；
    /// Exit 失败并保留当前激活时不会取消。取消发生后仍可读取令牌状态，但不应再注册新的工作。
    /// </para>
    /// </summary>
    public CancellationToken LifetimeToken => _lifetimeToken;

    /// <summary>
    /// 获取随当前 Procedure 激活自动释放的事件作用域。
    /// <para>Enter 失败、正常 Exit 完成或 ProcedureService 关闭时会自动注销其中的监听。</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">当前 Procedure 激活已经结束。</exception>
    public EventScope Events
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            VerifyActive();
            return _events ??= new EventScope();
        }
    }

    /// <summary>获取已注册的长期框架服务。</summary>
    public TService GetService<TService>() where TService : class => Services.Get<TService>();

    /// <summary>尝试获取已注册的长期框架服务。</summary>
    public bool TryGetService<TService>(out TService? service) where TService : class =>
        Services.TryGet(out service);

    /// <summary>
    /// 登记当前 Procedure 激活结束时执行的同步清理。
    /// <para>
    /// 清理按登记顺序的逆序执行；正常退出时在 <see cref="IProcedure.ExitAsync"/> 成功后执行，
    /// Enter 失败时也会执行。Exit 失败和 ProcedureService 关闭不会执行业务清理。
    /// </para>
    /// </summary>
    /// <param name="cleanup">必须在 Godot 主线程快速完成的清理动作。</param>
    /// <exception cref="ArgumentNullException"><paramref name="cleanup"/> 为 null。</exception>
    /// <exception cref="InvalidOperationException">当前 Procedure 激活已经结束。</exception>
    public void RegisterCleanup(Action cleanup)
    {
        MainThreadGuard.VerifyAccess();
        VerifyActive();
        ArgumentNullException.ThrowIfNull(cleanup);
        (_cleanupActions ??= new List<Action>(4)).Add(cleanup);
    }

    /// <summary>
    /// 登记由当前 Procedure 激活拥有、并在激活结束时同步释放的资源。
    /// <para>
    /// 资源的 <see cref="IDisposable.Dispose"/> 按注册顺序的逆序执行，并沿用
    /// <see cref="RegisterCleanup(Action)"/> 的退出、进入失败和异常聚合语义。
    /// 调用方不应重复登记同一资源，并应确保释放操作可以在 Godot 主线程快速完成。
    /// </para>
    /// </summary>
    /// <param name="disposable">随当前 Procedure 激活结束而释放的资源。</param>
    /// <exception cref="ArgumentNullException"><paramref name="disposable"/> 为 null。</exception>
    /// <exception cref="InvalidOperationException">当前 Procedure 激活已经结束。</exception>
    public void RegisterCleanup(IDisposable disposable)
    {
        MainThreadGuard.VerifyAccess();
        VerifyActive();
        ArgumentNullException.ThrowIfNull(disposable);
        RegisterCleanup(disposable.Dispose);
    }

    /// <summary>
    /// 请求在当前流程切换安全结束后进入目标流程。
    /// <para>
    /// 本方法不会递归执行切换；ProcedureService 会串行处理请求。每个 Procedure 激活周期只接受第一次请求，
    /// 后续请求不会覆盖已经接受的目标；需要判断是否登记成功时使用 <see cref="TryRequestChange(IProcedure)"/>。
    /// </para>
    /// </summary>
    public void RequestChange(IProcedure next)
    {
        _ = TryRequestChange(next);
    }

    /// <summary>在验证 Godot 主线程后创建并请求进入无参构造的目标流程。</summary>
    public void RequestChange<TProcedure>() where TProcedure : IProcedure, new()
    {
        MainThreadGuard.VerifyAccess();
        VerifyActive();
        RequestChange(new TProcedure());
    }

    /// <summary>
    /// 尝试登记在当前流程切换安全结束后进入的目标流程。
    /// <para>
    /// 每个 Procedure 激活周期只接受第一次请求；后续请求返回 false，且不会覆盖已经接受的目标。
    /// 请求不会递归执行，ProcedureService 会在当前 Enter、Exit 或调用边界安全结束后串行处理。
    /// </para>
    /// </summary>
    /// <param name="next">要进入的目标流程。</param>
    /// <returns>成功登记返回 true；当前激活已经登记请求或服务已有待处理请求时返回 false。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> 为 null。</exception>
    /// <exception cref="InvalidOperationException">当前 Procedure 激活已经结束。</exception>
    public bool TryRequestChange(IProcedure next)
    {
        MainThreadGuard.VerifyAccess();
        VerifyActive();
        ArgumentNullException.ThrowIfNull(next);

        if (_hasRequestedChange)
        {
            _recordRequestRejection(next, "当前 Procedure 激活已经登记流程切换请求");
            return false;
        }

        _hasRequestedChange = true;
        if (_tryRequestChange(next))
            return true;

        _hasRequestedChange = false;
        return false;
    }

    /// <summary>创建无参目标流程，并尝试登记在当前流程切换安全结束后进入。</summary>
    /// <typeparam name="TProcedure">具有无参构造函数的目标流程类型。</typeparam>
    /// <returns>成功登记返回 true；当前激活已经登记请求或服务已有待处理请求时返回 false。</returns>
    /// <exception cref="InvalidOperationException">当前 Procedure 激活已经结束。</exception>
    public bool TryRequestChange<TProcedure>() where TProcedure : IProcedure, new()
    {
        MainThreadGuard.VerifyAccess();
        VerifyActive();
        return TryRequestChange(new TProcedure());
    }

#if DEBUG
    internal int CleanupCount => (_cleanupActions?.Count ?? 0) + (_events is null ? 0 : 1);
#endif

    internal void Cleanup()
    {
        MainThreadGuard.VerifyAccess();
        if (!_isActive)
            return;

        _isActive = false;
        List<Exception>? exceptions = null;
        CancelLifetime(ref exceptions);
        if (_events is not null)
        {
            try
            {
                _events.Dispose();
            }
            catch (Exception exception)
            {
                (exceptions ??= new List<Exception>()).Add(exception);
            }
            finally
            {
                _events = null;
            }
        }

        if (_cleanupActions is not null)
        {
            for (int index = _cleanupActions.Count - 1; index >= 0; index--)
            {
                try
                {
                    _cleanupActions[index]();
                }
                catch (Exception exception)
                {
                    (exceptions ??= new List<Exception>()).Add(exception);
                }
            }

            _cleanupActions.Clear();
            _cleanupActions = null;
        }

        ThrowCleanupExceptions(exceptions);
    }

    internal void InvalidateForShutdown()
    {
        MainThreadGuard.VerifyAccess();
        if (!_isActive)
            return;

        _isActive = false;
        List<Exception>? exceptions = null;
        CancelLifetime(ref exceptions);
        if (_events is not null)
        {
            try
            {
                _events.Dispose();
            }
            catch (Exception exception)
            {
                (exceptions ??= new List<Exception>()).Add(exception);
            }
            finally
            {
                _events = null;
            }
        }

        _cleanupActions?.Clear();
        _cleanupActions = null;

        ThrowCleanupExceptions(exceptions);
    }

    private void CancelLifetime(ref List<Exception>? exceptions)
    {
        try
        {
            _lifetimeCancellation.Cancel();
        }
        catch (Exception exception)
        {
            (exceptions ??= new List<Exception>()).Add(exception);
        }
        finally
        {
            _lifetimeCancellation.Dispose();
        }
    }

    private static void ThrowCleanupExceptions(List<Exception>? exceptions)
    {
        if (exceptions is null)
            return;
        if (exceptions.Count == 1)
            throw exceptions[0];

        throw new AggregateException("多个 Procedure 激活清理失败。", exceptions);
    }

    private void VerifyActive()
    {
        if (!_isActive)
            throw new InvalidOperationException("ProcedureContext 所属的流程激活已经结束。");
    }
}
