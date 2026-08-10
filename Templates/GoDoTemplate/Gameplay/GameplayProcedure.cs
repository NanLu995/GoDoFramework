using System;
using System.Threading.Tasks;
using Godot;
using GoDo;
using GoDoTemplate.ExampleContent;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 管理模板 Gameplay 场景、Scene UI 和暂停相关 Modal 的顶层流程。
/// <para>该流程不包含具体玩法；项目可替换 GameplayScene 的示例内容，同时保留场景切换和 UI 生命周期边界。</para>
/// </summary>
internal sealed class GameplayProcedure : IProcedure
{
    private UiScope<GameplayHud>? _hud;
    private UiScope<PauseModal>? _pauseModal;
    private UiScope<SettingsView>? _settings;
    private UiScope<ConfirmDialogModal>? _confirmDialog;
    private ProcedureContext? _context;
    private bool _pausedByProcedure;
    private bool _pauseContextPushed;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        IUiService ui = context.GetService<IUiService>();
        context.RegisterCleanup(() => SetScenePaused(false));
        context.RegisterCleanup(() => PopPauseContext(context));
        context.RegisterCleanup(() => ui.CloseAll(StarterKeys.ToastOverlay));
        context.RegisterCleanup(CleanupHud);
        context.RegisterCleanup(CleanupPauseModal);
        context.RegisterCleanup(CleanupSettings);
        context.RegisterCleanup(CleanupConfirmDialog);
        UiScope<LoadingOverlay> loading = ui.OpenScoped<LoadingOverlay>(
            StarterKeys.LoadingOverlay,
            view => view.SetProgress(0f));
        try
        {
            await context.GetService<ISceneService>().ChangeAsync(
                StarterKeys.GameplayScene,
                loading.View.SetProgress,
                context.LifetimeToken);
        }
        finally
        {
            await UiTransitionCoordinator.TryCloseAsync(loading.View, loading.Dispose);
        }

        SetGameplayContext(context);
        _hud = ui.OpenScoped<GameplayHud>(StarterKeys.GameplayHud);
        context.Events.On<PauseSelectedEvent>(OnPauseSelected);
        context.Events.On<ResumeSelectedEvent>(OnResumeSelected);
        context.Events.On<PauseSettingsSelectedEvent>(OnPauseSettingsSelected);
        context.Events.On<SettingsCloseSelectedEvent>(OnSettingsCloseSelected);
        context.Events.On<ReturnToMainMenuSelectedEvent>(OnReturnToMainMenuSelected);
        context.Events.On<ConfirmAcceptedEvent>(OnConfirmAccepted);
        context.Events.On<ConfirmCancelledEvent>(OnConfirmCancelled);
        context.Events.On<BackSelectedEvent>(OnBackSelected);
        context.Events.On<ExampleSaveSelectedEvent>(OnExampleSaveSelected);
        ShowToast(ui, "Gameplay scene ready.");
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _context = null;
        return Task.CompletedTask;
    }

    private void OnPauseSelected(PauseSelectedEvent _)
    {
        if (_context == null || _pauseModal != null || _settings != null)
            return;

        SetScenePaused(true);
        PushPauseContext();
        IUiService ui = _context.GetService<IUiService>();
        _pauseModal = ui.OpenScoped<PauseModal>(StarterKeys.PauseModal);
    }

    private async void OnResumeSelected(ResumeSelectedEvent _)
    {
        ProcedureContext? context = _context;
        UiScope<PauseModal>? pauseModal = _pauseModal;
        if (context == null || pauseModal == null)
            return;

        bool closed = await UiTransitionCoordinator.TryCloseAsync(pauseModal.View, pauseModal.Dispose);
        if (!closed || _context != context || _pauseModal != pauseModal)
            return;

        _pauseModal = null;
        PopPauseContext();
        SetScenePaused(false);
    }

    private void OnReturnToMainMenuSelected(ReturnToMainMenuSelectedEvent _)
    {
        if (_context == null || _pauseModal == null || _confirmDialog != null)
            return;

        IUiService ui = _context.GetService<IUiService>();
        _confirmDialog = ui.OpenScoped<ConfirmDialogModal>(
            StarterKeys.ConfirmDialog,
            dialog => dialog.SetMessage("Return to the main menu?"));
    }

    private async void OnConfirmAccepted(ConfirmAcceptedEvent _)
    {
        ProcedureContext? context = _context;
        UiScope<ConfirmDialogModal>? confirmDialog = _confirmDialog;
        if (context == null || confirmDialog == null)
            return;

        await UiTransitionCoordinator.TryCloseAsync(
            confirmDialog.View,
            () =>
            {
                SetScenePaused(false);
                context.RequestChange<MainMenuProcedure>();
            });
    }

    private async void OnConfirmCancelled(ConfirmCancelledEvent _)
    {
        ProcedureContext? context = _context;
        UiScope<ConfirmDialogModal>? confirmDialog = _confirmDialog;
        if (context == null || confirmDialog == null)
            return;

        bool closed = await UiTransitionCoordinator.TryCloseAsync(confirmDialog.View, confirmDialog.Dispose);
        if (closed && _context == context && _confirmDialog == confirmDialog)
            _confirmDialog = null;
    }

    private void OnBackSelected(BackSelectedEvent _)
    {
        if (_context == null)
            return;

        if (_confirmDialog != null)
        {
            OnConfirmCancelled(default);
            return;
        }

        if (_settings != null)
        {
            OnSettingsCloseSelected(default);
            return;
        }

        if (_pauseModal != null)
        {
            OnResumeSelected(default);
            return;
        }

        OnPauseSelected(default);
    }

    private void OnExampleSaveSelected(ExampleSaveSelectedEvent _)
    {
        if (_context == null)
            return;

        IUiService ui = _context.GetService<IUiService>();
        try
        {
            ExampleSaveData value = ExampleSaveStore.SaveNext(_context.GetService<ISaveService>());
            ShowToast(ui, $"Example save written ({value.WriteCount}).");
        }
        catch (Exception exception)
        {
            StarterLog.Gameplay.Error(exception, "ExampleSave");
            ShowToast(ui, "Example save failed. See the error log.");
        }
    }

    private static void ShowToast(IUiService ui, string message)
    {
        int stackIndex = ui.GetOpenCount(StarterKeys.ToastOverlay);
        ui.Open<ToastOverlay>(StarterKeys.ToastOverlay, toast => toast.Show(message, stackIndex));
    }

    private async void OnPauseSettingsSelected(PauseSettingsSelectedEvent _)
    {
        ProcedureContext? context = _context;
        UiScope<PauseModal>? pauseModal = _pauseModal;
        if (context == null || pauseModal == null || _confirmDialog != null || _settings != null)
            return;

        bool closed = await UiTransitionCoordinator.TryCloseAsync(pauseModal.View, pauseModal.Dispose);
        if (!closed || _context != context || _pauseModal != pauseModal)
            return;

        _pauseModal = null;
        _settings = context.GetService<IUiService>().OpenScoped<SettingsView>(
            StarterKeys.SettingsView,
            view => view.Refresh());
    }

    private async void OnSettingsCloseSelected(SettingsCloseSelectedEvent _)
    {
        ProcedureContext? context = _context;
        UiScope<SettingsView>? settings = _settings;
        if (context == null || settings == null)
            return;

        bool closed = await UiTransitionCoordinator.TryCloseAsync(settings.View, settings.Dispose);
        if (!closed || _context != context || _settings != settings)
            return;

        _settings = null;
        _pauseModal = context.GetService<IUiService>().OpenScoped<PauseModal>(StarterKeys.PauseModal);
    }

    private void CleanupConfirmDialog()
    {
        _confirmDialog?.Dispose();
        _confirmDialog = null;
    }

    private void CleanupSettings()
    {
        _settings?.Dispose();
        _settings = null;
    }

    private void CleanupPauseModal()
    {
        _pauseModal?.Dispose();
        _pauseModal = null;
    }

    private void CleanupHud()
    {
        _hud?.Dispose();
        _hud = null;
    }

    private void SetScenePaused(bool paused)
    {
        if (Engine.GetMainLoop() is not SceneTree sceneTree)
            throw new InvalidOperationException("当前主循环不是 SceneTree，不能修改暂停状态。");

        if (paused)
        {
            if (sceneTree.Paused)
                return;

            sceneTree.Paused = true;
            _pausedByProcedure = true;
            return;
        }

        if (!_pausedByProcedure)
            return;

        sceneTree.Paused = false;
        _pausedByProcedure = false;
    }

    private static void SetGameplayContext(ProcedureContext context)
    {
        if (StarterInput.IsReady(context))
            context.GetService<IInputService>().SetBaseContext(StarterInput.Gameplay);
    }

    private void PushPauseContext()
    {
        if (_context != null && !_pauseContextPushed && StarterInput.IsReady(_context))
        {
            _context.GetService<IInputService>().PushContext(StarterInput.Pause);
            _pauseContextPushed = true;
        }
    }

    private void PopPauseContext() => PopPauseContext(_context);

    private void PopPauseContext(ProcedureContext? context)
    {
        if (!_pauseContextPushed || context == null)
            return;

        if (StarterInput.IsReady(context))
            context.GetService<IInputService>().PopContext(StarterInput.Pause);
        _pauseContextPushed = false;
    }
}
