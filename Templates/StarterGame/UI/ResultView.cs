using System;
using Godot;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>StarterGame 结算界面。</summary>
public sealed partial class ResultView : Control
{
    private Label? _scoreLabel;
    private Label? _bestScoreLabel;
    private Button? _retryButton;
    private Button? _menuButton;

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
            throw new InvalidOperationException("ResultView 缺少必要的导出节点引用。");
        }

        SaveLoadResult<StarterSaveData> loaded =
            Services.Get<ISaveService>().Load(StarterGameKeys.SaveSlot, StarterGameKeys.SaveCodec);
        StarterSaveData data = loaded.HasValue ? loaded.Value : new StarterSaveData();
        _scoreLabel.Text = $"本局得分：{data.LastScore}";
        _bestScoreLabel.Text = $"最高分：{data.BestScore}    已玩：{data.GamesPlayed} 局";

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

    private void OnRetryPressed()
    {
        NotifyCurrentProcedure(static procedure => procedure.Retry(), "通知 ResultProcedure 再来一局失败");
    }

    private void OnMenuPressed()
    {
        NotifyCurrentProcedure(static procedure => procedure.ReturnToMenu(), "通知 ResultProcedure 返回主菜单失败");
    }

    private void NotifyCurrentProcedure(Action<ResultProcedure> action, string errorContext)
    {
        SetButtonsDisabled(true);
        try
        {
            if (Services.Get<IProcedureService>().Current is not ResultProcedure procedure)
                throw new InvalidOperationException("当前流程不是 ResultProcedure，不能处理结算操作。");

            action(procedure);
        }
        catch (Exception exception)
        {
            SetButtonsDisabled(false);
            ErrorHub.Report(exception, nameof(ResultView), errorContext);
        }
    }

    private void SetButtonsDisabled(bool disabled)
    {
        if (IsInstanceValid(_retryButton))
            _retryButton.Disabled = disabled;
        if (IsInstanceValid(_menuButton))
            _menuButton.Disabled = disabled;
    }
}
