using GoDo;

#nullable enable

namespace GoDoFramework.Templates.StarterGame;

/// <summary>StarterGame 模板集中维护的资源键和存档槽位。</summary>
internal static class StarterGameKeys
{
    public static readonly ResourceKey Config =
        ResourceKey.Create("res://Templates/StarterGame/Config/StarterGameConfig.tres");

    public static readonly ResourceKey MainMenuScene =
        ResourceKey.Create("res://Templates/StarterGame/Scenes/MainMenuScene.tscn");

    public static readonly ResourceKey GameplayScene =
        ResourceKey.Create("res://Templates/StarterGame/Scenes/GameplayScene.tscn");

    public static readonly ResourceKey GameplayHud =
        ResourceKey.Create("res://Templates/StarterGame/UI/GameplayHud.tscn");

    public static readonly ResourceKey ResultView =
        ResourceKey.Create("res://Templates/StarterGame/UI/ResultView.tscn");

    public static readonly ResourceKey Bgm =
        ResourceKey.Create("res://Templates/StarterGame/Audio/StarterBgm.tres");

    public static readonly ResourceKey ClickSfx =
        ResourceKey.Create("res://Templates/StarterGame/Audio/ClickSfx.tres");

    public static readonly SaveSlot SaveSlot = SaveSlot.Create("starter_game_progress");

    public static readonly StarterSaveCodec SaveCodec = new();
}
