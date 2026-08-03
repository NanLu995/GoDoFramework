using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>可由 Procedure 配置文本的通用确认 Modal。</summary>
public sealed partial class ConfirmDialogModal : Control
{
    [Export] public NodePath MessageLabelPath { get; set; } = null!;
    [Export] public NodePath ConfirmButtonPath { get; set; } = null!;
    [Export] public NodePath CancelButtonPath { get; set; } = null!;

    private string _message = string.Empty;
    private Label? _messageLabel;
    private Button? _confirmButton;
    private Button? _cancelButton;

    /// <summary>在界面加入场景树前设置本次确认的显示文本。</summary>
    /// <param name="message">面向玩家的确认说明，不能为空或全空白。</param>
    /// <exception cref="ArgumentException">文本为空或仅包含空白字符。</exception>
    public void SetMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _message = message;
        if (GodotObject.IsInstanceValid(_messageLabel))
            _messageLabel!.Text = _message;
    }

    public override void _Ready()
    {
        _messageLabel = RequireNode<Label>(MessageLabelPath, "说明标签");
        _confirmButton = RequireNode<Button>(ConfirmButtonPath, "确认按钮");
        _cancelButton = RequireNode<Button>(CancelButtonPath, "取消按钮");
        _messageLabel.Text = _message;
        _confirmButton.Pressed += OnConfirmPressed;
        _cancelButton.Pressed += OnCancelPressed;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_confirmButton))
            _confirmButton!.Pressed -= OnConfirmPressed;
        if (GodotObject.IsInstanceValid(_cancelButton))
            _cancelButton!.Pressed -= OnCancelPressed;

        _messageLabel = null;
        _confirmButton = null;
        _cancelButton = null;
    }

    private void OnConfirmPressed()
    {
        _confirmButton!.Disabled = true;
        EventChannel.Emit<ConfirmAcceptedEvent>();
    }

    private void OnCancelPressed() => EventChannel.Emit<ConfirmCancelledEvent>();

    private T RequireNode<T>(NodePath path, string description)
        where T : Node
    {
        T? node = GetNodeOrNull<T>(path);
        if (!GodotObject.IsInstanceValid(node))
            throw new InvalidOperationException($"ConfirmDialogModal 缺少{description}引用。");

        return node!;
    }
}
