using Godot;

#nullable enable

namespace GoDoTemplate;

/// <summary>
/// Keeps its child UI inside the platform's unobscured display area while the surrounding UI root
/// can continue covering the complete viewport.
/// </summary>
/// <remarks>
/// The safe rectangle is refreshed when the viewport size changes. Platforms that do not report a
/// usable safe area, headless runs, and invalid stretch transforms fall back to the full visible viewport.
/// </remarks>
public sealed partial class SafeAreaContainer : Control
{
    private Viewport? _viewport;

    /// <inheritdoc />
    public override void _Ready()
    {
        _viewport = GetViewport();
        _viewport.SizeChanged += Refresh;
        Refresh();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        if (_viewport is not null && GodotObject.IsInstanceValid(_viewport))
            _viewport.SizeChanged -= Refresh;

        _viewport = null;
    }

    /// <summary>
    /// Re-queries the platform safe area and immediately updates this control's rectangle.
    /// Call this after a platform-specific display change that does not resize the viewport.
    /// </summary>
    public void Refresh()
    {
        if (_viewport is null || !IsInsideTree())
            return;

        Rect2 safeArea = CalculateViewportSafeArea(
            _viewport.GetVisibleRect(),
            DisplayServer.GetDisplaySafeArea(),
            DisplayServer.WindowGetPosition(),
            _viewport.GetStretchTransform());

        if (GetParentOrNull<Control>() is Control parent)
            safeArea = parent.GetGlobalTransformWithCanvas().AffineInverse() * safeArea;

        AnchorLeft = 0.0f;
        AnchorTop = 0.0f;
        AnchorRight = 0.0f;
        AnchorBottom = 0.0f;
        Position = safeArea.Position;
        Size = safeArea.Size;
    }

    internal static Rect2 CalculateViewportSafeArea(
        Rect2 viewportRect,
        Rect2I displaySafeArea,
        Vector2I windowPosition,
        Transform2D stretchTransform)
    {
        if (viewportRect.Size.X <= 0.0f || viewportRect.Size.Y <= 0.0f)
            return viewportRect;

        if (displaySafeArea.Size.X <= 0 || displaySafeArea.Size.Y <= 0)
            return viewportRect;

        if (!stretchTransform.IsFinite() || Mathf.IsZeroApprox(stretchTransform.Determinant()))
            return viewportRect;

        Rect2 clientSafeArea = new(
            displaySafeArea.Position - windowPosition,
            displaySafeArea.Size);
        Rect2 mappedSafeArea = stretchTransform.AffineInverse() * clientSafeArea;
        Rect2 visibleSafeArea = mappedSafeArea.Intersection(viewportRect);

        return visibleSafeArea.Size.X > 0.0f && visibleSafeArea.Size.Y > 0.0f
            ? visibleSafeArea
            : viewportRect;
    }
}
