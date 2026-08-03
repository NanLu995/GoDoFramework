using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>
/// 管理顶层游戏流程阶段的串行切换。
/// <para>本服务只提供流程生命周期机制，不内置任何具体业务流程。</para>
/// </summary>
public sealed class ProcedureService : IProcedureService
{
    private IProcedure? _requestedProcedure;
    private ProcedureContext? _currentContext;
    private ProcedureContext? _enteringContext;
    private bool _isProcessingRequest;
    private int _lifecycleVersion;
#if DEBUG
    private string? _debugPreviousName;
    private string? _debugTargetName;
    private ProcedureDebugPhase _debugPhase;
    private string? _debugLastSucceededName;
    private ProcedureDebugPhase _debugLastPhase;
    private ProcedureDebugResult _debugLastResult;
    private string? _debugLastFailure;
    private string? _debugLastRejectedRequestName;
    private string? _debugLastRequestRejection;
    private ulong _debugLastDurationMilliseconds;
    private ulong _debugStartedTicks;
#endif

    /// <summary>创建顶层流程服务。</summary>
    public ProcedureService()
    {
    }

    /// <inheritdoc />
    public event Action<ProcedureChangeException>? RequestedChangeFailed;

    /// <inheritdoc />
    public IProcedure? Current { get; private set; }

    /// <inheritdoc />
    public bool IsChanging { get; private set; }

    /// <inheritdoc />
    public async Task ChangeAsync(IProcedure next)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(next);

        if (IsChanging)
        {
#if DEBUG
            RecordDebugRejection(next);
#endif
            throw new ProcedureChangeException(
                GetProcedureName(next),
                ProcedureChangePhase.Requesting,
                "已有流程切换正在执行，不能重复发起请求。");
        }

        int lifecycleVersion = _lifecycleVersion;
        await ChangeSequenceAsync(next, lifecycleVersion);
    }

    /// <inheritdoc />
    public Task ChangeAsync<TProcedure>() where TProcedure : IProcedure, new()
    {
        MainThreadGuard.VerifyAccess();
        return ChangeAsync(new TProcedure());
    }

    internal void Shutdown()
    {
        MainThreadGuard.VerifyAccess();
        _lifecycleVersion++;
        List<Exception>? exceptions = null;
        TryInvalidateForShutdown(_currentContext, ref exceptions);
        if (!ReferenceEquals(_enteringContext, _currentContext))
            TryInvalidateForShutdown(_enteringContext, ref exceptions);
        Current = null;
        _currentContext = null;
        _enteringContext = null;
        _requestedProcedure = null;
        IsChanging = false;
        _isProcessingRequest = false;
        RequestedChangeFailed = null;
#if DEBUG
        _debugPhase = ProcedureDebugPhase.Idle;
#endif

        if (exceptions is null)
            return;
        if (exceptions.Count == 1)
            throw exceptions[0];

        throw new AggregateException("多个 Procedure Context 关闭失败。", exceptions);
    }

    private static void TryInvalidateForShutdown(
        ProcedureContext? context,
        ref List<Exception>? exceptions)
    {
        if (context is null)
            return;

        try
        {
            context.InvalidateForShutdown();
        }
        catch (Exception exception)
        {
            (exceptions ??= new List<Exception>()).Add(exception);
        }
    }

    private bool TryRequestChange(IProcedure next)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(next);

        if (_requestedProcedure is not null)
        {
            RecordRequestRejection(
                next,
                $"已有待处理的流程切换请求: {GetProcedureName(_requestedProcedure)}");
            return false;
        }

        _requestedProcedure = next;
        if (!IsChanging && !_isProcessingRequest)
            _ = ProcessRequestedChangeAsync(_lifecycleVersion);
        return true;
    }

    private void RecordRequestRejection(IProcedure procedure, string reason)
    {
#if DEBUG
        _debugLastRejectedRequestName = GetDebugName(procedure);
        _debugLastRequestRejection = reason;
#else
        _ = procedure;
        _ = reason;
#endif
    }

    private async Task ProcessRequestedChangeAsync(int lifecycleVersion)
    {
        ProcedureChangeException? requestedFailure = null;
        _isProcessingRequest = true;
        try
        {
            while (lifecycleVersion == _lifecycleVersion && _requestedProcedure != null)
            {
                IProcedure next = _requestedProcedure;
                _requestedProcedure = null;
                await ChangeAsync(next);
            }
        }
        catch (ProcedureChangeException exception)
            when (lifecycleVersion != _lifecycleVersion &&
                  exception.InnerException is OperationCanceledException)
        {
            // Runtime 关闭导致的预期取消不作为业务流程失败上报。
        }
        catch (ProcedureChangeException exception)
        {
            requestedFailure = exception;
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "Procedure", "处理流程切换请求失败");
        }
        finally
        {
            if (lifecycleVersion == _lifecycleVersion)
                _isProcessingRequest = false;
        }

        if (requestedFailure is null)
            return;

        NotifyRequestedChangeFailed(requestedFailure);
        ErrorHub.Report(requestedFailure, "Procedure", "处理流程切换请求失败");
    }

    private void NotifyRequestedChangeFailed(ProcedureChangeException exception)
    {
        Action<ProcedureChangeException>? handlers = RequestedChangeFailed;
        if (handlers is null)
            return;

        foreach (Action<ProcedureChangeException> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(exception);
            }
            catch (Exception handlerException)
            {
                ErrorHub.Report(
                    handlerException,
                    "Procedure",
                    "处理流程切换失败通知时订阅者执行失败");
            }
        }
    }

    private async Task ChangeSequenceAsync(IProcedure next, int lifecycleVersion)
    {
        IsChanging = true;
        try
        {
            await ChangeSingleAsync(next, lifecycleVersion);
            while (lifecycleVersion == _lifecycleVersion && _requestedProcedure != null)
            {
                IProcedure requested = _requestedProcedure;
                _requestedProcedure = null;
                await ChangeSingleAsync(requested, lifecycleVersion);
            }
        }
        catch
        {
            if (lifecycleVersion == _lifecycleVersion)
                _requestedProcedure = null;
            throw;
        }
        finally
        {
            if (lifecycleVersion == _lifecycleVersion)
                IsChanging = false;
        }
    }

    private async Task ChangeSingleAsync(IProcedure next, int lifecycleVersion)
    {
        IProcedure? previous = Current;
        ProcedureContext? previousContext = _currentContext;
#if DEBUG
        _debugStartedTicks = Godot.Time.GetTicksMsec();
        _debugPreviousName = GetDebugName(previous);
        _debugTargetName = GetDebugName(next);
        _debugPhase = previous is null ? ProcedureDebugPhase.Entering : ProcedureDebugPhase.Exiting;
#endif
        if (previous != null)
        {
            if (previousContext is null)
                throw new InvalidOperationException("当前 Procedure 缺少对应的激活 Context。");

            try
            {
                await ExitAsync(previous, previousContext, lifecycleVersion);
            }
            catch (Exception exception)
            {
#if DEBUG
                RecordDebugFailure(ProcedureDebugPhase.Exiting, "退出", previous, exception);
#else
                _ = exception;
#endif
                throw;
            }

            Current = null;
            _currentContext = null;
            try
            {
                previousContext.Cleanup();
            }
            catch (Exception exception)
            {
#if DEBUG
                RecordDebugFailure(
                    ProcedureDebugPhase.Exiting,
                    "退出清理",
                    previous,
                    exception);
#endif
                string procedureName = GetProcedureName(previous);
                throw new ProcedureChangeException(
                    procedureName,
                    ProcedureChangePhase.Cleanup,
                    $"流程退出后的激活清理失败，当前流程为空: {procedureName}",
                    exception);
            }
        }

        Current = null;
        _currentContext = null;
#if DEBUG
        _debugPhase = ProcedureDebugPhase.Entering;
#endif
        var nextContext = new ProcedureContext(TryRequestChange, RecordRequestRejection);
        _enteringContext = nextContext;
        try
        {
            await EnterAsync(next, nextContext, lifecycleVersion);
        }
        catch (Exception exception)
        {
            Exception failure = exception;
            try
            {
                nextContext.Cleanup();
            }
            catch (Exception cleanupException)
            {
                string procedureName = GetProcedureName(next);
                failure = new ProcedureChangeException(
                    procedureName,
                    ProcedureChangePhase.Entering,
                    $"流程进入失败，且激活清理也失败，当前流程为空: {procedureName}",
                    new AggregateException(
                        "Procedure Enter 与激活清理均失败。",
                        exception,
                        cleanupException));
            }
#if DEBUG
            RecordDebugFailure(ProcedureDebugPhase.Entering, "进入", next, failure);
#else
            _ = failure;
#endif
            if (ReferenceEquals(failure, exception))
                throw;
            throw failure;
        }
        finally
        {
            if (ReferenceEquals(_enteringContext, nextContext))
                _enteringContext = null;
        }
        Current = next;
        _currentContext = nextContext;
#if DEBUG
        RecordDebugSuccess(next);
#endif
    }

#if DEBUG
    internal ProcedureDebugSnapshot GetDebugSnapshot()
    {
        MainThreadGuard.VerifyAccess();
        return new ProcedureDebugSnapshot(
            GetDebugName(Current),
            _debugPreviousName,
            _debugTargetName,
            GetDebugName(_requestedProcedure),
            _debugPhase,
            _debugLastSucceededName,
            _debugLastPhase,
            _debugLastResult,
            _debugLastFailure,
            _debugLastRejectedRequestName,
            _debugLastRequestRejection,
            _debugLastDurationMilliseconds,
            (_enteringContext ?? _currentContext)?.IsActive == true,
            (_enteringContext ?? _currentContext)?.CleanupCount ?? 0);
    }

    private void RecordDebugSuccess(IProcedure procedure)
    {
        _debugPhase = ProcedureDebugPhase.Idle;
        _debugLastSucceededName = GetDebugName(procedure);
        _debugLastPhase = ProcedureDebugPhase.Entering;
        _debugLastResult = ProcedureDebugResult.Succeeded;
        _debugLastFailure = null;
        _debugLastDurationMilliseconds = GetDebugDurationMilliseconds();
    }

    private void RecordDebugRejection(IProcedure procedure)
    {
        _debugLastPhase = _debugPhase;
        _debugLastResult = ProcedureDebugResult.Rejected;
        _debugLastFailure = $"拒绝 {GetDebugName(procedure)}：已有流程切换正在执行";
        _debugLastDurationMilliseconds = GetDebugDurationMilliseconds();
    }

    private void RecordDebugFailure(
        ProcedureDebugPhase phase,
        string operation,
        IProcedure procedure,
        Exception exception)
    {
        _debugPhase = ProcedureDebugPhase.Idle;
        _debugLastPhase = phase;
        _debugLastResult = exception.InnerException is OperationCanceledException
            ? ProcedureDebugResult.LifecycleCanceled
            : ProcedureDebugResult.Failed;
        string detail = exception.Message;
        if (detail.Length > 256)
            detail = detail[..256];
        _debugLastFailure = $"{operation} {GetDebugName(procedure)}：{detail}";
        _debugLastDurationMilliseconds = GetDebugDurationMilliseconds();
    }

    private ulong GetDebugDurationMilliseconds() =>
        Godot.Time.GetTicksMsec() - _debugStartedTicks;

    private static string? GetDebugName(IProcedure? procedure)
    {
        return procedure is null ? null : GetProcedureName(procedure);
    }
#endif

    private async Task ExitAsync(
        IProcedure procedure,
        ProcedureContext context,
        int lifecycleVersion)
    {
        try
        {
            await procedure.ExitAsync(context);
            VerifyLifecycleVersion(
                lifecycleVersion,
                procedure,
                ProcedureChangePhase.Exiting);
            MainThreadGuard.VerifyAccess();
        }
        catch (Exception exception) when (exception is not ProcedureChangeException)
        {
            string procedureName = GetProcedureName(procedure);
            throw new ProcedureChangeException(
                procedureName,
                ProcedureChangePhase.Exiting,
                $"流程退出失败，已取消切换: {procedureName}",
                exception);
        }
    }

    private async Task EnterAsync(
        IProcedure procedure,
        ProcedureContext context,
        int lifecycleVersion)
    {
        try
        {
            await procedure.EnterAsync(context);
            VerifyLifecycleVersion(
                lifecycleVersion,
                procedure,
                ProcedureChangePhase.Entering);
            MainThreadGuard.VerifyAccess();
        }
        catch (Exception exception) when (exception is not ProcedureChangeException)
        {
            string procedureName = GetProcedureName(procedure);
            throw new ProcedureChangeException(
                procedureName,
                ProcedureChangePhase.Entering,
                $"流程进入失败，当前流程为空: {procedureName}",
                exception);
        }
    }

    private void VerifyLifecycleVersion(
        int lifecycleVersion,
        IProcedure procedure,
        ProcedureChangePhase phase)
    {
        if (lifecycleVersion == _lifecycleVersion)
            return;

        string procedureName = GetProcedureName(procedure);
        throw new ProcedureChangeException(
            procedureName,
            phase,
            $"ProcedureService 生命周期已变化，流程切换已取消: {procedureName}",
            new OperationCanceledException("ProcedureService 已关闭。"));
    }

    private static string GetProcedureName(IProcedure procedure)
    {
        try
        {
            string name = procedure.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch
        {
        }

        return procedure.GetType().Name;
    }
}
