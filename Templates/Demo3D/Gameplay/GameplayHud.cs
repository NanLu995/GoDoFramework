using System;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>显示当前收集进度和操作提示的 HUD。</summary>
public sealed partial class GameplayHud : Control
{
    [Export] public NodePath ProgressLabelPath { get; set; } = null!;

    private Label? _progressLabel;

    public override void _Ready()
    {
        _progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
        if (!GodotObject.IsInstanceValid(_progressLabel))
            throw new InvalidOperationException("GameplayHud 缺少进度标签引用。");

        EventChannel.Bind<CollectionProgressChangedEvent>(this, OnCollectionProgressChanged);
    }

    private void OnCollectionProgressChanged(CollectionProgressChangedEvent evt)
    {
        _progressLabel!.Text = $"能量核心：{evt.Current} / {evt.Total}";
    }
}
