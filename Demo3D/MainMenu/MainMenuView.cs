using System;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>Demo3D 主菜单界面。</summary>
public sealed partial class MainMenuView : Control
{
    [Export] public NodePath StartButtonPath { get; set; } = null!;

    private Button? _startButton;

    public override void _Ready()
    {
        _startButton = GetNodeOrNull<Button>(StartButtonPath);
        if (!IsInstanceValid(_startButton))
            throw new InvalidOperationException("MainMenuView 缺少开始按钮引用。");

        _startButton.Pressed += OnStartPressed;
        _startButton.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_startButton))
            _startButton!.Pressed -= OnStartPressed;

        _startButton = null;
    }

    private void OnStartPressed()
    {
        _startButton!.Disabled = true;
        EventChannel.Emit<StartGameSelectedEvent>();
    }
}
