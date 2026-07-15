using GoDo;

namespace Demo3D;

/// <summary>Demo3D 集中维护的业务资源键。</summary>
internal static class Demo3DKeys
{
    private const string Root = "res://Templates/Demo3D";

    public static readonly ResourceKey GameplayScene = Key("Gameplay/GameplayScene.tscn");
    public static readonly ResourceKey GameplayHud = Key("Gameplay/GameplayHud.tscn");
    public static readonly ResourceKey ResultView = Key("Result/ResultView.tscn");
    public static readonly CameraId GameplayCamera = CameraId.Create("gameplay");

    private static ResourceKey Key(string relativePath) =>
        ResourceKey.Create($"{Root}/{relativePath}");
}
