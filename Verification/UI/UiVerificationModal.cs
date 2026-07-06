using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>用于验证模态关闭顺序和嵌套模态的交互界面。</summary>
public sealed partial class UiVerificationModal : Control
{
    private static readonly ResourceKey ModalKey =
        ResourceKey.Create("res://Verification/UI/UiVerificationModal.tscn");

    private Button? _openModalButton;
    private Button? _closeButton;

    [Export] public NodePath OpenModalButtonPath { get; set; } = null!;
    [Export] public NodePath CloseButtonPath { get; set; } = null!;

    public override void _Ready()
    {
        _openModalButton = GetNodeOrNull<Button>(OpenModalButtonPath);
        _closeButton = GetNodeOrNull<Button>(CloseButtonPath);
        if (!IsInstanceValid(_openModalButton) || !IsInstanceValid(_closeButton))
            throw new InvalidOperationException("UiVerificationModal 缺少必要的导出节点引用。");

        _openModalButton.Pressed += OnOpenModalPressed;
        _closeButton.Pressed += OnClosePressed;
    }

    public override void _ExitTree()
    {
        if (IsInstanceValid(_openModalButton))
            _openModalButton.Pressed -= OnOpenModalPressed;
        if (IsInstanceValid(_closeButton))
            _closeButton.Pressed -= OnClosePressed;

        _openModalButton = null;
        _closeButton = null;
    }

    private void OnOpenModalPressed() => Services.Get<IUiService>().OpenModal(ModalKey);

    private void OnClosePressed() => Services.Get<IUiService>().Close(this);
}
