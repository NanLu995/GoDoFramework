using System;
using System.Threading.Tasks;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 管理主菜单主场景与主菜单 View 的顶层流程。
/// <para>后续阶段会在此处理开始游戏、设置和退出等玩家意图；本阶段只建立场景与 UI 生命周期边界。</para>
/// </summary>
internal sealed class MainMenuProcedure : IProcedure
{
    private ProcedureContext? _context;

    public string Name => "MainMenu";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        await context.GetService<ISceneService>().ChangeAsync(StarterKeys.MainMenuScene);
        IUiService ui = context.GetService<IUiService>();
        MainMenuView view = ui.Open<MainMenuView>(StarterKeys.MainMenuView);
        context.RegisterCleanup(() => ui.TryClose(view));
        context.Events.On<StartGameSelectedEvent>(OnStartGameSelected);
        context.Events.On<SettingsSelectedEvent>(OnSettingsSelected);
        context.Events.On<SettingsCloseSelectedEvent>(OnSettingsCloseSelected);
        context.Events.On<BackSelectedEvent>(OnBackSelected);
        SetMenuContext(context);
        StarterLog.MainMenu.Info("Main menu entered.");
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

        _context.RequestChange<GameplayProcedure>();
    }

    private void OnSettingsSelected(SettingsSelectedEvent _)
    {
        if (_context == null)
            throw new InvalidOperationException("MainMenuProcedure 尚未进入，不能打开设置。");

        IUiService ui = _context.GetService<IUiService>();
        if (!ui.IsOpen(StarterKeys.SettingsView))
            ui.Open<SettingsView>(StarterKeys.SettingsView, view => view.Refresh());
    }

    private void OnSettingsCloseSelected(SettingsCloseSelectedEvent _)
    {
        _context?.GetService<IUiService>().TryGoBack();
    }

    private void OnBackSelected(BackSelectedEvent _)
    {
        if (_context == null)
            return;

        IUiService ui = _context.GetService<IUiService>();
        if (ui.IsOpen(StarterKeys.SettingsView))
            ui.TryGoBack();
    }

    private static void SetMenuContext(ProcedureContext context)
    {
        if (StarterInput.IsReady(context))
            context.GetService<IInputService>().SetBaseContext(StarterInput.Menu);
    }
}
