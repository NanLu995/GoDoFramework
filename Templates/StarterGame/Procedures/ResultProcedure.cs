using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Templates.StarterGame;

/// <summary>结算流程：停止 BGM 并打开结算 View。</summary>
public sealed class ResultProcedure : IProcedure
{
    private Control? _view;

    public string Name => "Result";

    public Task EnterAsync(ProcedureContext context)
    {
        context.GetService<IAudioService>().StopBgm();
        _view = context.GetService<IUiService>().Open(StarterGameKeys.ResultView, UiLayer.View);
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context)
    {
        CloseView(context);
        return Task.CompletedTask;
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
