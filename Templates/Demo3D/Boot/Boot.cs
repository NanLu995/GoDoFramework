using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

public sealed partial class Boot : Node
{
    public override async void _Ready()
    {
        try
        {
            LoadInputBindings();
            await Services.Get<IProcedureService>().ChangeAsync<BootProcedure>();
        }
        catch (System.Exception exception)
        {
            ErrorHub.Report(exception, nameof(Boot));
        }
    }

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
