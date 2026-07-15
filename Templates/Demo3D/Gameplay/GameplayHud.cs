using System;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>显示当前收集进度和操作提示的 HUD。</summary>
public sealed partial class GameplayHud : Control
{
    [Export] public NodePath ProgressLabelPath { get; set; } = null!;
    [Export] public NodePath DeviceLabelPath { get; set; } = null!;

    private Label? _progressLabel;
    private Label? _deviceLabel;

    public override void _Ready()
    {
        _progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
        if (!GodotObject.IsInstanceValid(_progressLabel))
            throw new InvalidOperationException("GameplayHud 缺少进度标签引用。");
        _deviceLabel = GetNodeOrNull<Label>(DeviceLabelPath);
        if (!GodotObject.IsInstanceValid(_deviceLabel))
            throw new InvalidOperationException("GameplayHud 缺少输入设备标签引用。");

        EventChannel.Bind<CollectionProgressChangedEvent>(this, OnCollectionProgressChanged);
        EventChannel.Bind<InputDeviceChangedEvent>(this, OnInputDeviceChanged);
        UpdateDeviceLabel(Services.Get<IInputService>().ActiveDevice);
    }

    private void OnCollectionProgressChanged(CollectionProgressChangedEvent evt)
    {
        _progressLabel!.Text = $"能量核心：{evt.Current} / {evt.Total}";
    }

    private void OnInputDeviceChanged(InputDeviceChangedEvent evt) => UpdateDeviceLabel(evt.Current);

    private void UpdateDeviceLabel(InputDeviceKind device)
    {
        string name = device switch
        {
            InputDeviceKind.KeyboardMouse => "键盘鼠标",
            InputDeviceKind.Gamepad => "手柄",
            InputDeviceKind.Touch => "触摸",
            _ => "等待输入",
        };
        _deviceLabel!.Text = $"输入设备：{name}";
    }
}
