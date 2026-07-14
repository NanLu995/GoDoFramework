using System;
using Godot;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>
/// 通过 EventChannel 接收分数变化并刷新显示。
/// </summary>
public sealed partial class ScorePanel : Control
{
    private Label? _scoreLabel;

    [Export] public NodePath ScoreLabelPath { get; set; } = null!;

    public override void _Ready()
    {
        _scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
        if (!IsInstanceValid(_scoreLabel))
            throw new InvalidOperationException("ScorePanel 缺少分数 Label。");

        _scoreLabel.Text = "得分：0";
        EventChannel.Bind<StarterScoreChangedEvent>(this, OnScoreChanged);
    }

    public override void _ExitTree()
    {
        _scoreLabel = null;
    }

    private void OnScoreChanged(StarterScoreChangedEvent evt)
    {
        if (IsInstanceValid(_scoreLabel))
            _scoreLabel.Text = $"得分：{evt.Score}";
    }
}
