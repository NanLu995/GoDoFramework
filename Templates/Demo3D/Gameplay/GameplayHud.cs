using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace Demo3D;

/// <summary>显示当前收集进度和操作提示的 HUD。</summary>
public sealed partial class GameplayHud : Control
{
    private static readonly InputBindingId JumpPrimaryBinding =
        InputBindingId.Create("gameplay.jump.primary");

    [Export] public NodePath ProgressLabelPath { get; set; } = null!;
    [Export] public NodePath DeviceLabelPath { get; set; } = null!;
    [Export] public NodePath JumpBindingLabelPath { get; set; } = null!;
    [Export] public NodePath RebindJumpButtonPath { get; set; } = null!;
    [Export] public NodePath RestoreJumpButtonPath { get; set; } = null!;
    [Export] public NodePath RebindStatusLabelPath { get; set; } = null!;

    private Label? _progressLabel;
    private Label? _deviceLabel;
    private Label? _jumpBindingLabel;
    private Button? _rebindJumpButton;
    private Button? _restoreJumpButton;
    private Label? _rebindStatusLabel;
    private IInputRebinding? _rebinding;
    private IInputRebindingPersistence? _rebindingPersistence;
    private bool _captureOwned;

    public override void _Ready()
    {
        _progressLabel = GetNodeOrNull<Label>(ProgressLabelPath);
        if (!GodotObject.IsInstanceValid(_progressLabel))
            throw new InvalidOperationException("GameplayHud 缺少进度标签引用。");
        _deviceLabel = GetNodeOrNull<Label>(DeviceLabelPath);
        if (!GodotObject.IsInstanceValid(_deviceLabel))
            throw new InvalidOperationException("GameplayHud 缺少输入设备标签引用。");
        _jumpBindingLabel = RequireNode<Label>(JumpBindingLabelPath, "跳跃绑定标签");
        _rebindJumpButton = RequireNode<Button>(RebindJumpButtonPath, "跳跃改键按钮");
        _restoreJumpButton = RequireNode<Button>(RestoreJumpButtonPath, "恢复默认按钮");
        _rebindStatusLabel = RequireNode<Label>(RebindStatusLabelPath, "改键状态标签");

        IInputService input = Services.Get<IInputService>();
        if (!input.TryGetRebinding(out _rebinding))
            throw new InvalidOperationException("Demo3D 需要支持重绑定的输入后端。");
        if (!input.TryGetRebindingPersistence(out _rebindingPersistence))
            throw new InvalidOperationException("Demo3D 需要支持持久化的输入后端。");

        EventChannel.Bind<CollectionProgressChangedEvent>(this, OnCollectionProgressChanged);
        EventChannel.Bind<InputDeviceChangedEvent>(this, OnInputDeviceChanged);
        _rebindJumpButton.Pressed += OnRebindJumpPressed;
        _restoreJumpButton.Pressed += OnRestoreJumpPressed;
        UpdateDeviceLabel(input.ActiveDevice);
        RefreshJumpBinding();
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_rebindJumpButton))
            _rebindJumpButton!.Pressed -= OnRebindJumpPressed;
        if (GodotObject.IsInstanceValid(_restoreJumpButton))
            _restoreJumpButton!.Pressed -= OnRestoreJumpPressed;
        if (_captureOwned && _rebinding?.IsCapturing == true)
            _rebinding.CancelCapture();

        _captureOwned = false;
        _rebinding = null;
        _rebindingPersistence = null;
    }

    private void OnCollectionProgressChanged(CollectionProgressChangedEvent evt)
    {
        _progressLabel!.Text = $"能量核心：{evt.Current} / {evt.Total}";
    }

    private void OnInputDeviceChanged(InputDeviceChangedEvent evt) => UpdateDeviceLabel(evt.Current);

    private async void OnRebindJumpPressed()
    {
        if (_rebinding == null || _captureOwned)
            return;

        _rebindJumpButton!.ReleaseFocus();
        _captureOwned = true;
        SetRebindControlsEnabled(false);
        _rebindStatusLabel!.Text = "等待新输入…按 Esc 取消";
        try
        {
            InputBindingCandidate? candidate = await _rebinding.CaptureAsync(JumpPrimaryBinding);
            if (candidate == null)
            {
                _rebindStatusLabel.Text = "已取消改键";
                return;
            }

            IReadOnlyList<InputBindingInfo> conflicts =
                _rebinding.FindConflicts(JumpPrimaryBinding, candidate);
            if (conflicts.Count > 0)
            {
                _rebindStatusLabel.Text = $"存在冲突：{conflicts[0].DisplayName}，未应用";
                return;
            }

            _rebinding.Apply(JumpPrimaryBinding, candidate);
            SaveBindings($"已改为并保存：{candidate.DisplayText}");
        }
        catch (Exception exception)
        {
            _rebindStatusLabel!.Text = $"改键失败：{exception.Message}";
        }
        finally
        {
            _captureOwned = false;
            if (IsInsideTree())
            {
                SetRebindControlsEnabled(true);
                RefreshJumpBinding();
            }
        }
    }

    private void OnRestoreJumpPressed()
    {
        if (_rebinding == null || _captureOwned)
            return;

        _restoreJumpButton!.ReleaseFocus();
        try
        {
            _rebinding.RestoreDefault(JumpPrimaryBinding);
            SaveBindings("已恢复并保存默认绑定");
            RefreshJumpBinding();
        }
        catch (Exception exception)
        {
            _rebindStatusLabel!.Text = $"恢复失败：{exception.Message}";
        }
    }

    private void RefreshJumpBinding()
    {
        InputBindingInfo info = _rebinding!.GetBinding(JumpPrimaryBinding);
        string current = string.IsNullOrEmpty(info.CurrentDisplayText) ? "未绑定" : info.CurrentDisplayText;
        _jumpBindingLabel!.Text = $"跳跃主绑定：{current}";
    }

    private void SaveBindings(string successMessage)
    {
        try
        {
            _rebindingPersistence!.Save();
            _rebindStatusLabel!.Text = successMessage;
        }
        catch (Exception exception)
        {
            _rebindStatusLabel!.Text = $"绑定已生效，但保存失败：{exception.Message}";
        }
    }

    private void SetRebindControlsEnabled(bool enabled)
    {
        _rebindJumpButton!.Disabled = !enabled;
        _restoreJumpButton!.Disabled = !enabled;
    }

    private T RequireNode<T>(NodePath path, string description)
        where T : Node
    {
        T? node = GetNodeOrNull<T>(path);
        if (!GodotObject.IsInstanceValid(node))
            throw new InvalidOperationException($"GameplayHud 缺少{description}引用。");

        return node!;
    }

    private void UpdateDeviceLabel(InputDeviceKind device)
    {
        string name = device switch
        {
            InputDeviceKind.KeyboardMouse => "键盘鼠标",
            InputDeviceKind.Gamepad => "手柄",
            InputDeviceKind.Touch => "触摸",
            _ => "等待输入",
        };
        _deviceLabel!.Text = $"输入设备：{name}";
    }
}
