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
            throw new ProcedureChangeException(next.Name, "已有流程切换正在执行，不能重复发起请求。");
        }

        await ChangeSequenceAsync(next);
    }

    /// <inheritdoc />
    public Task ChangeAsync<TProcedure>() where TProcedure : IProcedure, new() =>
        ChangeAsync(new TProcedure());

    internal void Shutdown()
    {
        MainThreadGuard.VerifyAccess();
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
            _ = ProcessRequestedChangeAsync();
    }

    private async Task ProcessRequestedChangeAsync()
    {
        _isProcessingRequest = true;
        try
        {
            while (_requestedProcedure != null)
            {
                IProcedure next = _requestedProcedure;
                _requestedProcedure = null;
                await ChangeAsync(next);
            }
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, "Procedure", "处理流程切换请求失败");
        }
        finally
        {
            _isProcessingRequest = false;
        }
    }

    private async Task ChangeSequenceAsync(IProcedure next)
    {
        IsChanging = true;
        try
        {
            await ChangeSingleAsync(next);
            while (_requestedProcedure != null)
            {
                IProcedure requested = _requestedProcedure;
                _requestedProcedure = null;
                await ChangeSingleAsync(requested);
            }
        }
        finally
        {
            IsChanging = false;
        }
    }

    private async Task ChangeSingleAsync(IProcedure next)
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
                await ExitAsync(previous);
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
            await EnterAsync(next);
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
        if (procedure is null)
            return null;
        try
        {
            return procedure.Name;
        }
        catch
        {
            return "<Name 读取失败>";
        }
    }
#endif

    private async Task ExitAsync(IProcedure procedure)
    {
        try
        {
            await procedure.ExitAsync(_context);
            MainThreadGuard.VerifyAccess();
        }
        catch (Exception exception) when (exception is not ProcedureChangeException)
        {
            throw new ProcedureChangeException(
                procedure.Name,
                $"流程退出失败，已取消切换: {procedure.Name}",
                exception);
        }
    }

    private async Task EnterAsync(IProcedure procedure)
    {
        try
        {
            await procedure.EnterAsync(_context);
            MainThreadGuard.VerifyAccess();
        }
        catch (Exception exception) when (exception is not ProcedureChangeException)
        {
            throw new ProcedureChangeException(
                procedure.Name,
                $"流程进入失败，当前流程为空: {procedure.Name}",
                exception);
        }
    }
}
