using System;
using Godot;

#nullable enable

namespace GoDoTemplate.Verification;

/// <summary>Verifies safe-area coordinate conversion, fallback behavior, and starter UI scene structure.</summary>
public sealed partial class SafeAreaRegression : Node
{
    private static readonly string[] SafeAreaScenes =
    {
        "res://MainMenu/MainMenuView.tscn",
        "res://Settings/SettingsView.tscn",
        "res://Gameplay/GameplayHud.tscn",
        "res://Ui/PauseModal.tscn",
        "res://Ui/ConfirmDialogModal.tscn",
        "res://Ui/LoadingOverlay.tscn",
        "res://Ui/ToastOverlay.tscn",
    };

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            VerifyCoordinateConversion();
            VerifyFallbacks();
            VerifyCommonAspectRatios();
            VerifySceneStructure();
            GD.Print("[SafeAreaRegression] PASS (4/4)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SafeAreaRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void VerifyCoordinateConversion()
    {
        Rect2 viewport = new(0.0f, 0.0f, 1000.0f, 500.0f);
        Rect2 adjustedForWindow = SafeAreaContainer.CalculateViewportSafeArea(
            viewport,
            new Rect2I(110, 220, 980, 460),
            new Vector2I(100, 200),
            Transform2D.Identity);
        AssertRect(adjustedForWindow, new Rect2(10.0f, 20.0f, 980.0f, 460.0f), "window position");

        Transform2D doubled = Transform2D.Identity.Scaled(new Vector2(2.0f, 2.0f));
        Rect2 adjustedForStretch = SafeAreaContainer.CalculateViewportSafeArea(
            viewport,
            new Rect2I(20, 40, 1960, 920),
            Vector2I.Zero,
            doubled);
        AssertRect(adjustedForStretch, new Rect2(10.0f, 20.0f, 980.0f, 460.0f), "stretch transform");
    }

    private static void VerifyFallbacks()
    {
        Rect2 viewport = new(0.0f, 0.0f, 1280.0f, 720.0f);
        Rect2 desktopFallback = SafeAreaContainer.CalculateViewportSafeArea(
            viewport,
            new Rect2I(0, 0, 1920, 1040),
            new Vector2I(100, 100),
            Transform2D.Identity);
        AssertRect(desktopFallback, viewport, "desktop usable rectangle");

        Rect2 emptyFallback = SafeAreaContainer.CalculateViewportSafeArea(
            viewport,
            new Rect2I(),
            Vector2I.Zero,
            Transform2D.Identity);
        AssertRect(emptyFallback, viewport, "empty platform result");

        Rect2 nonOverlappingFallback = SafeAreaContainer.CalculateViewportSafeArea(
            viewport,
            new Rect2I(3000, 3000, 100, 100),
            Vector2I.Zero,
            Transform2D.Identity);
        AssertRect(nonOverlappingFallback, viewport, "non-overlapping platform result");
    }

    private static void VerifyCommonAspectRatios()
    {
        Vector2I[] viewportSizes =
        {
            new(1920, 1080),
            new(1920, 1200),
            new(1600, 1200),
            new(2560, 1080),
            new(1080, 1920),
        };

        foreach (Vector2I size in viewportSizes)
        {
            Rect2 expected = new(24.0f, 48.0f, size.X - 48.0f, size.Y - 96.0f);
            Rect2 actual = SafeAreaContainer.CalculateViewportSafeArea(
                new Rect2(Vector2.Zero, size),
                new Rect2I(24, 48, size.X - 48, size.Y - 96),
                Vector2I.Zero,
                Transform2D.Identity);
            AssertRect(actual, expected, $"aspect ratio {size.X}x{size.Y}");
        }
    }

    private static void VerifySceneStructure()
    {
        foreach (string path in SafeAreaScenes)
        {
            PackedScene scene = ResourceLoader.Load<PackedScene>(path)
                ?? throw new InvalidOperationException($"Cannot load UI scene: {path}");
            Control root = scene.Instantiate<Control>();
            try
            {
                Assert(root.GetNodeOrNull<SafeAreaContainer>("SafeArea") is not null, $"Missing SafeArea in {path}");
            }
            finally
            {
                root.Free();
            }
        }

        Control loading = ResourceLoader.Load<PackedScene>("res://Ui/LoadingOverlay.tscn").Instantiate<Control>();
        try
        {
            Assert(loading.GetNodeOrNull<ColorRect>("Background") is not null, "Loading mask must remain outside SafeArea");
            Assert(loading.GetNodeOrNull<Control>("SafeArea/Center") is not null, "Loading content must be inside SafeArea");
        }
        finally
        {
            loading.Free();
        }
    }

    private static void AssertRect(Rect2 actual, Rect2 expected, string scenario)
    {
        if (!actual.Position.IsEqualApprox(expected.Position) || !actual.Size.IsEqualApprox(expected.Size))
            throw new InvalidOperationException($"Unexpected safe area for {scenario}: {actual}, expected {expected}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
