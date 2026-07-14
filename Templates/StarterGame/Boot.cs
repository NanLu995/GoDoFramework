using System;
using Godot;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>游戏启动场景</summary>
public sealed partial class Boot : Node
{
    public override async void _Ready()
    {
        try
        {
            await Services.Get<IProcedureService>().ChangeAsync<BootProcedure>();
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(Boot));
        }
    }
}
