using Godot;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>在场景生命周期之外把原始 Godot 输入事件交给 GUIDE 后端做设备分类。</summary>
internal sealed partial class GuideInputDeviceTracker : Node
{
    internal const string NodeName = "GoDoGuideInputDeviceTracker";

    private GuideInputBackend? _backend;

    internal void Initialize(GuideInputBackend backend)
    {
        _backend = backend;
        Name = NodeName;
        ProcessMode = ProcessModeEnum.Always;
    }

    internal void Stop()
    {
        SetProcessInput(false);
        _backend = null;
    }

    /// <inheritdoc />
    public override void _Input(InputEvent inputEvent) => _backend?.ObserveInputEvent(inputEvent);
}
