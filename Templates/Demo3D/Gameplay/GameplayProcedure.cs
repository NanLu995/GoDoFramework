using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>管理 3D 关卡、HUD 与收集完成后的流程切换。</summary>
public sealed class GameplayProcedure : IProcedure
{
    private const int CollectibleTotal = 5;

    private ProcedureContext? _context;
    private PauseModal? _pauseModal;
    private int _collectedCount;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        _pauseModal = null;
        _collectedCount = 0;
        IUiService ui = context.GetService<IUiService>();
        SceneTree tree = GetSceneTree();
        context.RegisterCleanup(() =>
        {
            tree.Paused = false;
            ClosePauseModal(ui);
            Input.MouseMode = Input.MouseModeEnum.Visible;
        });
        using (UiScope<LoadingOverlay> loading = ui.OpenScoped<LoadingOverlay>(
            Demo3DKeys.LoadingOverlay,
            UiLayer.Overlay))
        {
            await context.GetService<ISceneService>().ChangeAsync(
                Demo3DKeys.GameplayScene,
                loading.View.SetProgress,
                context.LifetimeToken);
        }

        context.GetService<ICameraService>().ActivatePrimary(Demo3DKeys.GameplayCamera);
        UiScope<Control> hud = ui.OpenScoped<Control>(
            Demo3DKeys.GameplayHud,
            UiLayer.Scene);
        context.RegisterCleanup(hud);
        context.Events.On<CollectibleCollectedEvent>(OnCollectibleCollected);
        context.Events.On<PauseRequestedEvent>(OnPauseRequested);
        context.Events.On<ResumeSelectedEvent>(OnResumeSelected);
        context.Events.On<ReturnToMenuSelectedEvent>(OnReturnToMenuSelected);
        context.GetService<IInputService>().SetBaseContext(Demo3DInput.Gameplay);
        EventChannel.Emit(new CollectionProgressChangedEvent(_collectedCount, CollectibleTotal));
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _context = null;
        return Task.CompletedTask;
    }

    private void OnCollectibleCollected(CollectibleCollectedEvent evt)
    {
        if (_context == null)
            throw new InvalidOperationException("GameplayProcedure 尚未进入，不能处理收集事件。");

        _collectedCount++;
        EventChannel.Emit(new CollectionProgressChangedEvent(_collectedCount, CollectibleTotal));
        if (_collectedCount >= CollectibleTotal)
            _context.TryRequestChange<ResultProcedure>();
    }

    private void OnPauseRequested(PauseRequestedEvent _)
    {
        ProcedureContext context = RequireContext();
        if (GodotObject.IsInstanceValid(_pauseModal))
            return;

        _pauseModal = context.GetService<IUiService>().Open<PauseModal>(
            Demo3DKeys.PauseModal,
            UiLayer.Modal);
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetSceneTree().Paused = true;
    }

    private void OnResumeSelected(ResumeSelectedEvent _)
    {
        IUiService ui = RequireContext().GetService<IUiService>();
        GetSceneTree().Paused = false;
        ClosePauseModal(ui);
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void OnReturnToMenuSelected(ReturnToMenuSelectedEvent _)
    {
        ProcedureContext context = RequireContext();
        GetSceneTree().Paused = false;
        ClosePauseModal(context.GetService<IUiService>());
        Input.MouseMode = Input.MouseModeEnum.Visible;
        context.TryRequestChange<MainMenuProcedure>();
    }

    private void ClosePauseModal(IUiService ui)
    {
        if (GodotObject.IsInstanceValid(_pauseModal))
            ui.TryClose(_pauseModal!);

        _pauseModal = null;
    }

    private ProcedureContext RequireContext() =>
        _context ?? throw new InvalidOperationException(
            "GameplayProcedure 尚未进入，不能处理游戏流程操作。");

    private static SceneTree GetSceneTree() =>
        Engine.GetMainLoop() as SceneTree ??
        throw new InvalidOperationException("Demo3D 当前没有可用的 SceneTree。");

}
