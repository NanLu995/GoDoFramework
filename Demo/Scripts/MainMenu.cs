using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Demo;

/// <summary>点击挑战 Demo 的最小主菜单。</summary>
public sealed partial class MainMenu : Control
{
    private static readonly ResourceKey ClickGameKey =
        ResourceKey.Create("res://Demo/Scenes/ClickGame.tscn");
    private static readonly ResourceKey BgmKey =
        ResourceKey.Create("res://Demo/Audio/DemoBgm.tres");
    private static readonly SaveSlot DemoSlot = SaveSlot.Create("demo_progress");
    private static readonly DemoSaveCodec SaveCodec = new();

    private Label? _bestScoreLabel;
    private HSlider? _masterVolumeSlider;
    private Button? _startButton;
    private Button? _saveSettingsButton;
    private Button? _quitButton;
    private ISettingsService? _settings;

    [Export] public NodePath BestScoreLabelPath { get; set; } = null!;
    [Export] public NodePath MasterVolumeSliderPath { get; set; } = null!;
    [Export] public NodePath StartButtonPath { get; set; } = null!;
    [Export] public NodePath SaveSettingsButtonPath { get; set; } = null!;
    [Export] public NodePath QuitButtonPath { get; set; } = null!;

    public override void _Ready()
    {
        _bestScoreLabel = GetNodeOrNull<Label>(BestScoreLabelPath);
        _masterVolumeSlider = GetNodeOrNull<HSlider>(MasterVolumeSliderPath);
        _startButton = GetNodeOrNull<Button>(StartButtonPath);
        _saveSettingsButton = GetNodeOrNull<Button>(SaveSettingsButtonPath);
        _quitButton = GetNodeOrNull<Button>(QuitButtonPath);

        if (!IsInstanceValid(_bestScoreLabel) ||
            !IsInstanceValid(_masterVolumeSlider) ||
            !IsInstanceValid(_startButton) ||
            !IsInstanceValid(_saveSettingsButton) ||
            !IsInstanceValid(_quitButton))
        {
            throw new InvalidOperationException("MainMenu 场景缺少必要的导出节点引用。");
        }

        LoadBestScore();
        _settings = Services.Get<ISettingsService>();
        _masterVolumeSlider.Value = _settings.Current.MasterVolume;
        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        _startButton.Pressed += OnStartPressed;
        _saveSettingsButton.Pressed += OnSaveSettingsPressed;
        _quitButton.Pressed += OnQuitPressed;
        PlayBgm();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_masterVolumeSlider))
            _masterVolumeSlider.ValueChanged -= OnMasterVolumeChanged;
        if (IsInstanceValid(_startButton))
            _startButton.Pressed -= OnStartPressed;
        if (IsInstanceValid(_saveSettingsButton))
            _saveSettingsButton.Pressed -= OnSaveSettingsPressed;
        if (IsInstanceValid(_quitButton))
            _quitButton.Pressed -= OnQuitPressed;

        _bestScoreLabel = null;
        _masterVolumeSlider = null;
        _startButton = null;
        _saveSettingsButton = null;
        _quitButton = null;
        _settings = null;
    }

    private void LoadBestScore()
    {
        SaveLoadResult<DemoSaveData> result =
            Services.Get<ISaveService>().Load(DemoSlot, SaveCodec);
        _bestScoreLabel!.Text = result.HasValue
            ? $"最高分：{result.Value.BestScore}"
            : "最高分：0";
    }

    private void OnMasterVolumeChanged(double value)
    {
        _settings?.SetMasterVolume((float)value);
    }

    private async void OnStartPressed()
    {
        _startButton!.Disabled = true;
        try
        {
            await Services.Get<ISceneService>().ChangeAsync(ClickGameKey);
        }
        catch (Exception exception)
        {
            _startButton.Disabled = false;
            ErrorHub.Report(exception, nameof(MainMenu), ClickGameKey.Value);
        }
    }

    private void OnSaveSettingsPressed()
    {
        _settings?.Save();
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private async void PlayBgm()
    {
        try
        {
            await Services.Get<IAudioService>().PlayBgmAsync(BgmKey);
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(MainMenu), BgmKey.Value);
        }
    }
}
