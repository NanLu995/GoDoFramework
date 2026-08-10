using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
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
    private UiScope<MainMenuView>? _mainMenu;
    private SettingsView? _settingsView;
    private UiScope<LoadingOverlay>? _settingsLoading;
    private CancellationTokenSource? _settingsCancellation;
    private Control? _settingsReturnFocus;
    private bool _isOpeningSettings;

    public string Name => "MainMenu";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        await context.GetService<ISceneService>().ChangeAsync(
            StarterKeys.MainMenuScene,
            onProgress: null,
            cancellationToken: context.LifetimeToken);
        IUiService ui = context.GetService<IUiService>();
        _mainMenu = ui.OpenScoped<MainMenuView>(StarterKeys.MainMenuView);
        context.RegisterCleanup(CleanupMainMenu);
        context.RegisterCleanup(() => CleanupSettingsView(ui));
        context.RegisterCleanup(() => CloseSettingsLoading(restoreFocus: false));
        context.RegisterCleanup(CancelSettingsOpen);
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

    private async void OnStartGameSelected(StartGameSelectedEvent _)
    {
        ProcedureContext? context = _context;
        UiScope<MainMenuView>? mainMenu = _mainMenu;
        if (context == null)
            throw new InvalidOperationException("MainMenuProcedure 尚未进入，不能开始游戏。");
        if (mainMenu == null)
            return;

        await UiTransitionCoordinator.TryCloseAsync(
            mainMenu.View,
            () => context.RequestChange<GameplayProcedure>());
    }

    private async void OnSettingsSelected(SettingsSelectedEvent _)
    {
        ProcedureContext? context = _context;
        if (context == null || _isOpeningSettings || GodotObject.IsInstanceValid(_settingsView))
            return;

        IUiService ui = context.GetService<IUiService>();
        _isOpeningSettings = true;
        try
        {
            _settingsReturnFocus = _mainMenu?.View.GetViewport().GuiGetFocusOwner();
            if (!GodotObject.IsInstanceValid(_settingsReturnFocus) ||
                !_mainMenu!.View.IsAncestorOf(_settingsReturnFocus!))
            {
                _settingsReturnFocus = null;
            }
            _settingsCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.LifetimeToken);
            _settingsLoading = ui.OpenScoped<LoadingOverlay>(
                StarterKeys.LoadingOverlay,
                loading =>
                {
                    loading.SetProgress(0f);
                    loading.SetCancelAction(_settingsCancellation.Cancel);
                });
            _settingsView = await ui.OpenAsync<SettingsView>(
                StarterKeys.SettingsView,
                ConfigureSettings,
                progress => _settingsLoading?.View.SetProgress(progress),
                _settingsCancellation.Token);
        }
        catch (OperationCanceledException) when (_settingsCancellation?.IsCancellationRequested == true)
        {
            StarterLog.MainMenu.Info("Settings open cancelled.");
        }
        catch (Exception exception)
        {
            StarterLog.MainMenu.Error(exception, "OpenSettings");
        }
        finally
        {
            CloseSettingsLoading(restoreFocus: _context != null);
            _settingsCancellation?.Dispose();
            _settingsCancellation = null;
            _isOpeningSettings = false;
        }
    }

    private async void OnSettingsCloseSelected(SettingsCloseSelectedEvent _)
    {
        await CloseSettingsAsync();
    }

    private async void OnBackSelected(BackSelectedEvent _)
    {
        if (_context == null)
            return;

        await CloseSettingsAsync();
    }

    private static void SetMenuContext(ProcedureContext context)
    {
        if (StarterInput.IsReady(context))
            context.GetService<IInputService>().SetBaseContext(StarterInput.Menu);
    }

    private async Task CloseSettingsAsync()
    {
        _settingsCancellation?.Cancel();
        ProcedureContext? context = _context;
        SettingsView? view = _settingsView;
        if (context == null || !GodotObject.IsInstanceValid(view))
            return;

        IUiService ui = context.GetService<IUiService>();
        bool closed = await UiTransitionCoordinator.TryCloseAsync(view!, () => ui.TryClose(view!));
        if (closed && ReferenceEquals(_settingsView, view))
            _settingsView = null;
    }

    private void ConfigureSettings(SettingsView view)
    {
        CloseSettingsLoading(restoreFocus: true);
        view.Refresh();
    }

    private void CloseSettingsLoading(bool restoreFocus)
    {
        _settingsLoading?.Dispose();
        _settingsLoading = null;
        if (restoreFocus &&
            GodotObject.IsInstanceValid(_settingsReturnFocus) &&
            _settingsReturnFocus!.IsInsideTree() &&
            _settingsReturnFocus.IsVisibleInTree() &&
            _settingsReturnFocus.GetFocusModeWithOverride() != Control.FocusModeEnum.None)
        {
            _settingsReturnFocus.GrabFocus();
        }

        _settingsReturnFocus = null;
    }

    private void CleanupMainMenu()
    {
        _mainMenu?.Dispose();
        _mainMenu = null;
    }

    private void CancelSettingsOpen()
    {
        _settingsCancellation?.Cancel();
    }

    private void CleanupSettingsView(IUiService ui)
    {
        if (GodotObject.IsInstanceValid(_settingsView))
            ui.TryClose(_settingsView!);
        _settingsView = null;
    }
}
