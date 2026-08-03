using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 模板的唯一业务启动入口。
/// <para>GoDoRuntime 必须已由 Autoload 初始化；该节点只启动模板流程，不重复创建框架服务。</para>
/// </summary>
public sealed partial class Boot : Node
{
    public override async void _Ready()
    {
        try
        {
            await Services.Get<IProcedureService>().ChangeAsync(new BootstrapProcedure());
        }
        catch (Exception exception)
        {
            StarterLog.Boot.Error(exception, nameof(Boot));
        }
    }
}
