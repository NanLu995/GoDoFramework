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
            throw new ProcedureChangeException(next.Name, "已有流程切换正在执行，不能重复发起请求。");

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
        if (previous != null)
            await ExitAsync(previous);

        Current = null;
        await EnterAsync(next);
        Current = next;
    }

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
