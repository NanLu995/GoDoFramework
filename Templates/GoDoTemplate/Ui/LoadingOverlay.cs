using System;
using Godot;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 在异步请求期间显示调用方推送的进度，并可选地提供取消操作。
/// <para>调用方拥有该 Overlay，并负责在请求结束后关闭；该组件不查询服务或维护资源状态。</para>
/// </summary>
public sealed partial class LoadingOverlay : Control
{
    [Export] public NodePath ProgressLabelPath { get; set; } = null!;

    /// <summary>获取或设置可选取消按钮相对当前 Overlay 的节点路径。</summary>
    [Export] public NodePath CancelButtonPath { get; set; } = null!;

    private float _progress;
    private Action? _cancelAction;
    private Label? _progressLabel;
    private Button? _cancelButton;

    /// <summary>更新当前请求进度。</summary>
    /// <param name="progress">闭区间 [0, 1] 内的归一化进度。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="progress"/> 不是有限值或超出 [0, 1]。</exception>
    public void SetProgress(float progress)
    {
        if (!float.IsFinite(progress) || progress < 0f || progress > 1f)
            throw new ArgumentOutOfRangeException(nameof(progress), progress, "Progress must be finite and within [0, 1].");

        _progress = progress;
        UpdateProgressLabel();
    }

    /// <summary>设置可选的一次性取消操作；传入 null 时隐藏取消按钮。</summary>
    /// <param name="cancelAction">玩家请求取消时调用的操作，或 null。</param>
    /// <remarks>取消操作在 Godot 主线程调用；即使操作抛出异常，按钮也会保持禁用且不会再次调用。</remarks>
    public void SetCancelAction(Action? cancelAction)
    {
        _cancelAction = cancelAction;
        UpdateCancelButton();
    }

    public override void _Ready()
    {
        _progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
        if (!GodotObject.IsInstanceValid(_progressLabel))
            throw new InvalidOperationException("LoadingOverlay 缺少进度标签引用。");
        _cancelButton = GetNodeOrNull<Button>(CancelButtonPath);
        if (!GodotObject.IsInstanceValid(_cancelButton))
            throw new InvalidOperationException("LoadingOverlay 缺少取消按钮引用。");

        _cancelButton.Pressed += OnCancelPressed;
        UpdateProgressLabel();
        UpdateCancelButton();
        if (_cancelButton.Visible)
            _cancelButton.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_cancelButton))
            _cancelButton!.Pressed -= OnCancelPressed;

        _cancelAction = null;
        _progressLabel = null;
        _cancelButton = null;
    }

    private void OnCancelPressed()
    {
        Action? cancelAction = _cancelAction;
        if (cancelAction == null)
            return;

        _cancelAction = null;
        _cancelButton!.Disabled = true;
        cancelAction();
    }

    private void UpdateProgressLabel()
    {
        if (GodotObject.IsInstanceValid(_progressLabel))
            _progressLabel!.Text = $"Loading {Mathf.RoundToInt(_progress * 100f)}%";
    }

    private void UpdateCancelButton()
    {
        if (!GodotObject.IsInstanceValid(_cancelButton))
            return;

        _cancelButton!.Visible = _cancelAction != null;
        _cancelButton.Disabled = _cancelAction == null;
    }
}
