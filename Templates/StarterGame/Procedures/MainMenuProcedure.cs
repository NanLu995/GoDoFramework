using System;
using System.Threading.Tasks;
using GoDo;

#nullable enable

namespace GoDoFramework.Templates.StarterGame;

/// <summary>主菜单流程：切换到主菜单主场景并播放菜单 BGM。</summary>
public sealed class MainMenuProcedure : IProcedure
{
    public string Name => "MainMenu";

    public async Task EnterAsync(ProcedureContext context)
    {
        await context.GetService<ISceneService>().ChangeAsync(StarterGameKeys.MainMenuScene);
        PlayBgm(context);
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;

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
