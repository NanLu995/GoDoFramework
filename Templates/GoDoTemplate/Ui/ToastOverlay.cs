using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>显示短时提示并在计时结束后释放自身的 Overlay。</summary>
public sealed partial class ToastOverlay : Control
{
    [Export] public NodePath MessageLabelPath { get; set; } = null!;
    [Export] public NodePath DismissTimerPath { get; set; } = null!;

    private string _message = string.Empty;
    private Label? _messageLabel;
    private Timer? _dismissTimer;

    /// <summary>在 Overlay 加入场景树前设置提示文本。</summary>
    /// <param name="message">面向玩家的短时提示，不能为空或全空白。</param>
    /// <exception cref="ArgumentException">文本为空或仅包含空白字符。</exception>
    public void Show(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _message = message;
        if (GodotObject.IsInstanceValid(_messageLabel))
            _messageLabel!.Text = _message;
    }

    public override void _Ready()
    {
        _messageLabel = RequireNode<Label>(MessageLabelPath, "提示标签");
        _dismissTimer = RequireNode<Timer>(DismissTimerPath, "关闭计时器");
        _messageLabel.Text = _message;
        _dismissTimer.Timeout += OnDismissTimerTimeout;
        _dismissTimer.Start();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_dismissTimer))
            _dismissTimer!.Timeout -= OnDismissTimerTimeout;

        _messageLabel = null;
        _dismissTimer = null;
    }

    private void OnDismissTimerTimeout()
    {
        Services.Get<IUiService>().TryClose(this);
    }

    private T RequireNode<T>(NodePath path, string description)
        where T : Node
    {
        T? node = GetNodeOrNull<T>(path);
        if (!GodotObject.IsInstanceValid(node))
            throw new InvalidOperationException($"ToastOverlay 缺少{description}引用。");

        return node!;
    }
}
