using System;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDoTemplate.Verification;

/// <summary>Verifies template transition state, repeated close, pause, reuse, and external removal behavior.</summary>
public sealed partial class UiTransitionRegression : Node
{
    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            await VerifyMountedAndRepeatedClose();
            await VerifyPausedTransitionAndReuse();
            await VerifyExternalRemoval();
            GD.Print("[UiTransitionRegression] PASS (3/3)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GetTree().Paused = false;
            GD.PushError($"[UiTransitionRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task VerifyMountedAndRepeatedClose()
    {
        (Control root, UiTransitionPlayer transition) = CreateTransitionView(0.04, 0.04);
        AddChild(root);
        Assert(root.IsInsideTree(), "Transition view must be mounted before its enter animation completes");
        Assert(transition.Phase == UiTransitionPhase.Entering, "Mounted view must expose Entering separately from UI open state");

        int closeCount = 0;
        Task<bool> first = UiTransitionCoordinator.TryCloseAsync(root, () => closeCount++);
        Task<bool> repeated = UiTransitionCoordinator.TryCloseAsync(root, () => closeCount++);
        Assert(!await repeated, "Repeated close must be rejected while exit animation is running");
        Assert(root.IsInsideTree() && transition.Phase == UiTransitionPhase.Exiting,
            "UI must remain mounted and blocking while its exit animation runs");
        Assert(await first, "First close request must complete after the exit animation");
        Assert(closeCount == 1, "Close action must execute exactly once");
        root.Free();

        Control plainView = new() { Name = "PlainView" };
        AddChild(plainView);
        int immediateCloseCount = 0;
        Assert(await UiTransitionCoordinator.TryCloseAsync(plainView, () => immediateCloseCount++),
            "View without a transition component must close immediately");
        Assert(immediateCloseCount == 1, "Immediate close action must execute once");
        plainView.Free();
    }

    private async Task VerifyPausedTransitionAndReuse()
    {
        (Control root, UiTransitionPlayer transition) = CreateTransitionView(0.02, 0.02);
        AddChild(root);
        await WaitForPhase(transition, UiTransitionPhase.Open);

        GetTree().Paused = true;
        Assert(await transition.PlayExitAsync(), "Exit animation must complete while SceneTree is paused");
        Assert(transition.Phase == UiTransitionPhase.Closed, "Completed exit must report Closed");
        GetTree().Paused = false;

        RemoveChild(root);
        AddChild(root);
        Assert(transition.Phase == UiTransitionPhase.Entering, "Re-entered cached view must replay its enter animation");
        await WaitForPhase(transition, UiTransitionPhase.Open);
        root.Free();
    }

    private async Task VerifyExternalRemoval()
    {
        (Control root, UiTransitionPlayer transition) = CreateTransitionView(0.0, 0.2);
        AddChild(root);
        Assert(transition.Phase == UiTransitionPhase.Open, "Zero-duration enter must complete immediately");
        Task<bool> exit = transition.PlayExitAsync();
        root.Free();
        Assert(!await exit, "External removal must complete a pending exit without authorizing a second close");
    }

    private static (Control Root, UiTransitionPlayer Transition) CreateTransitionView(
        double enterDuration,
        double exitDuration)
    {
        Control root = new() { Name = "TransitionView" };
        Control target = new() { Name = "Target", Size = new Vector2(320.0f, 180.0f) };
        UiTransitionPlayer transition = new()
        {
            Name = "Transition",
            TargetPath = new NodePath("../Target"),
            EnterDuration = enterDuration,
            ExitDuration = exitDuration,
        };
        root.AddChild(target);
        root.AddChild(transition);
        return (root, transition);
    }

    private async Task WaitForPhase(UiTransitionPlayer transition, UiTransitionPhase expected)
    {
        for (int frame = 0; frame < 120; frame++)
        {
            if (transition.Phase == expected)
                return;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        throw new TimeoutException($"Waiting for transition phase {expected} timed out");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
