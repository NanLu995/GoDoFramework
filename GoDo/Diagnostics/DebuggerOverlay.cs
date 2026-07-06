using System;
using System.Collections.Generic;
using System.Text;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>Debug 构建中的紧凑只读框架状态面板。</summary>
public sealed partial class DebuggerOverlay : CanvasLayer
{
#if DEBUG
    private const double RefreshIntervalSeconds = 0.25;
    private const int MaxStoredWarnings = 16;
    private const int MaxDisplayedWarnings = 4;
    private const float ExpandedMaxWidth = 380f;
    private const float ExpandedMaxHeight = 360f;
    private const float ScreenMargin = 12f;

    private readonly Queue<DebuggerErrorEntry> _recentWarnings = new(MaxStoredWarnings);
    private readonly StringBuilder _textBuilder = new(1024);
    private PanelContainer? _panel;
    private Button? _toggleButton;
    private ScrollContainer? _content;
    private Label? _diagnosticsLabel;
    private double _refreshElapsed;
    private bool _expanded;

    [Export] public NodePath PanelPath { get; set; } = null!;
    [Export] public NodePath ToggleButtonPath { get; set; } = null!;
    [Export] public NodePath ContentPath { get; set; } = null!;
    [Export] public NodePath DiagnosticsLabelPath { get; set; } = null!;

    public override void _EnterTree()
    {
        ErrorHub.OnError += OnErrorReported;
    }

    public override void _Ready()
    {
        _panel = GetNodeOrNull<PanelContainer>(PanelPath);
        _toggleButton = GetNodeOrNull<Button>(ToggleButtonPath);
        _content = GetNodeOrNull<ScrollContainer>(ContentPath);
        _diagnosticsLabel = GetNodeOrNull<Label>(DiagnosticsLabelPath);

        if (!IsInstanceValid(_panel) ||
            !IsInstanceValid(_toggleButton) ||
            !IsInstanceValid(_content) ||
            !IsInstanceValid(_diagnosticsLabel))
        {
            throw new InvalidOperationException("DebuggerOverlay 场景缺少必要的导出节点引用。");
        }

        _toggleButton.Pressed += OnTogglePressed;
        ApplyExpandedState();
        RefreshFps();
    }

    public override void _Process(double delta)
    {
        _refreshElapsed += delta;
        if (_refreshElapsed < RefreshIntervalSeconds)
            return;

        _refreshElapsed = 0d;
        RefreshFps();
        ApplyPanelSize();
        if (_expanded)
            RefreshDiagnostics();
    }

    public override void _ExitTree()
    {
        ErrorHub.OnError -= OnErrorReported;
        if (IsInstanceValid(_toggleButton))
            _toggleButton.Pressed -= OnTogglePressed;

        _panel = null;
        _toggleButton = null;
        _content = null;
        _diagnosticsLabel = null;
    }

    private void OnTogglePressed()
    {
        _expanded = !_expanded;
        ApplyExpandedState();
        if (_expanded)
            RefreshDiagnostics();
    }

    private void ApplyExpandedState()
    {
        if (!IsInstanceValid(_toggleButton) || !IsInstanceValid(_content))
            return;

        _content.Visible = _expanded;
        ApplyPanelSize();
    }

    private void ApplyPanelSize()
    {
        if (!IsInstanceValid(_panel))
            return;

        if (!_expanded)
        {
            _panel.Size = new Vector2(116f, 40f);
            return;
        }

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        _panel.Size = new Vector2(
            Mathf.Max(240f, Mathf.Min(ExpandedMaxWidth, viewportSize.X - ScreenMargin * 2f)),
            Mathf.Max(180f, Mathf.Min(ExpandedMaxHeight, viewportSize.Y - ScreenMargin * 2f)));
    }

    private void RefreshFps()
    {
        if (IsInstanceValid(_toggleButton))
            _toggleButton.Text = $"FPS: {Mathf.RoundToInt(Engine.GetFramesPerSecond())}";
    }

    private void RefreshDiagnostics()
    {
        if (!IsInstanceValid(_diagnosticsLabel))
            return;

        _textBuilder.Clear();
        AppendRuntimeLine();
        AppendSceneLine();
        AppendAudioLine();
        AppendServicesLine();
        AppendEventsLine();
        AppendWarnings();
        _diagnosticsLabel.Text = _textBuilder.ToString();
    }

    private void AppendRuntimeLine()
    {
        _textBuilder.AppendLine("【运行时】")
            .Append("主线程：").Append(MainThreadGuard.IsMainThread ? "正常" : "异常")
            .Append("    资源加载：").AppendLine(ResourceHub.ActiveOperationCount.ToString());
    }

    private void AppendSceneLine()
    {
        _textBuilder.AppendLine().AppendLine("【场景】");
        if (!Services.TryGet<ISceneService>(out ISceneService? scene) || scene is null)
        {
            _textBuilder.AppendLine("服务不可用");
            return;
        }

        _textBuilder.Append(scene.IsChanging ? "切换中" : "空闲")
            .Append("    进度：").Append(Mathf.RoundToInt(scene.Progress * 100f)).AppendLine("%");
    }

    private void AppendAudioLine()
    {
        _textBuilder.AppendLine().AppendLine("【音频】");
        if (!Services.TryGet<IAudioService>(out IAudioService? audio) || audio is null)
        {
            _textBuilder.AppendLine("服务不可用");
            return;
        }

        string bgmState = audio.IsBgmLoading
            ? "加载中"
            : audio.IsBgmPlaying ? "播放中" : "已停止";
        _textBuilder.Append("BGM：").Append(bgmState)
            .Append("    SFX：").Append(audio.ActiveSfxCount)
            .Append('/').AppendLine(audio.MaxSfxVoices.ToString());
    }

    private void AppendServicesLine()
    {
        _textBuilder.AppendLine().AppendLine("【已注册服务】");
        Type[] services = Services.GetDebugSnapshot();
        if (services.Length == 0)
        {
            _textBuilder.AppendLine("无");
            return;
        }

        for (int i = 0; i < services.Length; i++)
        {
            if (i > 0)
                _textBuilder.Append(", ");
            _textBuilder.Append(services[i].Name);
        }
        _textBuilder.AppendLine();
    }

    private void AppendEventsLine()
    {
        EventChannel.EventDebugEntry[] events = EventChannel.GetDebugSnapshot();
        int listenerCount = 0;
        for (int i = 0; i < events.Length; i++)
            listenerCount += events[i].ListenerCount;

        _textBuilder.AppendLine().AppendLine("【事件通道】")
            .Append("事件类型：").Append(events.Length)
            .Append("    监听器：").AppendLine(listenerCount.ToString());
    }

    private void AppendWarnings()
    {
        if (_recentWarnings.Count == 0)
            return;

        _textBuilder.AppendLine().AppendLine("【最近警告 / 错误】");
        int skipCount = Math.Max(0, _recentWarnings.Count - MaxDisplayedWarnings);
        int index = 0;
        foreach (DebuggerErrorEntry error in _recentWarnings)
        {
            if (index++ < skipCount)
                continue;

            _textBuilder.Append(error.Timestamp.ToString("HH:mm:ss"))
                .Append(' ').Append('[').Append(error.Level).Append("] ")
                .Append(error.Module).Append(": ").AppendLine(error.Message);
        }
    }

    private void OnErrorReported(ErrorReport report)
    {
        if (report.Level < ErrorLevel.Warning)
            return;

        if (_recentWarnings.Count >= MaxStoredWarnings)
            _recentWarnings.Dequeue();

        _recentWarnings.Enqueue(new DebuggerErrorEntry(
            report.Timestamp,
            report.Level,
            report.Module,
            report.Message));
    }

    private readonly struct DebuggerErrorEntry
    {
        public DateTime Timestamp { get; }
        public ErrorLevel Level { get; }
        public string Module { get; }
        public string Message { get; }

        public DebuggerErrorEntry(DateTime timestamp, ErrorLevel level, string module, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Module = module;
            Message = message;
        }
    }
#else
    public override void _Ready()
    {
        QueueFree();
    }
#endif
}
