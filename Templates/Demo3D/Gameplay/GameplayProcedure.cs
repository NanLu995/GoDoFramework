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

    private readonly EventScope _events = new();
    private Control? _hud;
    private ProcedureContext? _context;
    private int _collectedCount;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        _collectedCount = 0;
        context.GetService<IInputService>().SetBaseContext(Demo3DInput.Gameplay);
        await context.GetService<ISceneService>().ChangeAsync(Demo3DKeys.GameplayScene);
        context.GetService<ICameraService>().ActivatePrimary(Demo3DKeys.GameplayCamera);
        _hud = context.GetService<IUiService>().Open(Demo3DKeys.GameplayHud, UiLayer.Scene);
        _events.On<CollectibleCollectedEvent>(OnCollectibleCollected);
        EventChannel.Emit(new CollectionProgressChangedEvent(_collectedCount, CollectibleTotal));
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _events.Dispose();
        _context = null;
        CloseHud(context);
        return Task.CompletedTask;
    }

    private void OnCollectibleCollected(CollectibleCollectedEvent evt)
    {
        if (_context == null)
            throw new InvalidOperationException("GameplayProcedure 尚未进入，不能处理收集事件。");

        _collectedCount++;
        EventChannel.Emit(new CollectionProgressChangedEvent(_collectedCount, CollectibleTotal));
        if (_collectedCount >= CollectibleTotal)
            _context.RequestChange<ResultProcedure>();
    }

    private void CloseHud(ProcedureContext context)
    {
        if (!GodotObject.IsInstanceValid(_hud))
            return;

        try
        {
            context.GetService<IUiService>().Close(_hud!);
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, Name, "关闭 Gameplay HUD 失败");
        }
        finally
        {
            _hud = null;
        }
    }
}
