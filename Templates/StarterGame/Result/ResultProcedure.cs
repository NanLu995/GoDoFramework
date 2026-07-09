using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>结算流程：停止 BGM、打开结算 View，并响应结算界面操作。</summary>
public sealed class ResultProcedure : IProcedure
{
    private readonly EventScope _events = new();
    private Control? _view;
    private ProcedureContext? _context;

    public string Name => "Result";

    public Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        context.GetService<IAudioService>().StopBgm();
        _view = context.GetService<IUiService>().Open(StarterGameKeys.ResultView, UiLayer.View);
        _events
            .On<StarterRetrySelectedEvent>(OnRetrySelected)
            .On<StarterReturnToMenuSelectedEvent>(OnReturnToMenuSelected);
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _events.Dispose();
        _context = null;
        CloseView(context);
        return Task.CompletedTask;
    }

    private void OnRetrySelected(StarterRetrySelectedEvent evt)
    {
        RequestChange(new GameplayProcedure());
    }

    private void OnReturnToMenuSelected(StarterReturnToMenuSelectedEvent evt)
    {
        RequestChange(new MainMenuProcedure());
    }

    private void RequestChange(IProcedure next)
    {
        if (_context == null)
            throw new InvalidOperationException("ResultProcedure 尚未进入，不能请求切换。");

        _context.RequestChange(next);
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
            ErrorHub.Report(exception, Name, "关闭结算 View 失败");
        }
        finally
        {
            _view = null;
        }
    }
}
