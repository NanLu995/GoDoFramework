using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Demo;

/// <summary>验证 Demo 基础数据后进入主菜单。</summary>
public sealed partial class DemoBoot : Control
{
    private static readonly ResourceKey ConfigKey =
        ResourceKey.Create("res://Demo/Config/ClickGameConfig.tres");
    private static readonly ResourceKey MainMenuKey =
        ResourceKey.Create("res://Demo/Scenes/MainMenu.tscn");
    private static readonly SaveSlot DemoSlot = SaveSlot.Create("demo_progress");
    private static readonly DemoSaveCodec SaveCodec = new();

    [Export] public NodePath StatusLabelPath { get; set; } = null!;

    public override async void _Ready()
    {
        Label? statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        if (!IsInstanceValid(statusLabel))
            throw new InvalidOperationException("DemoBoot 缺少状态 Label。");

        try
        {
            statusLabel.Text = "正在加载 Demo...";
            _ = ConfigHub.Load<ClickGameConfig>(ConfigKey);
            _ = Services.Get<ISaveService>().Load(DemoSlot, SaveCodec);
            _ = Services.Get<ISettingsService>().LoadAndApply();
            await Services.Get<ISceneService>().ChangeAsync(MainMenuKey);
        }
        catch (Exception exception)
        {
            statusLabel.Text = $"Demo 启动失败：{exception.Message}";
            ErrorHub.Report(exception, nameof(DemoBoot));
        }
    }
}
