using System;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>Demo3D 的通关结算页面。</summary>
public sealed partial class ResultView : Control
{
    [Export] public NodePath RetryButtonPath { get; set; } = null!;

    private Button? _retryButton;

    public override void _Ready()
    {
        _retryButton = GetNodeOrNull<Button>(RetryButtonPath);
        if (!IsInstanceValid(_retryButton))
            throw new InvalidOperationException("ResultView 缺少重试按钮引用。");

        _retryButton.Pressed += OnRetryPressed;
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_retryButton))
            _retryButton.Pressed -= OnRetryPressed;

        _retryButton = null;
    }

    private void OnRetryPressed()
    {
        _retryButton!.Disabled = true;
        EventChannel.Emit<RetrySelectedEvent>();
    }
}
