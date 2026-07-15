using GoDo;
using System.Threading.Tasks;

namespace Demo3D;

public sealed class BootProcedure : IProcedure
{
    public string Name => "Boot";

    public Task EnterAsync(ProcedureContext context)
    {
        context.RequestChange<GameplayProcedure>();
        return Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
}
