using System.Threading.Tasks;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 模板启动阶段的流程边界。
/// <para>后续阶段会在此按顺序加载设置、UI 目录与项目配置，再请求进入主菜单流程。</para>
/// </summary>
internal sealed class BootstrapProcedure : IProcedure
{
    public string Name => "Bootstrap";

    public Task EnterAsync(ProcedureContext context)
    {
        LoadSettings(context);
        StarterInput.LoadBindingsAndSetMenuContext(context);
        context.GetService<IUiService>().LoadUiConfig(StarterKeys.UiConfig);
        _ = ConfigHub.Load<StarterConfig>(StarterKeys.ProjectConfig);
        StarterLog.Boot.Info("BootstrapProcedure entered.");
        context.RequestChange<MainMenuProcedure>();
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;

    private static void LoadSettings(ProcedureContext context)
    {
        try
        {
            SettingsLoadStatus status = context.GetService<ISettingsService>().LoadAndApply();
            StarterLog.Boot.Info($"Settings loaded: {status}.");
        }
        catch (System.Exception exception)
        {
            StarterLog.Boot.Error(exception, "Settings");
        }
    }
}
