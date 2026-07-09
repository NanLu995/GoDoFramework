using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>游戏流程：切换主内容场景，打开 HUD，并响应一局游戏结束。</summary>
public sealed class GameplayProcedure : IProcedure
{
    private Control? _hud;
    private ProcedureContext? _context;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        await context.GetService<ISceneService>().ChangeAsync(StarterGameKeys.GameplayScene);
        _hud = context.GetService<IUiService>().Open(StarterGameKeys.GameplayHud, UiLayer.Scene);
        PlayBgm(context);
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _context = null;
        CloseHud(context);
        return Task.CompletedTask;
    }

    public void FinishRun(int score)
    {
        if (_context == null)
            throw new InvalidOperationException("GameplayProcedure 尚未进入，不能结束游戏。");

        ISaveService saves = _context.GetService<ISaveService>();
        SaveLoadResult<StarterSaveData> loaded = saves.Load(StarterGameKeys.SaveSlot, StarterGameKeys.SaveCodec);
        StarterSaveData data = loaded.HasValue ? loaded.Value : new StarterSaveData();
        data.LastScore = score;
        data.BestScore = Math.Max(data.BestScore, score);
        data.GamesPlayed++;
        saves.Save(StarterGameKeys.SaveSlot, data, StarterSaveCodec.CurrentDataVersion, StarterGameKeys.SaveCodec);

        EventChannel.Emit(new StarterRunFinishedEvent(score));
        _context.RequestChange(new ResultProcedure());
    }

    private async void PlayBgm(ProcedureContext context)
    {
        try
        {
            await context.GetService<IAudioService>().PlayBgmAsync(StarterGameKeys.Bgm);
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, Name, StarterGameKeys.Bgm.Value);
        }
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
