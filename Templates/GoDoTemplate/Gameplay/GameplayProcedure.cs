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
    private Control? _hud;
    private Control? _pauseModal;
    private Control? _confirmDialog;
    private ProcedureContext? _context;
    private bool _pausedByProcedure;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        IUiService ui = context.GetService<IUiService>();
        context.RegisterCleanup(() => SetScenePaused(false));
        context.RegisterCleanup(() => CloseHud(ui));
        context.RegisterCleanup(() => ClosePauseModal(ui));
        context.RegisterCleanup(() => CloseConfirmDialog(ui));
        LoadingOverlay loading = ui.Open<LoadingOverlay>(StarterKeys.LoadingOverlay);
        try
        {
            await context.GetService<ISceneService>().ChangeAsync(StarterKeys.GameplayScene);
        }
        finally
        {
            ui.TryClose(loading);
        }

        SetGameplayContext(context);
        GameplayHud hud = ui.Open<GameplayHud>(StarterKeys.GameplayHud);
        _hud = hud;
        context.RegisterCleanup(() => ui.TryClose(hud));
        context.Events.On<PauseSelectedEvent>(OnPauseSelected);
        context.Events.On<ResumeSelectedEvent>(OnResumeSelected);
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
        if (_context == null || GodotObject.IsInstanceValid(_pauseModal))
            return;

        SetScenePaused(true);
        PushPauseContext();
        IUiService ui = _context.GetService<IUiService>();
        PauseModal pauseModal = ui.Open<PauseModal>(StarterKeys.PauseModal);
        _pauseModal = pauseModal;
    }

    private void OnResumeSelected(ResumeSelectedEvent _)
    {
        if (_context == null || !GodotObject.IsInstanceValid(_pauseModal))
            return;

        _context.GetService<IUiService>().TryClose(_pauseModal!);
        _pauseModal = null;
        PopPauseContext();
        SetScenePaused(false);
    }

    private void OnReturnToMainMenuSelected(ReturnToMainMenuSelectedEvent _)
    {
        if (_context == null || GodotObject.IsInstanceValid(_confirmDialog))
            return;

        IUiService ui = _context.GetService<IUiService>();
        ConfirmDialogModal confirmDialog = ui.Open<ConfirmDialogModal>(
            StarterKeys.ConfirmDialog,
            dialog => dialog.SetMessage("Return to the main menu?"));
        _confirmDialog = confirmDialog;
    }

    private void OnConfirmAccepted(ConfirmAcceptedEvent _)
    {
        if (_context == null || !GodotObject.IsInstanceValid(_confirmDialog))
            return;

        SetScenePaused(false);
        _context.RequestChange<MainMenuProcedure>();
    }

    private void OnConfirmCancelled(ConfirmCancelledEvent _)
    {
        if (_context == null || !GodotObject.IsInstanceValid(_confirmDialog))
            return;

        _context.GetService<IUiService>().TryClose(_confirmDialog!);
        _confirmDialog = null;
    }

    private void OnBackSelected(BackSelectedEvent _)
    {
        if (_context == null)
            return;

        if (GodotObject.IsInstanceValid(_confirmDialog))
        {
            OnConfirmCancelled(default);
            return;
        }

        if (GodotObject.IsInstanceValid(_pauseModal))
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
        ui.Open<ToastOverlay>(StarterKeys.ToastOverlay, toast => toast.Show(message));
    }

    private void CloseConfirmDialog(IUiService ui)
    {
        Control? view = _confirmDialog;
        _confirmDialog = null;
        if (GodotObject.IsInstanceValid(view))
            ui.TryClose(view!);
    }

    private void ClosePauseModal(IUiService ui)
    {
        Control? view = _pauseModal;
        _pauseModal = null;
        if (GodotObject.IsInstanceValid(view))
            ui.TryClose(view!);
    }

    private void CloseHud(IUiService ui)
    {
        Control? view = _hud;
        _hud = null;
        if (GodotObject.IsInstanceValid(view))
            ui.TryClose(view!);
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
        if (_context != null && StarterInput.IsReady(_context))
            _context.GetService<IInputService>().PushContext(StarterInput.Pause);
    }

    private void PopPauseContext()
    {
        if (_context != null && StarterInput.IsReady(_context))
            _context.GetService<IInputService>().PopContext(StarterInput.Pause);
    }
}
