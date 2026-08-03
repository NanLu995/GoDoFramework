using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 将已配置输入后端的 <c>ui.back</c> Action 转换为模板业务事件。
/// <para>未安装 GUIDE 或其他后端时保持空操作；该节点不读取任何第三方输入类型。</para>
/// </summary>
public sealed partial class InputBackHandler : Node
{
    private IInputService? _input;

    public override void _Ready()
    {
        _input = Services.Get<IInputService>();
    }

    public override void _Process(double delta)
    {
        if (_input?.IsReady != true)
            return;

        try
        {
            if (_input.Frame.JustPressed(StarterInput.Back))
                EventChannel.Emit<BackSelectedEvent>();
        }
        catch (InputOperationException)
        {
            // The backend can be ready before its first completed input sample.
        }
    }

    public override void _ExitTree()
    {
        _input = null;
    }
}
