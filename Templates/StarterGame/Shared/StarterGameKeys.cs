using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>StarterGame 模板集中维护的资源键和存档槽位。</summary>
internal static class StarterGameKeys
{
    private const string Root = "res://Templates/StarterGame";

    public static readonly ResourceKey Config = Key("Shared/StarterGameConfig.tres");

    public static readonly ResourceKey MainMenuScene = Key("MainMenu/MainMenuScene.tscn");

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
