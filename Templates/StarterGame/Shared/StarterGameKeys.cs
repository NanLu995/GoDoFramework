using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>StarterGame 模板集中维护的资源键、语义资源 ID 和存档槽位。</summary>
internal static class StarterGameKeys
{
    private const string Root = "res://Templates/StarterGame";

    public static readonly ResourceKey Config = Key("Shared/StarterGameConfig.tres");

    public static readonly ResourceKey ResourceManifest = Key("Shared/ResourceManifest.tres");

    public static readonly ResourceKey GameplayScene = Key("Gameplay/GameplayScene.tscn");

    public static readonly ResourceKey GameplayHud = Key("Gameplay/GameplayHud.tscn");

    public static readonly ResourceKey ResultView = Key("Result/ResultView.tscn");

    public static readonly ResourceKey Bgm = Key("Audio/StarterBgm.tres");

    public static readonly ResourceKey ClickSfx = Key("Audio/ClickSfx.tres");

    public static readonly SaveSlot SaveSlot = SaveSlot.Create("starter_game_progress");

    public static readonly StarterSaveCodec SaveCodec = new();

    private static ResourceKey Key(string relativePath) =>
        ResourceKey.Create($"{Root}/{relativePath}");
}

/// <summary>StarterGame 模板使用的语义资源 ID。</summary>
internal static class StarterGameResourceIds
{
    /// <summary>主菜单场景的业务语义 ID，由项目开发者定义并在 ResourceManifest 中保持一致。</summary>
    public const string MainMenuScene = "main_menu";
}
