using System;
using Godot;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>StarterGame 主菜单主场景。</summary>
public sealed partial class MainMenuScene : Control
{
    private Label? _bestScoreLabel;
    private HSlider? _masterVolumeSlider;
    private Button? _startButton;
    private Button? _saveSettingsButton;
    private Button? _clearSaveButton;
    private Button? _quitButton;
    private ISettingsService? _settings;

    [Export] public NodePath BestScoreLabelPath { get; set; } = null!;
    [Export] public NodePath MasterVolumeSliderPath { get; set; } = null!;
    [Export] public NodePath StartButtonPath { get; set; } = null!;
    [Export] public NodePath SaveSettingsButtonPath { get; set; } = null!;
    [Export] public NodePath ClearSaveButtonPath { get; set; } = null!;
    [Export] public NodePath QuitButtonPath { get; set; } = null!;

    public override void _Ready()
    {
        _bestScoreLabel = GetNodeOrNull<Label>(BestScoreLabelPath);
        _masterVolumeSlider = GetNodeOrNull<HSlider>(MasterVolumeSliderPath);
        _startButton = GetNodeOrNull<Button>(StartButtonPath);
        _saveSettingsButton = GetNodeOrNull<Button>(SaveSettingsButtonPath);
        _clearSaveButton = GetNodeOrNull<Button>(ClearSaveButtonPath);
        _quitButton = GetNodeOrNull<Button>(QuitButtonPath);

        if (!IsInstanceValid(_bestScoreLabel) ||
            !IsInstanceValid(_masterVolumeSlider) ||
            !IsInstanceValid(_startButton) ||
            !IsInstanceValid(_saveSettingsButton) ||
            !IsInstanceValid(_clearSaveButton) ||
            !IsInstanceValid(_quitButton))
        {
            throw new InvalidOperationException("MainMenuScene 缺少必要的导出节点引用。");
        }

        _settings = Services.Get<ISettingsService>();
        _masterVolumeSlider.Value = _settings.Current.MasterVolume;
        RefreshBestScore();

        _masterVolumeSlider.ValueChanged += OnMasterVolumeChanged;
        _startButton.Pressed += OnStartPressed;
        _saveSettingsButton.Pressed += OnSaveSettingsPressed;
        _clearSaveButton.Pressed += OnClearSavePressed;
        _quitButton.Pressed += OnQuitPressed;
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_masterVolumeSlider))
            _masterVolumeSlider.ValueChanged -= OnMasterVolumeChanged;
        if (IsInstanceValid(_startButton))
            _startButton.Pressed -= OnStartPressed;
        if (IsInstanceValid(_saveSettingsButton))
            _saveSettingsButton.Pressed -= OnSaveSettingsPressed;
        if (IsInstanceValid(_clearSaveButton))
            _clearSaveButton.Pressed -= OnClearSavePressed;
        if (IsInstanceValid(_quitButton))
            _quitButton.Pressed -= OnQuitPressed;

        _bestScoreLabel = null;
        _masterVolumeSlider = null;
        _startButton = null;
        _saveSettingsButton = null;
        _clearSaveButton = null;
        _quitButton = null;
        _settings = null;
    }

    private void RefreshBestScore()
    {
        SaveLoadResult<StarterSaveData> result =
            Services.Get<ISaveService>().Load(StarterGameKeys.SaveSlot, StarterGameKeys.SaveCodec);
        _bestScoreLabel!.Text = result.HasValue
            ? $"最高分：{result.Value.BestScore}    已玩：{result.Value.GamesPlayed} 局"
            : "最高分：0    已玩：0 局";
    }

    private void OnMasterVolumeChanged(double value)
    {
        _settings?.SetMasterVolume((float)value);
    }

    private void OnStartPressed()
    {
        SetButtonsDisabled(true);
        try
        {
            if (Services.Get<IProcedureService>().Current is not MainMenuProcedure procedure)
                throw new InvalidOperationException("当前流程不是 MainMenuProcedure，不能开始游戏。");

            procedure.StartGame();
        }
        catch (Exception exception)
        {
            SetButtonsDisabled(false);
            ErrorHub.Report(exception, nameof(MainMenuScene), "通知 MainMenuProcedure 开始游戏失败");
        }
    }

    private void OnSaveSettingsPressed()
    {
        try
        {
            _settings?.Save();
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(MainMenuScene), "保存设置失败");
        }
    }

    private void OnClearSavePressed()
    {
        try
        {
            Services.Get<ISaveService>().Delete(StarterGameKeys.SaveSlot);
            RefreshBestScore();
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(MainMenuScene), "清除存档失败");
        }
    }

    private void OnQuitPressed()
    {
        _quitButton!.Disabled = true;
        GetTree().CreateTimer(0.01d).Timeout += () => GetTree().Quit();
    }

    private void SetButtonsDisabled(bool disabled)
    {
        if (IsInstanceValid(_startButton))
            _startButton.Disabled = disabled;
        if (IsInstanceValid(_clearSaveButton))
            _clearSaveButton.Disabled = disabled;
        if (IsInstanceValid(_quitButton))
            _quitButton.Disabled = disabled;
    }
}
