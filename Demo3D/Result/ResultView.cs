using System;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>Demo3D 的通关结算页面。</summary>
public sealed partial class ResultView : Control
{
    [Export] public NodePath RetryButtonPath { get; set; } = null!;
    [Export] public NodePath ReturnToMenuButtonPath { get; set; } = null!;

    private Button? _retryButton;
    private Button? _returnToMenuButton;

    public override void _Ready()
    {
        _retryButton = GetNodeOrNull<Button>(RetryButtonPath);
        if (!IsInstanceValid(_retryButton))
            throw new InvalidOperationException("ResultView 缺少重试按钮引用。");
        _returnToMenuButton = GetNodeOrNull<Button>(ReturnToMenuButtonPath);
        if (!IsInstanceValid(_returnToMenuButton))
            throw new InvalidOperationException("ResultView 缺少返回主菜单按钮引用。");

        _retryButton.Pressed += OnRetryPressed;
        _returnToMenuButton.Pressed += OnReturnToMenuPressed;
        _retryButton.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_retryButton))
            _retryButton.Pressed -= OnRetryPressed;
        if (IsInstanceValid(_returnToMenuButton))
            _returnToMenuButton!.Pressed -= OnReturnToMenuPressed;

        _retryButton = null;
        _returnToMenuButton = null;
    }

    private void OnRetryPressed()
    {
        SetButtonsDisabled(true);
        EventChannel.Emit<RetrySelectedEvent>();
    }

    private void OnReturnToMenuPressed()
    {
        SetButtonsDisabled(true);
        EventChannel.Emit<ReturnToMenuSelectedEvent>();
    }

    private void SetButtonsDisabled(bool disabled)
    {
        _retryButton!.Disabled = disabled;
        _returnToMenuButton!.Disabled = disabled;
    }
}
