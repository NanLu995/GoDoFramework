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
    private Button? _overviewWarningButton;
    private Button? _overviewErrorButton;

    private void CacheOverviewNodes()
    {
        _overviewFpsValue = GetOverviewLabel("Content/StatusGrid/FpsCard/Content/Value");
        _overviewWarningValue = GetOverviewLabel("Content/StatusGrid/WarningCard/Content/Value");
        _overviewErrorValue = GetOverviewLabel("Content/StatusGrid/ErrorCard/Content/Value");
        _overviewWarningButton = GetOverviewButton("Content/StatusGrid/WarningCard");
        _overviewErrorButton = GetOverviewButton("Content/StatusGrid/ErrorCard");
        _overviewServicesValue = GetOverviewLabel("Content/MetricGrid/ServicesCard/Content/Value");
        _overviewEventsValue = GetOverviewLabel("Content/MetricGrid/EventsCard/Content/Value");
        _overviewEventsDetail = GetOverviewLabel("Content/MetricGrid/EventsCard/Content/Detail");
        _overviewResourcesValue = GetOverviewLabel("Content/MetricGrid/ResourcesCard/Content/Value");
        _overviewResourcesDetail = GetOverviewLabel("Content/MetricGrid/ResourcesCard/Content/Detail");
        _overviewSceneValue = GetOverviewLabel("Content/MetricGrid/SceneCard/Content/Value");
        _overviewSceneDetail = GetOverviewLabel("Content/MetricGrid/SceneCard/Content/Detail");
        _overviewAudioValue = GetOverviewLabel("Content/ActivityGrid/AudioCard/Content/Value");
        _overviewAudioDetail = GetOverviewLabel("Content/ActivityGrid/AudioCard/Content/Detail");
        _overviewInputValue = GetOverviewLabel("Content/ActivityGrid/InputCard/Content/Value");
        _overviewInputDetail = GetOverviewLabel("Content/ActivityGrid/InputCard/Content/Detail");
        _overviewSchedulerValue = GetOverviewLabel("Content/ActivityGrid/SchedulerCard/Content/Value");
        _overviewSchedulerDetail = GetOverviewLabel("Content/ActivityGrid/SchedulerCard/Content/Detail");
    }

    private Label GetOverviewLabel(string path)
    {
        Label? label = _overviewDashboard!.GetNodeOrNull<Label>(path);
        return IsInstanceValid(label)
            ? label
            : throw new InvalidOperationException($"DebuggerOverview 场景缺少节点：{path}");
    }

    private Button GetOverviewButton(string path)
    {
        Button? button = _overviewDashboard!.GetNodeOrNull<Button>(path);
        return IsInstanceValid(button)
            ? button
            : throw new InvalidOperationException($"DebuggerOverview 场景缺少节点：{path}");
    }

    private void OnOverviewWarningPressed() =>
        OpenConsoleWithLevelFilter(ConsoleLevelFilter.Warning);

    private void OnOverviewErrorPressed() =>
        OpenConsoleWithLevelFilter(ConsoleLevelFilter.Error);


    private void RefreshOverviewDashboard()
    {
        if (!IsInstanceValid(_overviewFpsValue) ||
            !IsInstanceValid(_overviewWarningValue) ||
            !IsInstanceValid(_overviewErrorValue) ||
            !IsInstanceValid(_overviewServicesValue) ||
            !IsInstanceValid(_overviewEventsValue) ||
            !IsInstanceValid(_overviewEventsDetail) ||
            !IsInstanceValid(_overviewResourcesValue) ||
            !IsInstanceValid(_overviewResourcesDetail) ||
            !IsInstanceValid(_overviewSceneValue) ||
            !IsInstanceValid(_overviewSceneDetail) ||
            !IsInstanceValid(_overviewAudioValue) ||
            !IsInstanceValid(_overviewAudioDetail) ||
            !IsInstanceValid(_overviewInputValue) ||
            !IsInstanceValid(_overviewInputDetail) ||
            !IsInstanceValid(_overviewSchedulerValue) ||
            !IsInstanceValid(_overviewSchedulerDetail))
            return;

        int warningCount = 0;
        int errorCount = 0;
        foreach (DebuggerErrorEntry entry in _recentWarnings)
        {
            if (entry.Level >= ErrorLevel.Error)
                errorCount++;
            else
                warningCount++;
        }

        _overviewFpsValue.Text = Mathf.RoundToInt(Engine.GetFramesPerSecond())
            .ToString(CultureInfo.InvariantCulture);
        _overviewWarningValue.Text = warningCount.ToString(CultureInfo.InvariantCulture);
        _overviewErrorValue.Text = errorCount.ToString(CultureInfo.InvariantCulture);
        _overviewWarningValue.AddThemeColorOverride("font_color",
            warningCount > 0 ? new Color(1f, 0.72f, 0.28f) : new Color(0.58f, 0.65f, 0.73f));
        _overviewErrorValue.AddThemeColorOverride("font_color",
            errorCount > 0 ? new Color(1f, 0.38f, 0.34f) : new Color(0.58f, 0.65f, 0.73f));

        Services.ServiceDebugEntry[] services = Services.GetDebugSnapshot();
        EventChannel.EventDebugEntry[] events = EventChannel.GetDebugSnapshot();
        int listenerCount = 0;
        for (int index = 0; index < events.Length; index++)
            listenerCount += events[index].ListenerCount;

        _overviewServicesValue.Text = services.Length.ToString(CultureInfo.InvariantCulture);
        _overviewEventsValue.Text = events.Length.ToString(CultureInfo.InvariantCulture);
        _overviewEventsDetail.Text = $"{listenerCount} 个监听器";
        _overviewResourcesValue.Text = ResourceHub.ActiveOperationCount.ToString(CultureInfo.InvariantCulture);
        _overviewResourcesDetail.Text =
            MainThreadGuard.IsMainThread ? "主线程正常" : "主线程异常";

        if (Services.TryGet<ISceneService>(out ISceneService? scene) && scene is not null)
        {
            _overviewSceneValue.Text = scene.IsChanging ? "切换中" : "空闲";
            _overviewSceneDetail.Text =
                $"进度 {Mathf.RoundToInt(scene.Progress * 100f).ToString(CultureInfo.InvariantCulture)}%";
        }
        else
        {
            _overviewSceneValue.Text = "不可用";
            _overviewSceneDetail.Text = "SceneService";
        }

        if (Services.TryGet<IAudioService>(out IAudioService? audio) && audio is not null)
        {
            _overviewAudioValue.Text = audio.IsBgmLoading
                ? "加载中"
                : audio.IsBgmPlaying ? "播放中" : "已停止";
            _overviewAudioDetail.Text =
                $"SFX {audio.ActiveSfxCount}+{audio.PendingSfxCount}/{audio.MaxSfxVoices}";
        }
        else
        {
            _overviewAudioValue.Text = "未注册";
            _overviewAudioDetail.Text = "AudioService";
        }

        if (!Services.TryGet<IInputService>(out IInputService? input) || input is null)
        {
            _overviewInputValue.Text = "未注册";
            _overviewInputDetail.Text = "InputService";
        }
        else if (input is InputService inputService)
        {
            InputDebugSnapshot snapshot = inputService.GetDebugSnapshot();
            _overviewInputValue.Text = snapshot.ActiveDevice.ToString();
            _overviewInputDetail.Text = "当前活动设备";
        }
        else
        {
            _overviewInputValue.Text = "不支持";
            _overviewInputDetail.Text = "无 Debug 快照";
        }

        if (!Services.TryGet<ISchedulerService>(out ISchedulerService? scheduler) ||
            scheduler is null)
        {
            _overviewSchedulerValue.Text = "未注册";
            _overviewSchedulerDetail.Text = "SchedulerService";
        }
        else if (scheduler is SchedulerService schedulerService)
        {
            SchedulerDebugSnapshot snapshot = schedulerService.GetDebugSnapshot();
            _overviewSchedulerValue.Text = snapshot.ActiveCount.ToString(CultureInfo.InvariantCulture);
            _overviewSchedulerDetail.Text = $"{snapshot.PausedCount} 个暂停";
        }
        else
        {
            _overviewSchedulerValue.Text = "不支持";
            _overviewSchedulerDetail.Text = "无 Debug 快照";
        }
    }


}
#endif
