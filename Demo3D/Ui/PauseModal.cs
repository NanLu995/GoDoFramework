using System;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>暂停期间保持响应的 Gameplay 模态界面。</summary>
public sealed partial class PauseModal : Control
{
    [Export] public NodePath ResumeButtonPath { get; set; } = null!;
    [Export] public NodePath ReturnToMenuButtonPath { get; set; } = null!;

    private Button? _resumeButton;
    private Button? _returnToMenuButton;

    public override void _Ready()
    {
        _resumeButton = RequireButton(ResumeButtonPath, "继续游戏");
        _returnToMenuButton = RequireButton(ReturnToMenuButtonPath, "返回主菜单");
        _resumeButton.Pressed += OnResumePressed;
        _returnToMenuButton.Pressed += OnReturnToMenuPressed;
        _resumeButton.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_resumeButton))
            _resumeButton!.Pressed -= OnResumePressed;
        if (IsInstanceValid(_returnToMenuButton))
            _returnToMenuButton!.Pressed -= OnReturnToMenuPressed;

        _resumeButton = null;
        _returnToMenuButton = null;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent ||
            keyEvent.Keycode != Key.Escape)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        EmitResume();
    }

    private void OnResumePressed() => EmitResume();

    private void EmitResume()
    {
        if (_resumeButton!.Disabled)
            return;

        SetButtonsDisabled(true);
        EventChannel.Emit<ResumeSelectedEvent>();
    }

    private void OnReturnToMenuPressed()
    {
        if (_returnToMenuButton!.Disabled)
            return;

        SetButtonsDisabled(true);
        EventChannel.Emit<ReturnToMenuSelectedEvent>();
    }

    private Button RequireButton(NodePath path, string description)
    {
        Button? button = GetNodeOrNull<Button>(path);
        if (!IsInstanceValid(button))
            throw new InvalidOperationException($"PauseModal 缺少{description}按钮引用。");

        return button!;
    }

    private void SetButtonsDisabled(bool disabled)
    {
        _resumeButton!.Disabled = disabled;
        _returnToMenuButton!.Disabled = disabled;
    }
}
