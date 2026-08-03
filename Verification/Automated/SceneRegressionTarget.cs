using System;
using Godot;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>SceneService 回归使用的最小目标场景。</summary>
public sealed partial class SceneRegressionTarget : Node
{
    internal static Action? ConstructedAction { get; set; }
    internal static Action? ReadyAction { get; set; }

    public SceneRegressionTarget()
    {
        ConstructedAction?.Invoke();
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        ReadyAction?.Invoke();
    }
}
