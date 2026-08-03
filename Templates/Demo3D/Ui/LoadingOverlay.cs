using System;
using Godot;

#nullable enable

namespace Demo3D;

/// <summary>显示 Demo3D 当前主场景请求的加载进度。</summary>
public sealed partial class LoadingOverlay : Control
{
    private Label? _progressLabel;
    private ProgressBar? _progressBar;

    /// <summary>进度文本节点路径。</summary>
    [Export] public NodePath ProgressLabelPath { get; set; } = null!;

    /// <summary>进度条节点路径。</summary>
    [Export] public NodePath ProgressBarPath { get; set; } = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        _progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
        _progressBar = GetNodeOrNull<ProgressBar>(ProgressBarPath);
        if (!GodotObject.IsInstanceValid(_progressLabel) ||
            !GodotObject.IsInstanceValid(_progressBar))
        {
            throw new InvalidOperationException("LoadingOverlay 缺少进度文本或进度条引用。");
        }

        SetProgress(0f);
    }

    /// <summary>更新当前场景请求的归一化加载进度。</summary>
    public void SetProgress(float progress)
    {
        float normalized = Mathf.Clamp(progress, 0f, 1f);
        if (GodotObject.IsInstanceValid(_progressBar))
            _progressBar!.Value = normalized * 100.0;
        if (GodotObject.IsInstanceValid(_progressLabel))
            _progressLabel!.Text = $"Loading {Mathf.RoundToInt(normalized * 100f)}%";
    }
}
