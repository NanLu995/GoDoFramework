using System;
using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>
/// 管理顶层游戏流程阶段的串行切换。
/// <para>本服务只提供流程生命周期机制，不内置任何具体业务流程。</para>
/// </summary>
public sealed class ProcedureService : IProcedureService
{
    private readonly ProcedureContext _context;
    private IProcedure? _requestedProcedure;
    private bool _isProcessingRequest;
    private int _lifecycleVersion;
#if DEBUG
    private string? _debugPreviousName;
    private string? _debugTargetName;
    private ProcedureDebugPhase _debugPhase;
    private string? _debugLastSucceededName;
    private string? _debugLastFailure;
#endif

    /// <summary>创建顶层流程服务。</summary>
    public ProcedureService()
    {
        _context = new ProcedureContext(RequestChange);
    }

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
            _debugLastFailure = $"拒绝 {GetDebugName(next)}：已有流程切换正在执行";
#endif
            throw new ProcedureChangeException(
                GetProcedureName(next),
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
        Current = null;
        _requestedProcedure = null;
        IsChanging = false;
        _isProcessingRequest = false;
#if DEBUG
        _debugPreviousName = null;
        _debugTargetName = null;
        _debugPhase = ProcedureDebugPhase.Idle;
        _debugLastSucceededName = null;
        _debugLastFailure = null;
#endif
    }

    private void RequestChange(IProcedure next)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(next);

        _requestedProcedure = next;
        if (!IsChanging && !_isProcessingRequest)
            _ = ProcessRequestedChangeAsync(_lifecycleVersion);
    }

    private async Task ProcessRequestedChangeAsync(int lifecycleVersion)
    {
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
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "Procedure", "处理流程切换请求失败");
        }
        finally
        {
            if (lifecycleVersion == _lifecycleVersion)
                _isProcessingRequest = false;
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
#if DEBUG
        _debugPreviousName = GetDebugName(previous);
        _debugTargetName = GetDebugName(next);
        _debugPhase = previous is null ? ProcedureDebugPhase.Entering : ProcedureDebugPhase.Exiting;
#endif
        if (previous != null)
        {
            try
            {
                await ExitAsync(previous, lifecycleVersion);
            }
            catch (Exception exception)
            {
#if DEBUG
                RecordDebugFailure("退出", previous, exception);
#else
                _ = exception;
#endif
                throw;
            }
        }

        Current = null;
#if DEBUG
        _debugPhase = ProcedureDebugPhase.Entering;
#endif
        try
        {
            await EnterAsync(next, lifecycleVersion);
        }
        catch (Exception exception)
        {
#if DEBUG
            RecordDebugFailure("进入", next, exception);
#else
            _ = exception;
#endif
            throw;
        }
        Current = next;
#if DEBUG
        _debugPhase = ProcedureDebugPhase.Idle;
        _debugLastSucceededName = GetDebugName(next);
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
            _debugLastFailure);
    }

    private void RecordDebugFailure(string operation, IProcedure procedure, Exception exception)
    {
        _debugPhase = ProcedureDebugPhase.Idle;
        string detail = exception.Message;
        if (detail.Length > 256)
            detail = detail[..256];
        _debugLastFailure = $"{operation} {GetDebugName(procedure)}：{detail}";
    }

    private static string? GetDebugName(IProcedure? procedure)
    {
        return procedure is null ? null : GetProcedureName(procedure);
    }
#endif

    private async Task ExitAsync(IProcedure procedure, int lifecycleVersion)
    {
        try
        {
            await procedure.ExitAsync(_context);
            VerifyLifecycleVersion(lifecycleVersion, procedure);
            MainThreadGuard.VerifyAccess();
        }
        catch (Exception exception) when (exception is not ProcedureChangeException)
        {
            string procedureName = GetProcedureName(procedure);
            throw new ProcedureChangeException(
                procedureName,
                $"流程退出失败，已取消切换: {procedureName}",
                exception);
        }
    }

    private async Task EnterAsync(IProcedure procedure, int lifecycleVersion)
    {
        try
        {
            await procedure.EnterAsync(_context);
            VerifyLifecycleVersion(lifecycleVersion, procedure);
            MainThreadGuard.VerifyAccess();
        }
        catch (Exception exception) when (exception is not ProcedureChangeException)
        {
            string procedureName = GetProcedureName(procedure);
            throw new ProcedureChangeException(
                procedureName,
                $"流程进入失败，当前流程为空: {procedureName}",
                exception);
        }
    }

    private void VerifyLifecycleVersion(int lifecycleVersion, IProcedure procedure)
    {
        if (lifecycleVersion == _lifecycleVersion)
            return;

        string procedureName = GetProcedureName(procedure);
        throw new ProcedureChangeException(
            procedureName,
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
