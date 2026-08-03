using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// 与当前 Gameplay 主场景共存的最小 Scene UI。
/// <para>该控件只发送暂停意图，不包含暂停策略或具体玩法状态。</para>
/// </summary>
public sealed partial class GameplayHud : Control
{
    [Export] public NodePath PauseButtonPath { get; set; } = null!;
    [Export] public NodePath ExampleSaveButtonPath { get; set; } = null!;

    private Button? _pauseButton;
    private Button? _exampleSaveButton;

    public override void _Ready()
    {
        _pauseButton = GetNodeOrNull<Button>(PauseButtonPath);
        if (!GodotObject.IsInstanceValid(_pauseButton))
            throw new InvalidOperationException("GameplayHud 缺少暂停按钮引用。");
        _exampleSaveButton = GetNodeOrNull<Button>(ExampleSaveButtonPath);
        if (!GodotObject.IsInstanceValid(_exampleSaveButton))
            throw new InvalidOperationException("GameplayHud 缺少示例存档按钮引用。");

        _pauseButton.Pressed += OnPausePressed;
        _exampleSaveButton.Pressed += OnExampleSavePressed;
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_pauseButton))
            _pauseButton!.Pressed -= OnPausePressed;
        if (GodotObject.IsInstanceValid(_exampleSaveButton))
            _exampleSaveButton!.Pressed -= OnExampleSavePressed;

        _pauseButton = null;
        _exampleSaveButton = null;
    }

    private void OnPausePressed() => EventChannel.Emit<PauseSelectedEvent>();

    private void OnExampleSavePressed() => EventChannel.Emit<ExampleSaveSelectedEvent>();
}
