using GoDo;
using System.Threading.Tasks;

namespace Demo3D;

public sealed class BootProcedure : IProcedure
{
    public string Name => "Boot";

    public Task EnterAsync(ProcedureContext context)
    {
        LoadInputBindings();
        context.RequestChange<GameplayProcedure>();
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;

    private static void LoadInputBindings()
    {
        IInputService input = Services.Get<IInputService>();
        if (!input.TryGetRebindingPersistence(out IInputRebindingPersistence? persistence) || persistence == null)
            throw new System.InvalidOperationException("Demo3D 需要支持持久化的输入后端。");

        try
        {
            InputBindingLoadStatus status = persistence.LoadAndApply();
            if (status == InputBindingLoadStatus.RecoveredFromBackup)
            {
                ErrorHub.Warn(
                    "输入绑定正式配置不可用，已从备份恢复",
                    nameof(Boot));
            }
        }
        catch (System.Exception exception)
        {
            ErrorHub.Report(exception, nameof(Boot));
        }
    }
}
