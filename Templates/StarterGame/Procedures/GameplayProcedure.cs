using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Templates.StarterGame;

/// <summary>游戏流程：切换主内容场景，打开 HUD 并播放游戏 BGM。</summary>
public sealed class GameplayProcedure : IProcedure
{
    private Control? _hud;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        await context.GetService<ISceneService>().ChangeAsync(StarterGameKeys.GameplayScene);
        _hud = context.GetService<IUiService>().Open(StarterGameKeys.GameplayHud, UiLayer.Scene);
        PlayBgm(context);
    }

    public Task ExitAsync(ProcedureContext context)
    {
        CloseHud(context);
        return Task.CompletedTask;
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
