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

    /// <summary>获取或设置用于应用多实例垂直偏移的容器节点路径。</summary>
    [Export] public NodePath StackContainerPath { get; set; } = null!;

    private string _message = string.Empty;
    private int _stackIndex;
    private Label? _messageLabel;
    private Timer? _dismissTimer;
    private Control? _stackContainer;

    /// <summary>在 Overlay 加入场景树前设置提示文本。</summary>
    /// <param name="message">面向玩家的短时提示，不能为空或全空白。</param>
    /// <exception cref="ArgumentException">文本为空或仅包含空白字符。</exception>
    public void Show(string message)
    {
        Show(message, 0);
    }

    /// <summary>在 Overlay 加入场景树前设置提示文本和当前堆叠序号。</summary>
    /// <param name="message">面向玩家的短时提示，不能为空或全空白。</param>
    /// <param name="stackIndex">从零开始的堆叠序号；后续提示显示在更高位置。</param>
    /// <exception cref="ArgumentException">文本为空或仅包含空白字符。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stackIndex"/> 小于零。</exception>
    public void Show(string message, int stackIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentOutOfRangeException.ThrowIfNegative(stackIndex);
        _message = message;
        _stackIndex = stackIndex;
        if (GodotObject.IsInstanceValid(_messageLabel))
            _messageLabel!.Text = _message;
        ApplyStackOffset();
    }

    public override void _Ready()
    {
        _messageLabel = RequireNode<Label>(MessageLabelPath, "提示标签");
        _dismissTimer = RequireNode<Timer>(DismissTimerPath, "关闭计时器");
        _stackContainer = RequireNode<Control>(StackContainerPath, "堆叠容器");
        _messageLabel.Text = _message;
        ApplyStackOffset();
        _dismissTimer.Timeout += OnDismissTimerTimeout;
        _dismissTimer.Start();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_dismissTimer))
            _dismissTimer!.Timeout -= OnDismissTimerTimeout;

        _messageLabel = null;
        _dismissTimer = null;
        _stackContainer = null;
    }

    private async void OnDismissTimerTimeout()
    {
        IUiService ui = Services.Get<IUiService>();
        await UiTransitionCoordinator.TryCloseAsync(this, () => ui.TryClose(this));
    }

    private void ApplyStackOffset()
    {
        if (!GodotObject.IsInstanceValid(_stackContainer))
            return;

        const float StackSpacing = 72f;
        float offset = _stackIndex * StackSpacing;
        _stackContainer!.OffsetTop = -92f - offset;
        _stackContainer.OffsetBottom = -20f - offset;
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
