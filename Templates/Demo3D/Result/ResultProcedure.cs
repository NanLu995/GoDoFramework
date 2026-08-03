using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>完成收集后显示结算页面，并处理重新开始。</summary>
public sealed class ResultProcedure : IProcedure
{
    private ProcedureContext? _context;

    public string Name => "Result";

    public Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        SceneTree tree = GetSceneTree();
        tree.Paused = true;
        context.RegisterCleanup(() => tree.Paused = false);
        context.GetService<IInputService>().SetBaseContext(Demo3DInput.Result);
        IUiService ui = context.GetService<IUiService>();
        UiScope<Control> view = ui.OpenScoped<Control>(
            Demo3DKeys.ResultView,
            UiLayer.View);
        context.RegisterCleanup(view);
        context.Events.On<RetrySelectedEvent>(OnRetrySelected);
        context.Events.On<ReturnToMenuSelectedEvent>(OnReturnToMenuSelected);
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _context = null;
        return Task.CompletedTask;
    }

    private void OnRetrySelected(RetrySelectedEvent evt)
    {
        if (_context == null)
            throw new InvalidOperationException("ResultProcedure 尚未进入，不能重新开始。");

        _context.TryRequestChange<GameplayProcedure>();
    }

    private void OnReturnToMenuSelected(ReturnToMenuSelectedEvent _)
    {
        if (_context == null)
            throw new InvalidOperationException("ResultProcedure 尚未进入，不能返回主菜单。");

        _context.TryRequestChange<MainMenuProcedure>();
    }

    private static SceneTree GetSceneTree() =>
        Engine.GetMainLoop() as SceneTree ??
        throw new InvalidOperationException("Demo3D 当前没有可用的 SceneTree。");
}
