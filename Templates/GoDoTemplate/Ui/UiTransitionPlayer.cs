using System;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDoTemplate;

/// <summary>Describes the visual phase of a template UI transition.</summary>
public enum UiTransitionPhase
{
    /// <summary>The node is mounted and its enter animation is running.</summary>
    Entering,

    /// <summary>The enter animation completed and the target is fully visible.</summary>
    Open,

    /// <summary>The exit animation is running while the UI remains mounted.</summary>
    Exiting,

    /// <summary>The exit animation completed, or the component is outside the scene tree.</summary>
    Closed,
}

/// <summary>
/// Plays a template-local fade and scale transition without changing <c>UiService</c> lifecycle semantics.
/// </summary>
/// <remarks>
/// The owner must keep the UI mounted until <see cref="PlayExitAsync"/> returns <see langword="true"/>.
/// A repeated exit request, or removal from the scene tree during the animation, returns
/// <see langword="false"/>. The target's <see cref="CanvasItem.Modulate"/> and
/// <see cref="Control.Scale"/> are owned by this component while a transition is active.
/// </remarks>
public sealed partial class UiTransitionPlayer : Node
{
    private const float EnterScale = 0.98f;

    private Control? _target;
    private Tween? _tween;
    private TaskCompletionSource<bool>? _exitCompletion;
    private Color _baseModulate;
    private Vector2 _baseScale;
    private bool _hasBaseValues;

    /// <summary>Gets or sets the animated Control path relative to this component.</summary>
    [Export] public NodePath TargetPath { get; set; } = null!;

    /// <summary>Gets or sets the enter duration in seconds. Zero applies the final state immediately.</summary>
    [Export(PropertyHint.Range, "0,1,0.01,or_greater")]
    public double EnterDuration { get; set; } = 0.16;

    /// <summary>Gets or sets the exit duration in seconds. Zero applies the final state immediately.</summary>
    [Export(PropertyHint.Range, "0,1,0.01,or_greater")]
    public double ExitDuration { get; set; } = 0.12;

    /// <summary>Gets the current visual transition phase.</summary>
    public UiTransitionPhase Phase { get; private set; } = UiTransitionPhase.Closed;

    /// <inheritdoc />
    public override void _EnterTree()
    {
        _target = GetNodeOrNull<Control>(TargetPath);
        if (!GodotObject.IsInstanceValid(_target))
            throw new InvalidOperationException($"UiTransitionPlayer cannot find target Control: {TargetPath}");

        if (!_hasBaseValues)
        {
            _baseModulate = _target!.Modulate;
            _baseScale = _target.Scale;
            _hasBaseValues = true;
        }

        _target!.Resized += OnTargetResized;
        OnTargetResized();
        BeginEnter();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_target))
            _target!.Resized -= OnTargetResized;

        StopTween(kill: true);
        _exitCompletion?.TrySetResult(false);
        _exitCompletion = null;
        _target = null;
        Phase = UiTransitionPhase.Closed;
    }

    /// <summary>Plays the exit animation once while the owning UI remains in the scene tree.</summary>
    /// <returns>
    /// A task returning <see langword="true"/> when this request completed the exit animation;
    /// <see langword="false"/> for repeated requests or when the node left the scene tree first.
    /// </returns>
    public Task<bool> PlayExitAsync()
    {
        if (Phase is UiTransitionPhase.Exiting or UiTransitionPhase.Closed)
            return Task.FromResult(false);

        if (!GodotObject.IsInstanceValid(_target) || !IsInsideTree())
            return Task.FromResult(false);

        StopTween(kill: true);
        Phase = UiTransitionPhase.Exiting;
        _exitCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (ExitDuration <= 0.0)
        {
            ApplyExitState();
            CompleteExit();
            return _exitCompletion?.Task ?? Task.FromResult(true);
        }

        Task<bool> completion = _exitCompletion.Task;
        _tween = CreateTransitionTween();
        _tween.TweenProperty(_target, "modulate", WithAlpha(_baseModulate, 0.0f), ExitDuration);
        _tween.TweenProperty(_target, "scale", _baseScale * EnterScale, ExitDuration);
        return completion;
    }

    private void BeginEnter()
    {
        StopTween(kill: true);
        _exitCompletion?.TrySetResult(false);
        _exitCompletion = null;
        Phase = UiTransitionPhase.Entering;
        _target!.Modulate = WithAlpha(_baseModulate, 0.0f);
        _target.Scale = _baseScale * EnterScale;

        if (EnterDuration <= 0.0)
        {
            CompleteEnter();
            return;
        }

        _tween = CreateTransitionTween();
        _tween.TweenProperty(_target, "modulate", _baseModulate, EnterDuration);
        _tween.TweenProperty(_target, "scale", _baseScale, EnterDuration);
    }

    private Tween CreateTransitionTween()
    {
        Tween tween = CreateTween()
            .SetParallel()
            .SetPauseMode(Tween.TweenPauseMode.Process)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.Finished += OnTweenFinished;
        return tween;
    }

    private void OnTweenFinished()
    {
        UiTransitionPhase completedPhase = Phase;
        StopTween(kill: false);
        if (completedPhase == UiTransitionPhase.Entering)
            CompleteEnter();
        else if (completedPhase == UiTransitionPhase.Exiting)
            CompleteExit();
    }

    private void CompleteEnter()
    {
        if (GodotObject.IsInstanceValid(_target))
        {
            _target!.Modulate = _baseModulate;
            _target.Scale = _baseScale;
        }

        Phase = UiTransitionPhase.Open;
    }

    private void ApplyExitState()
    {
        if (!GodotObject.IsInstanceValid(_target))
            return;

        _target!.Modulate = WithAlpha(_baseModulate, 0.0f);
        _target.Scale = _baseScale * EnterScale;
    }

    private void CompleteExit()
    {
        ApplyExitState();
        Phase = UiTransitionPhase.Closed;
        TaskCompletionSource<bool>? completion = _exitCompletion;
        _exitCompletion = null;
        completion?.TrySetResult(true);
    }

    private void StopTween(bool kill)
    {
        if (_tween is null)
            return;

        _tween.Finished -= OnTweenFinished;
        if (kill && _tween.IsValid())
            _tween.Kill();
        _tween = null;
    }

    private void OnTargetResized()
    {
        if (GodotObject.IsInstanceValid(_target))
            _target!.PivotOffset = _target.Size * 0.5f;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.A = alpha;
        return color;
    }
}
