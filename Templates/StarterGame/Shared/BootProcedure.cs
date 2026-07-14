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
        // 加载游戏配置，使后续流程可通过 ConfigHub 读取配置数据。
        ConfigHub.Load<StarterGameConfig>(StarterGameKeys.Config);

        // 通过资源清单加载并注册语义 ID，避免业务代码分散使用 res:// 路径。
        ResourceRegistry.Load(ResourceHub.Load<ResourceManifest>(StarterGameKeys.ResourceManifest));

        // 读取存档槽中的持久化数据；首次启动时由存档服务按其约定处理缺失存档。
        context.GetService<ISaveService>().Load(StarterGameKeys.SaveSlot, StarterGameKeys.SaveCodec);

        // 读取设置并立即应用，例如音量等已持久化的全局设置。
        context.GetService<ISettingsService>().LoadAndApply();

        // 记录启动加载完成，便于在调试日志中确认初始化顺序。
        LogHub.Debug("启动加载完成", Name);

        // 基础数据准备完成后，切换到模板的主菜单流程。
        context.RequestChange<MainMenuProcedure>();

        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
}
