using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>用于验证 View 入栈、恢复和模态阻挡的交互界面。</summary>
public sealed partial class UiVerificationPage : Control
{
    private static readonly ResourceKey ViewBKey =
        ResourceKey.Create("res://Verification/UI/UiVerificationPageB.tscn");
    private static readonly ResourceKey ModalKey =
        ResourceKey.Create("res://Verification/UI/UiVerificationModal.tscn");
    private static readonly ResourceKey TargetSceneKey =
        ResourceKey.Create("res://Verification/UI/UiVerificationTargetScene.tscn");
    private static readonly ResourceKey MissingViewKey =
        ResourceKey.Create("res://Verification/UI/MissingUiVerificationView.tscn");
    private static readonly ResourceKey InvalidRootKey =
        ResourceKey.Create("res://Verification/UI/UiVerificationInvalidRoot.tscn");

    private Button? _openPageButton;
    private Button? _openModalButton;
    private Button? _changeSceneButton;
    private Button? _failureButton;
    private Button? _closeButton;
    private Button? _counterButton;
    private Label? _counterLabel;
    private Label? _failureLabel;
    private int _clickCount;

    [Export] public NodePath OpenPageButtonPath { get; set; } = null!;
    [Export] public NodePath OpenModalButtonPath { get; set; } = null!;
    [Export] public NodePath ChangeSceneButtonPath { get; set; } = null!;
    [Export] public NodePath FailureButtonPath { get; set; } = null!;
    [Export] public NodePath CloseButtonPath { get; set; } = null!;
    [Export] public NodePath CounterButtonPath { get; set; } = null!;
    [Export] public NodePath CounterLabelPath { get; set; } = null!;
    [Export] public NodePath FailureLabelPath { get; set; } = null!;

    public override void _Ready()
    {
        _openPageButton = GetNodeOrNull<Button>(OpenPageButtonPath);
        _openModalButton = GetNodeOrNull<Button>(OpenModalButtonPath);
        _changeSceneButton = GetNodeOrNull<Button>(ChangeSceneButtonPath);
        _failureButton = GetNodeOrNull<Button>(FailureButtonPath);
        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        _counterButton = GetNodeOrNull<Button>(CounterButtonPath);
        _counterLabel = GetNodeOrNull<Label>(CounterLabelPath);
        _failureLabel = GetNodeOrNull<Label>(FailureLabelPath);
        if (!IsInstanceValid(_openPageButton) ||
            !IsInstanceValid(_openModalButton) ||
            !IsInstanceValid(_changeSceneButton) ||
            !IsInstanceValid(_failureButton) ||
            !IsInstanceValid(_closeButton) ||
            !IsInstanceValid(_counterButton) ||
            !IsInstanceValid(_counterLabel) ||
            !IsInstanceValid(_failureLabel))
        {
            throw new InvalidOperationException("UiVerificationPage 缺少必要的导出节点引用。");
        }

        _openPageButton.Pressed += OnOpenPagePressed;
        _openModalButton.Pressed += OnOpenModalPressed;
        _changeSceneButton.Pressed += OnChangeScenePressed;
        _failureButton.Pressed += OnFailurePressed;
        _closeButton.Pressed += OnClosePressed;
        _counterButton.Pressed += OnCounterPressed;
        RefreshCounter();
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_openPageButton))
            _openPageButton.Pressed -= OnOpenPagePressed;
        if (IsInstanceValid(_openModalButton))
            _openModalButton.Pressed -= OnOpenModalPressed;
        if (IsInstanceValid(_changeSceneButton))
            _changeSceneButton.Pressed -= OnChangeScenePressed;
        if (IsInstanceValid(_failureButton))
            _failureButton.Pressed -= OnFailurePressed;
        if (IsInstanceValid(_closeButton))
            _closeButton.Pressed -= OnClosePressed;
        if (IsInstanceValid(_counterButton))
            _counterButton.Pressed -= OnCounterPressed;

        _openPageButton = null;
        _openModalButton = null;
        _changeSceneButton = null;
        _failureButton = null;
        _closeButton = null;
        _counterButton = null;
        _counterLabel = null;
        _failureLabel = null;
    }

    private void OnOpenPagePressed() => Services.Get<IUiService>().Open(ViewBKey, UiLayer.View);

    private void OnOpenModalPressed() => Services.Get<IUiService>().Open(ModalKey, UiLayer.Modal);

    private async void OnChangeScenePressed()
    {
        _changeSceneButton!.Disabled = true;
        try
        {
            await Services.Get<ISceneService>().ChangeAsync(TargetSceneKey);
        }
        catch
        {
            _changeSceneButton.Disabled = false;
            throw;
        }
    }

    private void OnClosePressed() => Services.Get<IUiService>().Close(this);

    private void OnFailurePressed()
    {
        IUiService ui = Services.Get<IUiService>();
        bool missingPassed = ThrowsUiOpenException(() => ui.Open(MissingViewKey, UiLayer.View));
        bool invalidRootPassed = ThrowsUiOpenException(() => ui.Open(InvalidRootKey, UiLayer.View));

        var unmanaged = new Control();
        bool invalidClosePassed;
        try
        {
            ui.Close(unmanaged);
            invalidClosePassed = false;
        }
        catch (InvalidOperationException)
        {
            invalidClosePassed = true;
        }
        finally
        {
            unmanaged.Free();
        }

        _failureLabel!.Text = missingPassed && invalidRootPassed && invalidClosePassed
            ? "失败语义通过；当前 View 状态保持"
            : $"失败：缺失={missingPassed}，错误根={invalidRootPassed}，非法关闭={invalidClosePassed}";
    }

    private static bool ThrowsUiOpenException(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (UiOpenException)
        {
            return true;
        }
    }

    private void OnCounterPressed()
    {
        _clickCount++;
        RefreshCounter();
    }

    private void RefreshCounter()
    {
        _counterLabel!.Text = $"当前 View 按钮点击次数：{_clickCount}";
    }
}
