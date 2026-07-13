using System.Threading.Tasks;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>模板启动流程：验证配置、读取存档并应用设置，然后进入主菜单流程。</summary>
public sealed class BootProcedure : IProcedure
{
    public string Name => "Boot";

    public Task EnterAsync(ProcedureContext context)
    {
        _ = ConfigHub.Load<StarterGameConfig>(StarterGameKeys.Config);
        ResourceRegistry.Load(ResourceHub.Load<ResourceManifest>(StarterGameKeys.ResourceManifest));
        _ = context.GetService<ISaveService>().Load(StarterGameKeys.SaveSlot, StarterGameKeys.SaveCodec);
        _ = context.GetService<ISettingsService>().LoadAndApply();
        ErrorHub.Debug("StarterGame 启动检查完成", Name);
        context.RequestChange(new MainMenuProcedure());
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
}
