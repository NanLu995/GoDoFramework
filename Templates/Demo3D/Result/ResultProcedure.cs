using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>完成收集后显示结算页面，并处理重新开始。</summary>
public sealed class ResultProcedure : IProcedure
{
    private readonly EventScope _events = new();
    private Control? _view;
    private ProcedureContext? _context;

    public string Name => "Result";

    public Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        context.GetService<IInputService>().SetBaseContext(Demo3DInput.Result);
        _view = context.GetService<IUiService>().Open(Demo3DKeys.ResultView, UiLayer.View);
        _events.On<RetrySelectedEvent>(OnRetrySelected);
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _events.Dispose();
        _context = null;
        CloseView(context);
        return Task.CompletedTask;
    }

    private void OnRetrySelected(RetrySelectedEvent evt)
    {
        if (_context == null)
            throw new InvalidOperationException("ResultProcedure 尚未进入，不能重新开始。");

        _context.RequestChange<GameplayProcedure>();
    }

    private void CloseView(ProcedureContext context)
    {
        if (!GodotObject.IsInstanceValid(_view))
            return;

        try
        {
            context.GetService<IUiService>().Close(_view!);
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, Name, "关闭 ResultView 失败");
        }
        finally
        {
            _view = null;
        }
    }
}
