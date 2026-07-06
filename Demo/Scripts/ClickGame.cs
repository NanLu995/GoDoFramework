using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Demo;

/// <summary>点击挑战单局流程。</summary>
public sealed partial class ClickGame : Control
{
    private static readonly ResourceKey ConfigKey =
        ResourceKey.Create("res://Demo/Config/ClickGameConfig.tres");
    private static readonly ResourceKey ResultKey =
        ResourceKey.Create("res://Demo/Scenes/Result.tscn");
    private static readonly ResourceKey BgmKey =
        ResourceKey.Create("res://Demo/Audio/DemoBgm.tres");
    private static readonly ResourceKey ClickSfxKey =
        ResourceKey.Create("res://Demo/Audio/ClickSfx.tres");
    private static readonly SaveSlot DemoSlot = SaveSlot.Create("demo_progress");
    private static readonly DemoSaveCodec SaveCodec = new();

    private Label? _timeLabel;
    private Label? _scoreLabel;
    private Button? _clickButton;
    private ClickGameConfig? _config;
    private double _timeRemaining;
    private int _displayedTenths = -1;
    private int _score;
    private bool _finishing;

    [Export] public NodePath TimeLabelPath { get; set; } = null!;
    [Export] public NodePath ScoreLabelPath { get; set; } = null!;
    [Export] public NodePath ClickButtonPath { get; set; } = null!;

    public override void _Ready()
    {
        _timeLabel = GetNodeOrNull<Label>(TimeLabelPath);
        _scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
        _clickButton = GetNodeOrNull<Button>(ClickButtonPath);
        if (!IsInstanceValid(_timeLabel) ||
            !IsInstanceValid(_scoreLabel) ||
            !IsInstanceValid(_clickButton))
        {
            throw new InvalidOperationException("ClickGame 场景缺少必要的导出节点引用。");
        }

        _config = ConfigHub.Load<ClickGameConfig>(ConfigKey);
        _timeRemaining = _config.RoundDurationSeconds;
        _clickButton.Pressed += OnClickPressed;
        RefreshTimeLabel();
        RefreshScoreLabel();
        PlayBgm();
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
        _scoreLabel = null;
        _clickButton = null;
        _config = null;
    }

    private async void OnClickPressed()
    {
        if (_finishing || _config is null)
            return;

        _score += _config.ScorePerClick;
        RefreshScoreLabel();

        try
        {
            _ = await Services.Get<IAudioService>().PlaySfxAsync(ClickSfxKey);
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(ClickGame), ClickSfxKey.Value);
        }
    }

    private async void FinishRun()
    {
        _finishing = true;
        _clickButton!.Disabled = true;

        try
        {
            ISaveService saves = Services.Get<ISaveService>();
            SaveLoadResult<DemoSaveData> loaded = saves.Load(DemoSlot, SaveCodec);
            DemoSaveData data = loaded.HasValue ? loaded.Value : new DemoSaveData();
            data.LastScore = _score;
            data.BestScore = Math.Max(data.BestScore, _score);
            data.GamesPlayed++;
            saves.Save(DemoSlot, data, DemoSaveCodec.CurrentDataVersion, SaveCodec);

            EventChannel.Emit(new RunFinishedEvent(_score));
            await Services.Get<ISceneService>().ChangeAsync(ResultKey);
        }
        catch (Exception exception)
        {
            _timeLabel!.Text = $"结算失败：{exception.Message}";
            ErrorHub.Report(exception, nameof(ClickGame));
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

    private void RefreshScoreLabel()
    {
        _scoreLabel!.Text = $"得分：{_score}";
    }

    private async void PlayBgm()
    {
        try
        {
            await Services.Get<IAudioService>().PlayBgmAsync(BgmKey);
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(ClickGame), BgmKey.Value);
        }
    }
}
