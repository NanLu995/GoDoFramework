using System;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDoTemplate;

/// <summary>Coordinates template-owned close actions with an optional transition child.</summary>
internal static class UiTransitionCoordinator
{
    private static readonly NodePath TransitionPath = new("Transition");

    internal static async Task<bool> TryCloseAsync(Control view, Action close)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(close);

        if (!GodotObject.IsInstanceValid(view) || !view.IsInsideTree())
            return false;

        UiTransitionPlayer? transition = view.GetNodeOrNull<UiTransitionPlayer>(TransitionPath);
        if (GodotObject.IsInstanceValid(transition))
        {
            try
            {
                if (!await transition!.PlayExitAsync())
                    return false;
            }
            catch (Exception exception)
            {
                StarterLog.Ui.Error(exception, "PlayExitTransition");
            }
        }

        if (!GodotObject.IsInstanceValid(view) || !view.IsInsideTree())
            return false;

        try
        {
            close();
            return true;
        }
        catch (Exception exception)
        {
            StarterLog.Ui.Error(exception, "CloseTransition");
            throw;
        }
    }
}
