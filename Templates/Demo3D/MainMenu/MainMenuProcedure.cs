using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>加载主菜单场景并响应开始游戏操作。</summary>
public sealed class MainMenuProcedure : IProcedure
{
    private ProcedureContext? _context;

    public string Name => "MainMenu";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        IUiService ui = context.GetService<IUiService>();
        using (UiScope<LoadingOverlay> loading = ui.OpenScoped<LoadingOverlay>(
            Demo3DKeys.LoadingOverlay,
            UiLayer.Overlay))
        {
            await context.GetService<ISceneService>().ChangeAsync(
                Demo3DKeys.MainMenuScene,
                loading.View.SetProgress,
                context.LifetimeToken);
        }

        UiScope<Control> view = ui.OpenScoped<Control>(
            Demo3DKeys.MainMenuView,
            UiLayer.View);
        context.RegisterCleanup(view);
        context.Events.On<StartGameSelectedEvent>(OnStartGameSelected);
        context.GetService<IInputService>().SetBaseContext(Demo3DInput.Menu);
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _context = null;
        return Task.CompletedTask;
    }

    private void OnStartGameSelected(StartGameSelectedEvent _)
    {
        if (_context == null)
            throw new InvalidOperationException("MainMenuProcedure 尚未进入，不能开始游戏。");

        _context.TryRequestChange<GameplayProcedure>();
    }
}
