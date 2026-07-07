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

    private Button? _openPageButton;
    private Button? _openModalButton;
    private Button? _closeButton;
    private Button? _counterButton;
    private Label? _counterLabel;
    private int _clickCount;

    [Export] public NodePath OpenPageButtonPath { get; set; } = null!;
    [Export] public NodePath OpenModalButtonPath { get; set; } = null!;
    [Export] public NodePath CloseButtonPath { get; set; } = null!;
    [Export] public NodePath CounterButtonPath { get; set; } = null!;
    [Export] public NodePath CounterLabelPath { get; set; } = null!;

    public override void _Ready()
    {
        _openPageButton = GetNodeOrNull<Button>(OpenPageButtonPath);
        _openModalButton = GetNodeOrNull<Button>(OpenModalButtonPath);
        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        _counterButton = GetNodeOrNull<Button>(CounterButtonPath);
        _counterLabel = GetNodeOrNull<Label>(CounterLabelPath);
        if (!IsInstanceValid(_openPageButton) ||
            !IsInstanceValid(_openModalButton) ||
            !IsInstanceValid(_closeButton) ||
            !IsInstanceValid(_counterButton) ||
            !IsInstanceValid(_counterLabel))
        {
            throw new InvalidOperationException("UiVerificationPage 缺少必要的导出节点引用。");
        }

        _openPageButton.Pressed += OnOpenPagePressed;
        _openModalButton.Pressed += OnOpenModalPressed;
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
        if (IsInstanceValid(_closeButton))
            _closeButton.Pressed -= OnClosePressed;
        if (IsInstanceValid(_counterButton))
            _counterButton.Pressed -= OnCounterPressed;

        _openPageButton = null;
        _openModalButton = null;
        _closeButton = null;
        _counterButton = null;
        _counterLabel = null;
    }

    private void OnOpenPagePressed() => Services.Get<IUiService>().Open(ViewBKey, UiLayer.View);

    private void OnOpenModalPressed() => Services.Get<IUiService>().Open(ModalKey, UiLayer.Modal);

    private void OnClosePressed() => Services.Get<IUiService>().Close(this);

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
