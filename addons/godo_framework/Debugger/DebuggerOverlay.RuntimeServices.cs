#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

#nullable enable

namespace GoDo;

public sealed partial class DebuggerOverlay : CanvasLayer
{

    private void CacheInputNodes()
    {
        _inputBackendValue = GetInputNode<Label>("StatusGrid/BackendCard/Content/Value");
        _inputBackendDetail = GetInputNode<Label>("StatusGrid/BackendCard/Content/Detail");
        _inputDeviceValue = GetInputNode<Label>("StatusGrid/DeviceCard/Content/Value");
        _inputFrameValue = GetInputNode<Label>("StatusGrid/FrameCard/Content/Value");
        _inputFrameDetail = GetInputNode<Label>("StatusGrid/FrameCard/Content/Detail");
        _inputActionsValue = GetInputNode<Label>("StatusGrid/ActionsCard/Content/Value");
        _inputCapabilities = GetInputNode<Label>("Capabilities");
        _inputContextsTree = GetInputNode<Tree>("ContextList");
        _inputActionsSearch = GetInputNode<LineEdit>("ActionSearch");
        _inputActionsMatchStatus = GetInputNode<Label>("ActionMatchStatus");
        _inputActionsTree = GetInputNode<Tree>("ActionList");

        _inputContextsTree.SetColumnTitle(0, "Context");
        _inputContextsTree.SetColumnTitle(1, "模式");
        _inputContextsTree.SetColumnTitle(2, "状态");
        _inputContextsTree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _inputContextsTree.SetColumnTitleAlignment(1, HorizontalAlignment.Center);
        _inputContextsTree.SetColumnTitleAlignment(2, HorizontalAlignment.Center);
        _inputContextsTree.SetColumnExpand(0, true);
        _inputContextsTree.SetColumnExpand(1, false);
        _inputContextsTree.SetColumnExpand(2, false);
        _inputContextsTree.SetColumnCustomMinimumWidth(1, 76);
        _inputContextsTree.SetColumnCustomMinimumWidth(2, 64);

        _inputActionsTree.SetColumnTitle(0, "Action");
        _inputActionsTree.SetColumnTitle(1, "类型");
        _inputActionsTree.SetColumnTitle(2, "当前值");
        _inputActionsTree.SetColumnTitle(3, "边沿");
        _inputActionsTree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _inputActionsTree.SetColumnTitleAlignment(1, HorizontalAlignment.Center);
        _inputActionsTree.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _inputActionsTree.SetColumnTitleAlignment(3, HorizontalAlignment.Center);
        _inputActionsTree.SetColumnExpand(0, true);
        _inputActionsTree.SetColumnExpand(1, false);
        _inputActionsTree.SetColumnExpand(2, true);
        _inputActionsTree.SetColumnExpand(3, false);
        _inputActionsTree.SetColumnCustomMinimumWidth(1, 58);
        _inputActionsTree.SetColumnCustomMinimumWidth(3, 82);
    }

    private T GetInputNode<T>(string path) where T : Node
    {
        T? node = _inputDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerInput 场景缺少节点：{path}");
    }

    private void CacheSchedulerNodes()
    {
        _schedulerActiveValue = GetSchedulerLabel("Content/StatusGrid/ActiveCard/Content/Value");
        _schedulerPausedValue = GetSchedulerLabel("Content/StatusGrid/PausedCard/Content/Value");
        _schedulerRepeatingValue = GetSchedulerLabel("Content/StatusGrid/RepeatingCard/Content/Value");
        _schedulerNextValue = GetSchedulerLabel("Content/StatusGrid/NextCard/Content/Value");
        _schedulerProcessGameValue =
            GetSchedulerLabel("Content/PhaseGrid/ProcessCard/Content/Clocks/Game/Value");
        _schedulerProcessUnscaledValue =
            GetSchedulerLabel("Content/PhaseGrid/ProcessCard/Content/Clocks/Unscaled/Value");
        _schedulerProcessRealValue =
            GetSchedulerLabel("Content/PhaseGrid/ProcessCard/Content/Clocks/Real/Value");
        _schedulerProcessDispatchValue =
            GetSchedulerLabel("Content/PhaseGrid/ProcessCard/Content/Dispatch/Value");
        _schedulerPhysicsGameValue =
            GetSchedulerLabel("Content/PhaseGrid/PhysicsCard/Content/Clocks/Game/Value");
        _schedulerPhysicsUnscaledValue =
            GetSchedulerLabel("Content/PhaseGrid/PhysicsCard/Content/Clocks/Unscaled/Value");
        _schedulerPhysicsRealValue =
            GetSchedulerLabel("Content/PhaseGrid/PhysicsCard/Content/Clocks/Real/Value");
        _schedulerPhysicsDispatchValue =
            GetSchedulerLabel("Content/PhaseGrid/PhysicsCard/Content/Dispatch/Value");
        _schedulerCanceledValue = GetSchedulerLabel("Content/LifetimeGrid/CanceledCard/Content/Value");
        _schedulerOwnerCanceledValue =
            GetSchedulerLabel("Content/LifetimeGrid/OwnerCard/Content/Value");
        _schedulerFailedValue = GetSchedulerLabel("Content/LifetimeGrid/FailedCard/Content/Value");
    }

    private Label GetSchedulerLabel(string path)
    {
        Label? label = _schedulerDashboard!.GetNodeOrNull<Label>(path);
        return IsInstanceValid(label)
            ? label
            : throw new InvalidOperationException($"DebuggerScheduler 场景缺少节点：{path}");
    }

    private void CacheAudioNodes()
    {
        _audioBgmStateValue = GetAudioLabel("Content/PlaybackGrid/BgmStateCard/Content/Value");
        _audioBgmStateDetail = GetAudioLabel("Content/PlaybackGrid/BgmStateCard/Content/Detail");
        _audioSfxValue = GetAudioLabel("Content/PlaybackGrid/SfxCard/Content/Value");
        _audioSfxDetail = GetAudioLabel("Content/PlaybackGrid/SfxCard/Content/Detail");
        _audioBgmResourceValue = GetAudioLabel("Content/BgmCard/Content/Value");
        _audioMasterVolumeValue = GetAudioLabel("Content/VolumeGrid/MasterCard/Content/Value");
        _audioBgmVolumeValue = GetAudioLabel("Content/VolumeGrid/BgmCard/Content/Value");
        _audioSfxVolumeValue = GetAudioLabel("Content/VolumeGrid/SfxCard/Content/Value");
    }

    private Label GetAudioLabel(string path)
    {
        Label? label = _audioDashboard!.GetNodeOrNull<Label>(path);
        return IsInstanceValid(label)
            ? label
            : throw new InvalidOperationException($"DebuggerAudio 场景缺少节点：{path}");
    }


    private void OnInputActionsSearchChanged(string text)
    {
        _inputActionsSearchQuery = text.Trim();
        _inputActionsSignature = int.MinValue;
        if (_selectedPage?.IsInput == true)
            RefreshDebugger(force: true);
    }

    private void OnInputActionsSearchSubmitted(string text)
    {
        _inputActionsSearchQuery = text.Trim();
        if (IsInstanceValid(_inputActionsSearch))
            _inputActionsSearch.ReleaseFocus();
    }


    private void RefreshInputDashboard()
    {
        if (!IsInstanceValid(_inputBackendValue) ||
            !IsInstanceValid(_inputBackendDetail) ||
            !IsInstanceValid(_inputDeviceValue) ||
            !IsInstanceValid(_inputFrameValue) ||
            !IsInstanceValid(_inputFrameDetail) ||
            !IsInstanceValid(_inputActionsValue) ||
            !IsInstanceValid(_inputCapabilities) ||
            !IsInstanceValid(_inputContextsTree) ||
            !IsInstanceValid(_inputActionsMatchStatus) ||
            !IsInstanceValid(_inputActionsTree))
        {
            return;
        }

        if (!Services.TryGet<IInputService>(out IInputService? input) || input is null)
        {
            SetInputUnavailable("未注册", "InputService 未注册");
            return;
        }

        if (input is not InputService inputService)
        {
            SetInputUnavailable("不支持", "当前实现不支持 Debug 快照");
            return;
        }

        InputDebugSnapshot snapshot = inputService.GetDebugSnapshot();
        _inputBackendValue.Text = snapshot.IsReady ? snapshot.BackendName : "未安装";
        _inputBackendValue.TooltipText = snapshot.IsReady ? snapshot.BackendName : string.Empty;
        _inputBackendDetail.Text = snapshot.IsReady ? "已就绪" : "无输入后端";
        _inputDeviceValue.Text = snapshot.ActiveDevice.ToString();
        _inputFrameValue.Text = snapshot.Sequence.ToString(CultureInfo.InvariantCulture);
        _inputFrameDetail.Text = snapshot.HasSample ? "采样正常" : "等待首次采样";
        _inputActionsValue.Text = snapshot.Actions.Length.ToString(CultureInfo.InvariantCulture);
        _inputCapabilities.Text = $"能力：{snapshot.Capabilities}";
        _inputCapabilities.TooltipText = snapshot.Capabilities.ToString();

        RefreshInputContexts(snapshot.Contexts);
        RefreshInputActions(snapshot.Actions);
    }

    private void SetInputUnavailable(string state, string detail)
    {
        _inputBackendValue!.Text = state;
        _inputBackendValue.TooltipText = string.Empty;
        _inputBackendDetail!.Text = detail;
        _inputDeviceValue!.Text = "Unknown";
        _inputFrameValue!.Text = "—";
        _inputFrameDetail!.Text = "无采样";
        _inputActionsValue!.Text = "0";
        _inputCapabilities!.Text = "能力：无";
        _inputCapabilities.TooltipText = string.Empty;
        _inputActionsMatchStatus!.Text = detail;
        _inputContextsTree!.Clear();
        _inputActionsTree!.Clear();
        _inputContextsSignature = int.MinValue;
        _inputActionsSignature = int.MinValue;
    }

    private void RefreshInputContexts(InputDebugContextEntry[] contexts)
    {
        float contextHeight = Mathf.Clamp(
            MinimumInputContextHeight + Math.Max(0, contexts.Length - 1) * InputContextRowHeight,
            MinimumInputContextHeight,
            MaximumInputContextHeight);
        _inputContextsTree!.CustomMinimumSize =
            new Vector2(_inputContextsTree.CustomMinimumSize.X, contextHeight);

        var signature = new HashCode();
        for (int index = 0; index < contexts.Length; index++)
        {
            InputDebugContextEntry entry = contexts[index];
            signature.Add(entry.Context);
            signature.Add(entry.Mode);
            signature.Add(entry.IsEffective);
        }

        int snapshotSignature = signature.ToHashCode();
        if (_inputContextsSignature == snapshotSignature)
            return;

        _inputContextsSignature = snapshotSignature;
        _inputContextsTree.Clear();
        TreeItem root = _inputContextsTree.CreateItem();
        for (int index = 0; index < contexts.Length; index++)
        {
            InputDebugContextEntry entry = contexts[index];
            TreeItem item = _inputContextsTree.CreateItem(root);
            item.SetText(0, entry.Context.Value);
            item.SetText(1, entry.Mode.ToString());
            item.SetText(2, entry.IsEffective ? "有效" : "被屏蔽");
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTextAlignment(2, HorizontalAlignment.Center);
            item.SetTooltipText(0, entry.Context.Value);
        }
    }

    private void RefreshInputActions(InputDebugActionEntry[] actions)
    {
        var signature = new HashCode();
        signature.Add(_inputActionsSearchQuery, StringComparer.OrdinalIgnoreCase);
        int matchingCount = 0;
        for (int index = 0; index < actions.Length; index++)
        {
            InputDebugActionEntry entry = actions[index];
            signature.Add(entry.Action);
            signature.Add(entry.ValueType);
            signature.Add(entry.Value);
            signature.Add(entry.Pressed);
            signature.Add(entry.JustPressed);
            signature.Add(entry.JustReleased);
            if (MatchesInputActionSearch(entry))
                matchingCount++;
        }

        int displayedCount = Math.Min(matchingCount, MaxDisplayedInputActions);
        if (string.IsNullOrEmpty(_inputActionsSearchQuery))
        {
            _inputActionsMatchStatus!.Text = matchingCount <= MaxDisplayedInputActions
                ? $"全部 {matchingCount} 个 Action"
                : $"显示 {displayedCount} / {matchingCount} 个 Action";
        }
        else
        {
            _inputActionsMatchStatus!.Text = matchingCount <= MaxDisplayedInputActions
                ? $"找到 {matchingCount} 个 Action"
                : $"找到 {matchingCount} 个，显示前 {displayedCount} 个 Action";
        }

        int snapshotSignature = signature.ToHashCode();
        if (_inputActionsSignature == snapshotSignature)
            return;

        _inputActionsSignature = snapshotSignature;
        _inputActionsTree!.Clear();
        TreeItem root = _inputActionsTree.CreateItem();
        int addedCount = 0;
        for (int index = 0; index < actions.Length && addedCount < MaxDisplayedInputActions; index++)
        {
            InputDebugActionEntry entry = actions[index];
            if (!MatchesInputActionSearch(entry))
                continue;

            TreeItem item = _inputActionsTree.CreateItem(root);
            item.SetText(0, entry.Action.Value);
            item.SetText(1, entry.ValueType.ToString());
            item.SetText(2, FormatInputActionValue(entry));
            item.SetText(3, FormatInputActionEdge(entry));
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTextAlignment(3, HorizontalAlignment.Center);
            item.SetTooltipText(0, entry.Action.Value);
            addedCount++;
        }
    }

    private bool MatchesInputActionSearch(InputDebugActionEntry entry)
    {
        if (string.IsNullOrEmpty(_inputActionsSearchQuery))
            return true;

        return entry.Action.Value.Contains(
                _inputActionsSearchQuery,
                StringComparison.OrdinalIgnoreCase) ||
            entry.ValueType.ToString().Contains(
                _inputActionsSearchQuery,
                StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatInputActionValue(InputDebugActionEntry entry)
    {
        return entry.ValueType switch
        {
            InputActionValueType.Bool => entry.Pressed ? "Pressed" : "Released",
            InputActionValueType.Axis1D =>
                entry.Value.X.ToString("0.00", CultureInfo.InvariantCulture),
            InputActionValueType.Axis2D => string.Create(
                CultureInfo.InvariantCulture,
                $"({entry.Value.X:0.00}, {entry.Value.Y:0.00})"),
            InputActionValueType.Axis3D => string.Create(
                CultureInfo.InvariantCulture,
                $"({entry.Value.X:0.00}, {entry.Value.Y:0.00}, {entry.Value.Z:0.00})"),
            _ => "—",
        };
    }

    private static string FormatInputActionEdge(InputDebugActionEntry entry)
    {
        if (entry.JustPressed && entry.JustReleased)
            return "按下 / 释放";
        if (entry.JustPressed)
            return "刚按下";
        if (entry.JustReleased)
            return "刚释放";
        return "—";
    }

    private void RefreshSchedulerDashboard()
    {
        if (!IsInstanceValid(_schedulerActiveValue) ||
            !IsInstanceValid(_schedulerPausedValue) ||
            !IsInstanceValid(_schedulerRepeatingValue) ||
            !IsInstanceValid(_schedulerNextValue) ||
            !IsInstanceValid(_schedulerProcessGameValue) ||
            !IsInstanceValid(_schedulerProcessUnscaledValue) ||
            !IsInstanceValid(_schedulerProcessRealValue) ||
            !IsInstanceValid(_schedulerProcessDispatchValue) ||
            !IsInstanceValid(_schedulerPhysicsGameValue) ||
            !IsInstanceValid(_schedulerPhysicsUnscaledValue) ||
            !IsInstanceValid(_schedulerPhysicsRealValue) ||
            !IsInstanceValid(_schedulerPhysicsDispatchValue) ||
            !IsInstanceValid(_schedulerCanceledValue) ||
            !IsInstanceValid(_schedulerOwnerCanceledValue) ||
            !IsInstanceValid(_schedulerFailedValue))
            return;

        if (!Services.TryGet<ISchedulerService>(out ISchedulerService? scheduler) ||
            scheduler is null)
        {
            SetSchedulerUnavailable("未注册");
            return;
        }

        if (scheduler is not SchedulerService schedulerService)
        {
            SetSchedulerUnavailable("不支持 Debug 快照");
            return;
        }

        SchedulerDebugSnapshot snapshot = schedulerService.GetDebugSnapshot();
        _schedulerActiveValue.Text = snapshot.ActiveCount.ToString(CultureInfo.InvariantCulture);
        _schedulerPausedValue.Text = snapshot.PausedCount.ToString(CultureInfo.InvariantCulture);
        _schedulerRepeatingValue.Text = snapshot.RepeatingCount.ToString(CultureInfo.InvariantCulture);
        _schedulerNextValue.Text = snapshot.NextRemainingSeconds.HasValue
            ? $"{snapshot.NextRemainingSeconds.Value.ToString("0.000", CultureInfo.InvariantCulture)}s"
            : "无";
        _schedulerProcessGameValue.Text =
            snapshot.GameProcessCount.ToString(CultureInfo.InvariantCulture);
        _schedulerProcessUnscaledValue.Text =
            snapshot.UnscaledProcessCount.ToString(CultureInfo.InvariantCulture);
        _schedulerProcessRealValue.Text =
            snapshot.RealProcessCount.ToString(CultureInfo.InvariantCulture);
        _schedulerProcessDispatchValue.Text =
            snapshot.LastProcessDispatchCount.ToString(CultureInfo.InvariantCulture);
        _schedulerPhysicsGameValue.Text =
            snapshot.GamePhysicsCount.ToString(CultureInfo.InvariantCulture);
        _schedulerPhysicsUnscaledValue.Text =
            snapshot.UnscaledPhysicsCount.ToString(CultureInfo.InvariantCulture);
        _schedulerPhysicsRealValue.Text =
            snapshot.RealPhysicsCount.ToString(CultureInfo.InvariantCulture);
        _schedulerPhysicsDispatchValue.Text =
            snapshot.LastPhysicsDispatchCount.ToString(CultureInfo.InvariantCulture);
        _schedulerCanceledValue.Text = snapshot.CanceledCount.ToString(CultureInfo.InvariantCulture);
        _schedulerOwnerCanceledValue.Text =
            snapshot.OwnerCanceledCount.ToString(CultureInfo.InvariantCulture);
        _schedulerFailedValue.Text =
            snapshot.CallbackFailedCount.ToString(CultureInfo.InvariantCulture);
        _schedulerFailedValue.AddThemeColorOverride("font_color",
            snapshot.CallbackFailedCount > 0
                ? new Color(1f, 0.38f, 0.34f)
                : new Color(0.86f, 0.91f, 0.97f));
    }

    private void SetSchedulerUnavailable(string state)
    {
        _schedulerActiveValue!.Text = state;
        _schedulerPausedValue!.Text = "—";
        _schedulerRepeatingValue!.Text = "—";
        _schedulerNextValue!.Text = "—";
        _schedulerProcessGameValue!.Text = "—";
        _schedulerProcessUnscaledValue!.Text = "—";
        _schedulerProcessRealValue!.Text = "—";
        _schedulerProcessDispatchValue!.Text = "—";
        _schedulerPhysicsGameValue!.Text = "—";
        _schedulerPhysicsUnscaledValue!.Text = "—";
        _schedulerPhysicsRealValue!.Text = "—";
        _schedulerPhysicsDispatchValue!.Text = "—";
        _schedulerCanceledValue!.Text = "—";
        _schedulerOwnerCanceledValue!.Text = "—";
        _schedulerFailedValue!.Text = "—";
        _schedulerFailedValue.RemoveThemeColorOverride("font_color");
    }

    private void RefreshAudioDashboard()
    {
        if (!IsInstanceValid(_audioBgmStateValue) ||
            !IsInstanceValid(_audioBgmStateDetail) ||
            !IsInstanceValid(_audioBgmResourceValue) ||
            !IsInstanceValid(_audioSfxValue) ||
            !IsInstanceValid(_audioSfxDetail) ||
            !IsInstanceValid(_audioMasterVolumeValue) ||
            !IsInstanceValid(_audioBgmVolumeValue) ||
            !IsInstanceValid(_audioSfxVolumeValue))
            return;

        if (!Services.TryGet<IAudioService>(out IAudioService? audio) || audio is null)
        {
            _audioBgmStateValue.Text = "未注册";
            _audioBgmStateDetail.Text = "AudioService 未注册";
            _audioBgmResourceValue.Text = "—";
            _audioBgmResourceValue.TooltipText = string.Empty;
            _audioSfxValue.Text = "—";
            _audioSfxDetail.Text = "AudioService 未注册";
            SetAudioVolumeUnavailable();
            return;
        }

        ResourceKey? currentBgm = audio.CurrentBgm;
        _audioBgmStateValue.Text = audio.BgmState switch
        {
            BgmPlaybackState.Stopped => "已停止",
            BgmPlaybackState.Loading => "加载中",
            BgmPlaybackState.Playing => "播放中",
            BgmPlaybackState.Paused => "已暂停",
            BgmPlaybackState.Transitioning => "过渡中",
            BgmPlaybackState.Ended => "已结束",
            _ => "未知",
        };
        _audioBgmStateDetail.Text = audio.BgmState switch
        {
            BgmPlaybackState.Loading => currentBgm.HasValue ? "旧音乐继续播放" : "等待首次播放",
            BgmPlaybackState.Transitioning => "双播放器交叉淡化",
            BgmPlaybackState.Paused => "播放器与过渡均暂停",
            BgmPlaybackState.Playing => "播放器活跃",
            BgmPlaybackState.Ended => "资源保留，播放已结束",
            _ => "没有 BGM",
        };

        string bgmResource = currentBgm?.Value ?? "无";
        _audioBgmResourceValue.Text = bgmResource;
        _audioBgmResourceValue.TooltipText = currentBgm?.Value ?? string.Empty;

        int activeSfx = audio.ActiveSfxCount;
        int pendingSfx = audio.PendingSfxCount;
        int maxSfx = audio.MaxSfxVoices;
        int activeSfx3D = audio.ActiveSfx3DCount;
        int pendingSfx3D = audio.PendingSfx3DCount;
        int maxSfx3D = audio.MaxSfx3DVoices;
        int followingSfx3D = audio.FollowingSfx3DCount;
        int maxFollowingSfx3D = audio.MaxFollowingSfx3DVoices;
        _audioSfxValue.Text = $"{activeSfx}/{maxSfx} · 3D {activeSfx3D}/{maxSfx3D}";
        _audioSfxDetail.Text = maxSfx > 0
            ? $"非空间占用 {Mathf.RoundToInt((activeSfx + pendingSfx) * 100f / maxSfx).ToString(CultureInfo.InvariantCulture)}% · " +
              $"等待 {pendingSfx.ToString(CultureInfo.InvariantCulture)} · " +
              $"已准备 {audio.PreparedSfxVoiceCount.ToString(CultureInfo.InvariantCulture)} · " +
              $"拒绝 {audio.RejectedSfxCount.ToString(CultureInfo.InvariantCulture)} · " +
              $"抢占 {audio.PreemptedSfxCount.ToString(CultureInfo.InvariantCulture)}\n" +
              $"3D 等待 {pendingSfx3D.ToString(CultureInfo.InvariantCulture)} · " +
              $"跟随 {followingSfx3D.ToString(CultureInfo.InvariantCulture)}/{maxFollowingSfx3D.ToString(CultureInfo.InvariantCulture)} · " +
              $"已准备 {audio.PreparedSfx3DVoiceCount.ToString(CultureInfo.InvariantCulture)} · " +
              $"拒绝 {audio.RejectedSfx3DCount.ToString(CultureInfo.InvariantCulture)} · " +
              $"抢占 {audio.PreemptedSfx3DCount.ToString(CultureInfo.InvariantCulture)}"
            : "容量未配置";

        SetAudioVolume(_audioMasterVolumeValue, audio.GetVolume(AudioGroup.Master));
        SetAudioVolume(_audioBgmVolumeValue, audio.GetVolume(AudioGroup.Bgm));
        SetAudioVolume(_audioSfxVolumeValue, audio.GetVolume(AudioGroup.Sfx));
    }

    private void SetAudioVolumeUnavailable()
    {
        _audioMasterVolumeValue!.Text = "—";
        _audioMasterVolumeValue.TooltipText = string.Empty;
        _audioBgmVolumeValue!.Text = "—";
        _audioBgmVolumeValue.TooltipText = string.Empty;
        _audioSfxVolumeValue!.Text = "—";
        _audioSfxVolumeValue.TooltipText = string.Empty;
    }

    private static void SetAudioVolume(Label label, float volume)
    {
        label.Text =
            $"{Mathf.RoundToInt(volume * 100f).ToString(CultureInfo.InvariantCulture)}%";
        label.TooltipText =
            $"线性音量 {volume.ToString("0.00", CultureInfo.InvariantCulture)}";
    }


}
#endif
