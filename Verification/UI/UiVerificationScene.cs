using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>UI 首版的可交互验证入口。</summary>
public sealed partial class UiVerificationScene : Control
{
    private static readonly ResourceKey ViewAKey =
        ResourceKey.Create("res://Verification/UI/UiVerificationPageA.tscn");
    private static readonly ResourceKey SceneLayerKey =
        ResourceKey.Create("res://Verification/UI/UiVerificationSceneLayer.tscn");
    private static readonly ResourceKey TargetSceneKey =
        ResourceKey.Create("res://Verification/UI/UiVerificationTargetScene.tscn");

    private Button? _openPageButton;
    private Button? _openSceneLayerButton;
    private Button? _changeSceneButton;
    private Button? _backButton;
    private Button? _backgroundButton;
    private Label? _statusLabel;
    private int _backgroundClickCount;

    [Export] public NodePath OpenPageButtonPath { get; set; } = null!;
    [Export] public NodePath OpenSceneLayerButtonPath { get; set; } = null!;
    [Export] public NodePath ChangeSceneButtonPath { get; set; } = null!;
    [Export] public NodePath BackButtonPath { get; set; } = null!;
    [Export] public NodePath BackgroundButtonPath { get; set; } = null!;
    [Export] public NodePath StatusLabelPath { get; set; } = null!;

    public override void _Ready()
    {
        _openPageButton = GetNodeOrNull<Button>(OpenPageButtonPath);
        _openSceneLayerButton = GetNodeOrNull<Button>(OpenSceneLayerButtonPath);
        _changeSceneButton = GetNodeOrNull<Button>(ChangeSceneButtonPath);
        _backButton = GetNodeOrNull<Button>(BackButtonPath);
        _backgroundButton = GetNodeOrNull<Button>(BackgroundButtonPath);
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        if (!IsInstanceValid(_openPageButton) ||
            !IsInstanceValid(_openSceneLayerButton) ||
            !IsInstanceValid(_changeSceneButton) ||
            !IsInstanceValid(_backButton) ||
            !IsInstanceValid(_backgroundButton) ||
            !IsInstanceValid(_statusLabel))
        {
            throw new InvalidOperationException("UiVerificationScene 缺少必要的导出节点引用。");
        }

        _openPageButton.Pressed += OnOpenPagePressed;
        _openSceneLayerButton.Pressed += OnOpenSceneLayerPressed;
        _changeSceneButton.Pressed += OnChangeScenePressed;
        _backButton.Pressed += OnBackPressed;
        _backgroundButton.Pressed += OnBackgroundPressed;
        RefreshStatus("准备就绪");
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_openPageButton))
            _openPageButton.Pressed -= OnOpenPagePressed;
        if (IsInstanceValid(_openSceneLayerButton))
            _openSceneLayerButton.Pressed -= OnOpenSceneLayerPressed;
        if (IsInstanceValid(_changeSceneButton))
            _changeSceneButton.Pressed -= OnChangeScenePressed;
        if (IsInstanceValid(_backButton))
            _backButton.Pressed -= OnBackPressed;
        if (IsInstanceValid(_backgroundButton))
            _backgroundButton.Pressed -= OnBackgroundPressed;

        _openPageButton = null;
        _openSceneLayerButton = null;
        _changeSceneButton = null;
        _backButton = null;
        _backgroundButton = null;
        _statusLabel = null;
    }

    private void OnOpenPagePressed()
    {
        Services.Get<IUiService>().Open(ViewAKey, UiLayer.View);
        RefreshStatus("已打开 View A");
    }

    private void OnOpenSceneLayerPressed()
    {
        Services.Get<IUiService>().Open(SceneLayerKey, UiLayer.Scene);
        RefreshStatus("已打开 Scene 层标记");
    }

    private async void OnChangeScenePressed()
    {
        _changeSceneButton!.Disabled = true;
        try
        {
            await Services.Get<ISceneService>().ChangeAsync(TargetSceneKey);
        }
        catch (Exception exception)
        {
            _changeSceneButton.Disabled = false;
            RefreshStatus($"切换失败：{exception.Message}");
        }
    }

    private void OnBackPressed()
    {
        bool closed = Services.Get<IUiService>().TryGoBack();
        RefreshStatus(closed ? "已关闭顶部 UI" : "返回栈为空");
    }

    private void OnBackgroundPressed()
    {
        _backgroundClickCount++;
        RefreshStatus("底层按钮收到点击");
    }

    private void RefreshStatus(string action)
    {
        _statusLabel!.Text = $"{action}\n底层按钮点击次数：{_backgroundClickCount}";
    }
}
