using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>暂停菜单 Modal，仅向当前 Procedure 发送恢复或返回主菜单意图。</summary>
public sealed partial class PauseModal : Control
{
    [Export] public NodePath ResumeButtonPath { get; set; } = null!;
    [Export] public NodePath ReturnButtonPath { get; set; } = null!;

    private Button? _resumeButton;
    private Button? _returnButton;

    public override void _Ready()
    {
        _resumeButton = RequireNode<Button>(ResumeButtonPath, "恢复按钮");
        _returnButton = RequireNode<Button>(ReturnButtonPath, "返回主菜单按钮");
        _resumeButton.Pressed += OnResumePressed;
        _returnButton.Pressed += OnReturnPressed;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_resumeButton))
            _resumeButton!.Pressed -= OnResumePressed;
        if (GodotObject.IsInstanceValid(_returnButton))
            _returnButton!.Pressed -= OnReturnPressed;

        _resumeButton = null;
        _returnButton = null;
    }

    private void OnResumePressed() => EventChannel.Emit<ResumeSelectedEvent>();

    private void OnReturnPressed() => EventChannel.Emit<ReturnToMainMenuSelectedEvent>();

    private T RequireNode<T>(NodePath path, string description)
        where T : Node
    {
        T? node = GetNodeOrNull<T>(path);
        if (!GodotObject.IsInstanceValid(node))
            throw new InvalidOperationException($"PauseModal 缺少{description}引用。");

        return node!;
    }
}
