using System;
using Godot;
using GoDo;

#nullable enable

namespace StarterGame;

/// <summary>StarterGame 模板入口场景。</summary>
public sealed partial class Boot : Control
{
    private Label? _statusLabel;

    [Export] public NodePath StatusLabelPath { get; set; } = null!;

    public override async void _Ready()
    {
        _statusLabel = GetNodeOrNull<Label>(StatusLabelPath);
        if (!IsInstanceValid(_statusLabel))
            throw new InvalidOperationException("Boot 缺少状态 Label。");

        try
        {
            _statusLabel.Text = "正在启动 StarterGame...";
            await Services.Get<IProcedureService>().ChangeAsync(new BootProcedure());
        }
        catch (Exception exception)
        {
            _statusLabel.Text = $"StarterGame 启动失败：{exception.Message}";
            ErrorHub.Report(exception, nameof(Boot));
        }
    }
}
