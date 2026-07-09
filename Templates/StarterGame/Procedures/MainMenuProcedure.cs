using System;
using System.Threading.Tasks;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>主菜单流程：切换到主菜单主场景并响应主菜单操作。</summary>
public sealed class MainMenuProcedure : IProcedure
{
    private ProcedureContext? _context;

    public string Name => "MainMenu";

    public async Task EnterAsync(ProcedureContext context)
    {
        _context = context;
        await context.GetService<ISceneService>().ChangeAsync(StarterGameKeys.MainMenuScene);
        PlayBgm(context);
    }

    public Task ExitAsync(ProcedureContext context)
    {
        _context = null;
        return Task.CompletedTask;
    }

    public void StartGame()
    {
        if (_context == null)
            throw new InvalidOperationException("MainMenuProcedure 尚未进入，不能开始游戏。");

        _context.RequestChange(new GameplayProcedure());
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
}
