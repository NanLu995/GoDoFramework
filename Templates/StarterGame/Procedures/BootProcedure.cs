using System.Threading.Tasks;
using GoDo;

#nullable enable

namespace GoDoFramework.Templates.StarterGame;

/// <summary>模板启动流程：验证配置、读取存档并应用设置。</summary>
public sealed class BootProcedure : IProcedure
{
    public string Name => "Boot";

    public Task EnterAsync(ProcedureContext context)
    {
        _ = ConfigHub.Load<StarterGameConfig>(StarterGameKeys.Config);
        _ = context.GetService<ISaveService>().Load(StarterGameKeys.SaveSlot, StarterGameKeys.SaveCodec);
        _ = context.GetService<ISettingsService>().LoadAndApply();
        ErrorHub.Debug("StarterGame 启动检查完成", Name);
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
}
