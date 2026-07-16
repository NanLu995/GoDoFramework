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
            await Services.Get<IProcedureService>().ChangeAsync<BootProcedure>();
        }
        catch (System.Exception exception)
        {
            ErrorHub.Report(exception, nameof(Boot));
        }
    }
}
