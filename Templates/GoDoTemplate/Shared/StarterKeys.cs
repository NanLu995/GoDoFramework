using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>集中维护模板业务层的资源定位和 UI 语义标识。</summary>
internal static class StarterKeys
{
    internal static readonly ResourceKey UiConfig = ResourceKey.Create("res://Ui/UiConfig.tres");
    internal static readonly ResourceKey ProjectConfig = ResourceKey.Create("res://Shared/StarterConfig.tres");
    internal static readonly ResourceKey MainMenuScene = ResourceKey.Create("res://MainMenu/MainMenuScene.tscn");
    internal static readonly ResourceKey GameplayScene = ResourceKey.Create("res://Gameplay/GameplayScene.tscn");

    internal static readonly UiId MainMenuView = UiId.Create("main_menu");
    internal static readonly UiId SettingsView = UiId.Create("settings");
    internal static readonly UiId GameplayHud = UiId.Create("gameplay_hud");
    internal static readonly UiId PauseModal = UiId.Create("pause");
    internal static readonly UiId ConfirmDialog = UiId.Create("confirm_dialog");
    internal static readonly UiId LoadingOverlay = UiId.Create("loading");
    internal static readonly UiId ToastOverlay = UiId.Create("toast");
}
