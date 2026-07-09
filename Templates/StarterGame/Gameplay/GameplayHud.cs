using System;
using Godot;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>StarterGame 的游戏 HUD，负责一局点击挑战。</summary>
public sealed partial class GameplayHud : Control
{
    private Label? _timeLabel;
    private Button? _clickButton;
    private StarterGameConfig? _config;
    private double _timeRemaining;
    private int _displayedTenths = -1;
    private int _score;
    private bool _finishing;

    [Export] public NodePath TimeLabelPath { get; set; } = null!;
    [Export] public NodePath ClickButtonPath { get; set; } = null!;

    public override void _Ready()
    {
        _timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
        _clickButton = GetNodeOrNull<Button>(ClickButtonPath);
        if (!IsInstanceValid(_timeLabel) || !IsInstanceValid(_clickButton))
            throw new InvalidOperationException("GameplayHud 缺少必要的导出节点引用。");

        _config = ConfigHub.Load<StarterGameConfig>(StarterGameKeys.Config);
        _timeRemaining = _config.RoundDurationSeconds;
        _clickButton.Pressed += OnClickPressed;
        RefreshTimeLabel();
        EventChannel.Emit(new StarterScoreChangedEvent(_score));
    }

    public override void _Process(double delta)
    {
        if (_finishing)
            return;

        _timeRemaining = Math.Max(0d, _timeRemaining - delta);
        RefreshTimeLabel();
        if (_timeRemaining <= 0d)
            FinishRun();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_clickButton))
            _clickButton.Pressed -= OnClickPressed;

        _timeLabel = null;
        _clickButton = null;
        _config = null;
    }

    private async void OnClickPressed()
    {
        if (_finishing || _config is null)
            return;

        _score += _config.ScorePerClick;
        EventChannel.Emit(new StarterScoreChangedEvent(_score));

        try
        {
            _ = await Services.Get<IAudioService>().PlaySfxAsync(StarterGameKeys.ClickSfx);
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(GameplayHud), StarterGameKeys.ClickSfx.Value);
        }
    }

    private void FinishRun()
    {
        _finishing = true;
        _clickButton!.Disabled = true;

        try
        {
            EventChannel.Emit(new StarterRunFinishedEvent(_score));
        }
        catch (Exception exception)
        {
            _timeLabel!.Text = $"结算失败：{exception.Message}";
            ErrorHub.Report(exception, nameof(GameplayHud));
        }
    }

    private void RefreshTimeLabel()
    {
        int tenths = Mathf.CeilToInt(_timeRemaining * 10d);
        if (tenths == _displayedTenths)
            return;

        _displayedTenths = tenths;
        _timeLabel!.Text = $"剩余时间：{tenths / 10d:0.0}";
    }
}
