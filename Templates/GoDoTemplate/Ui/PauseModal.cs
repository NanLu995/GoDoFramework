using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>暂停菜单 Modal，仅向当前 Procedure 发送恢复或返回主菜单意图。</summary>
public sealed partial class PauseModal : Control
{
    [Export] public NodePath ResumeButtonPath { get; set; } = null!;

    /// <summary>获取或设置 Settings 按钮相对当前 Modal 的节点路径。</summary>
    [Export] public NodePath SettingsButtonPath { get; set; } = null!;
    [Export] public NodePath ReturnButtonPath { get; set; } = null!;

    private Button? _resumeButton;
    private Button? _settingsButton;
    private Button? _returnButton;

    public override void _Ready()
    {
        _resumeButton = RequireNode<Button>(ResumeButtonPath, "恢复按钮");
        _settingsButton = RequireNode<Button>(SettingsButtonPath, "设置按钮");
        _returnButton = RequireNode<Button>(ReturnButtonPath, "返回主菜单按钮");
        _resumeButton.Pressed += OnResumePressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _returnButton.Pressed += OnReturnPressed;
        _resumeButton.GrabFocus();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_resumeButton))
            _resumeButton!.Pressed -= OnResumePressed;
        if (GodotObject.IsInstanceValid(_settingsButton))
            _settingsButton!.Pressed -= OnSettingsPressed;
        if (GodotObject.IsInstanceValid(_returnButton))
            _returnButton!.Pressed -= OnReturnPressed;

        _resumeButton = null;
        _settingsButton = null;
        _returnButton = null;
    }

    private void OnResumePressed() => EventChannel.Emit<ResumeSelectedEvent>();

    private void OnSettingsPressed() => EventChannel.Emit<PauseSettingsSelectedEvent>();

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
