using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 在主场景异步切换期间显示 <see cref="ISceneService.Progress"/> 的 Loading Overlay。
/// <para>调用方负责在场景切换结束后通过 <see cref="IUiService.TryClose(Control)"/> 关闭该界面。</para>
/// </summary>
public sealed partial class LoadingOverlay : Control
{
    [Export] public NodePath ProgressLabelPath { get; set; } = null!;

    private Label? _progressLabel;
    private ISceneService? _scenes;

    public override void _Ready()
    {
        _progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
        if (!GodotObject.IsInstanceValid(_progressLabel))
            throw new InvalidOperationException("LoadingOverlay 缺少进度标签引用。");

        _scenes = Services.Get<ISceneService>();
        UpdateProgress();
    }

    public override void _Process(double delta)
    {
        UpdateProgress();
    }

    public override void _ExitTree()
    {
        _progressLabel = null;
        _scenes = null;
    }

    private void UpdateProgress()
    {
        if (!GodotObject.IsInstanceValid(_progressLabel) || _scenes == null)
            return;

        _progressLabel!.Text = $"Loading {Mathf.RoundToInt(_scenes.Progress * 100f)}%";
    }
}
