using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// Starter Template 的主菜单 View。
/// <para>该 View 仅负责界面呈现；玩家按钮将由后续阶段通过业务事件通知当前 Procedure。</para>
/// </summary>
public sealed partial class MainMenuView : Control
{
    [Export] public NodePath StartButtonPath { get; set; } = null!;
    [Export] public NodePath SettingsButtonPath { get; set; } = null!;

    private Button? _startButton;
    private Button? _settingsButton;

    public override void _Ready()
    {
        _startButton = GetNodeOrNull<Button>(StartButtonPath);
        if (!GodotObject.IsInstanceValid(_startButton))
            throw new InvalidOperationException("MainMenuView 缺少开始按钮引用。");
        _settingsButton = GetNodeOrNull<Button>(SettingsButtonPath);
        if (!GodotObject.IsInstanceValid(_settingsButton))
            throw new InvalidOperationException("MainMenuView 缺少设置按钮引用。");

        _startButton.Pressed += OnStartPressed;
        _settingsButton.Pressed += OnSettingsPressed;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_startButton))
            _startButton!.Pressed -= OnStartPressed;
        if (GodotObject.IsInstanceValid(_settingsButton))
            _settingsButton!.Pressed -= OnSettingsPressed;

        _startButton = null;
        _settingsButton = null;
    }

    private void OnStartPressed()
    {
        _startButton!.Disabled = true;
        EventChannel.Emit<StartGameSelectedEvent>();
    }

    private void OnSettingsPressed() => EventChannel.Emit<SettingsSelectedEvent>();
}
