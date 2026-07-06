using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Demo;

/// <summary>显示最近一局结果并提供后续场景入口。</summary>
public sealed partial class ResultScreen : Control
{
    private static readonly ResourceKey ClickGameKey =
        ResourceKey.Create("res://Demo/Scenes/ClickGame.tscn");
    private static readonly ResourceKey MainMenuKey =
        ResourceKey.Create("res://Demo/Scenes/MainMenu.tscn");
    private static readonly SaveSlot DemoSlot = SaveSlot.Create("demo_progress");
    private static readonly DemoSaveCodec SaveCodec = new();

    private Label? _scoreLabel;
    private Label? _bestScoreLabel;
    private Button? _retryButton;
    private Button? _menuButton;
    private bool _changingScene;

    [Export] public NodePath ScoreLabelPath { get; set; } = null!;
    [Export] public NodePath BestScoreLabelPath { get; set; } = null!;
    [Export] public NodePath RetryButtonPath { get; set; } = null!;
    [Export] public NodePath MenuButtonPath { get; set; } = null!;

    public override void _Ready()
    {
        _scoreLabel = GetNodeOrNull<Label>(ScoreLabelPath);
        _bestScoreLabel = GetNodeOrNull<Label>(BestScoreLabelPath);
        _retryButton = GetNodeOrNull<Button>(RetryButtonPath);
        _menuButton = GetNodeOrNull<Button>(MenuButtonPath);
        if (!IsInstanceValid(_scoreLabel) ||
            !IsInstanceValid(_bestScoreLabel) ||
            !IsInstanceValid(_retryButton) ||
            !IsInstanceValid(_menuButton))
        {
            throw new InvalidOperationException("Result 场景缺少必要的导出节点引用。");
        }

        SaveLoadResult<DemoSaveData> loaded =
            Services.Get<ISaveService>().Load(DemoSlot, SaveCodec);
        DemoSaveData data = loaded.HasValue ? loaded.Value : new DemoSaveData();
        _scoreLabel.Text = $"本局得分：{data.LastScore}";
        _bestScoreLabel.Text = $"最高分：{data.BestScore}    已玩：{data.GamesPlayed} 局";
        Services.Get<IAudioService>().StopBgm();

        _retryButton.Pressed += OnRetryPressed;
        _menuButton.Pressed += OnMenuPressed;
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_retryButton))
            _retryButton.Pressed -= OnRetryPressed;
        if (IsInstanceValid(_menuButton))
            _menuButton.Pressed -= OnMenuPressed;
        _scoreLabel = null;
        _bestScoreLabel = null;
        _retryButton = null;
        _menuButton = null;
    }

    private void OnRetryPressed() => ChangeScene(ClickGameKey);

    private void OnMenuPressed() => ChangeScene(MainMenuKey);

    private async void ChangeScene(ResourceKey key)
    {
        if (_changingScene)
            return;

        _changingScene = true;
        _retryButton!.Disabled = true;
        _menuButton!.Disabled = true;
        try
        {
            await Services.Get<ISceneService>().ChangeAsync(key);
        }
        catch (Exception exception)
        {
            _changingScene = false;
            _retryButton.Disabled = false;
            _menuButton.Disabled = false;
            ErrorHub.Report(exception, nameof(ResultScreen), key.Value);
        }
    }
}
