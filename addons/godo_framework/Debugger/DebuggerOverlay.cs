using System;
using System.Collections.Generic;
using System.Globalization;
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
    private const int MaxDisplayedWarnings = 12;
    private const int MaxDisplayedLogs = 20;
    private const int MaxDisplayedInputActions = 32;
    private const float ExpandedMaxWidth = 520f;
    private const float ExpandedMaxHeight = 420f;
    private const float ScreenMargin = 12f;

    private readonly Queue<DebuggerErrorEntry> _recentWarnings = new(MaxStoredWarnings);
    private readonly StringBuilder _textBuilder = new(1024);
    private readonly List<DebuggerPageGroup> _pageGroups = new();
    private PanelContainer? _panel;
    private Button? _toggleButton;
    private Label? _titleLabel;
    private TabBar? _categoryTabs;
    private TabBar? _pageTabs;
    private ScrollContainer? _content;
    private Label? _debuggerLabel;
    private DebuggerPage? _selectedPage;
    private double _refreshElapsed;
    private bool _expanded;
    private bool _updatingNavigation;

    /// <summary>展开面板的节点路径。</summary>
    [Export] public NodePath PanelPath { get; set; } = null!;
    /// <summary>折叠状态按钮的节点路径。</summary>
    [Export] public NodePath ToggleButtonPath { get; set; } = null!;
    /// <summary>调试内容容器的节点路径。</summary>
    [Export] public NodePath ContentPath { get; set; } = null!;
    /// <summary>调试摘要标签的节点路径。</summary>
    [Export] public NodePath DebuggerLabelPath { get; set; } = null!;
    /// <summary>展开状态标题的节点路径。</summary>
    [Export] public NodePath TitleLabelPath { get; set; } = null!;
    /// <summary>一级诊断分类的节点路径。</summary>
    [Export] public NodePath CategoryTabsPath { get; set; } = null!;
    /// <summary>分类内页面的节点路径。</summary>
    [Export] public NodePath PageTabsPath { get; set; } = null!;

    /// <inheritdoc />
    public override void _EnterTree()
    {
        ErrorHub.OnError += OnErrorReported;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        _panel = GetNodeOrNull<PanelContainer>(PanelPath);
        _toggleButton = GetNodeOrNull<Button>(ToggleButtonPath);
        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _categoryTabs = GetNodeOrNull<TabBar>(CategoryTabsPath);
        _pageTabs = GetNodeOrNull<TabBar>(PageTabsPath);
        _content = GetNodeOrNull<ScrollContainer>(ContentPath);
        _debuggerLabel = GetNodeOrNull<Label>(DebuggerLabelPath);

        if (!IsInstanceValid(_panel) ||
            !IsInstanceValid(_toggleButton) ||
            !IsInstanceValid(_titleLabel) ||
            !IsInstanceValid(_categoryTabs) ||
            !IsInstanceValid(_pageTabs) ||
            !IsInstanceValid(_content) ||
            !IsInstanceValid(_debuggerLabel))
        {
            throw new InvalidOperationException("DebuggerOverlay 场景缺少必要的导出节点引用。");
        }

        RegisterPages();
        ConfigureCategoryTabs();
        _toggleButton.Pressed += OnTogglePressed;
        _categoryTabs.TabChanged += OnCategoryTabChanged;
        _pageTabs.TabChanged += OnPageTabChanged;
        ApplyExpandedState();
        RefreshHealthStatus();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        _refreshElapsed += delta;
        if (_refreshElapsed < RefreshIntervalSeconds)
            return;

        _refreshElapsed = 0d;
        RefreshHealthStatus();
        ApplyPanelSize();
        if (_expanded)
            RefreshDebugger();
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        ErrorHub.OnError -= OnErrorReported;
        if (IsInstanceValid(_toggleButton))
            _toggleButton.Pressed -= OnTogglePressed;
        if (IsInstanceValid(_categoryTabs))
            _categoryTabs.TabChanged -= OnCategoryTabChanged;
        if (IsInstanceValid(_pageTabs))
            _pageTabs.TabChanged -= OnPageTabChanged;

        _panel = null;
        _toggleButton = null;
        _titleLabel = null;
        _categoryTabs = null;
        _pageTabs = null;
        _content = null;
        _debuggerLabel = null;
        _selectedPage = null;
        _pageGroups.Clear();
    }

    private void OnTogglePressed()
    {
        _expanded = !_expanded;
        ApplyExpandedState();
        if (_expanded)
            RefreshDebugger();
    }

    private void ApplyExpandedState()
    {
        if (!IsInstanceValid(_toggleButton) ||
            !IsInstanceValid(_titleLabel) ||
            !IsInstanceValid(_categoryTabs) ||
            !IsInstanceValid(_pageTabs) ||
            !IsInstanceValid(_content))
            return;

        _titleLabel.Visible = _expanded;
        _categoryTabs.Visible = _expanded;
        _pageTabs.Visible = _expanded && _pageTabs.TabCount > 1;
        _content.Visible = _expanded;
        ApplyPanelSize();
    }

    private void ApplyPanelSize()
    {
        if (!IsInstanceValid(_panel))
            return;

        if (!_expanded)
        {
            _panel.Size = new Vector2(156f, 40f);
            return;
        }

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        _panel.Size = new Vector2(
            Mathf.Max(240f, Mathf.Min(ExpandedMaxWidth, viewportSize.X - ScreenMargin * 2f)),
            Mathf.Max(180f, Mathf.Min(ExpandedMaxHeight, viewportSize.Y - ScreenMargin * 2f)));
    }

    private void RefreshHealthStatus()
    {
        if (!IsInstanceValid(_toggleButton))
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

        _toggleButton.Text = _expanded
            ? $"FPS {Mathf.RoundToInt(Engine.GetFramesPerSecond())} | W {warningCount} | E {errorCount}"
            : $"FPS {Mathf.RoundToInt(Engine.GetFramesPerSecond())} | W{warningCount} E{errorCount}";

        Color statusColor = errorCount > 0
            ? new Color(1f, 0.42f, 0.38f)
            : warningCount > 0 ? new Color(1f, 0.76f, 0.28f) : new Color(0.88f, 0.94f, 1f);
        _toggleButton.AddThemeColorOverride("font_color", statusColor);
    }

    private void RefreshDebugger()
    {
        if (!IsInstanceValid(_debuggerLabel))
            return;

        _textBuilder.Clear();
        _selectedPage?.Render();
        _debuggerLabel.Text = _textBuilder.ToString().ReplaceLineEndings("\n");
    }

    private void BeginSection(string title)
    {
        if (_textBuilder.Length > 0)
            _textBuilder.Append('\n');
        _textBuilder.Append(title);
    }

    private void AppendSection(string title)
    {
        BeginSection(title);
        _textBuilder.AppendLine();
    }

    private void RegisterPages()
    {
        RegisterPage("Overview", "概览", "概览", AppendOverview);
        RegisterPage("Runtime/Input", "运行时", "Input", AppendInput);
        RegisterPage("Runtime/Scheduler", "运行时", "Scheduler", AppendScheduler);
        RegisterPage("Framework/Services", "框架", "Services", AppendServicesLine);
        RegisterPage("Framework/Events", "框架", "Events", AppendEventsLine);
        RegisterPage("Console/All", "控制台", "全部", AppendConsole);
        RegisterPage("Console/Debug", "控制台", "Debug", AppendDebugConsole);
        RegisterPage("Console/Info", "控制台", "Info", AppendInfoConsole);
        RegisterPage("Console/Warning", "控制台", "Warning", AppendWarningConsole);
        RegisterPage("Console/Error", "控制台", "Error", AppendErrorConsole);
    }

    private void RegisterPage(string path, string groupTitle, string title, Action render)
    {
        int separatorIndex = path.IndexOf('/');
        string groupPath = separatorIndex < 0 ? path : path[..separatorIndex];
        DebuggerPageGroup? group = null;
        for (int index = 0; index < _pageGroups.Count; index++)
        {
            if (string.Equals(_pageGroups[index].Path, groupPath, StringComparison.Ordinal))
            {
                group = _pageGroups[index];
                break;
            }
        }

        if (group is null)
        {
            group = new DebuggerPageGroup(groupPath, groupTitle);
            _pageGroups.Add(group);
        }

        group.Pages.Add(new DebuggerPage(title, render));
    }

    private void ConfigureCategoryTabs()
    {
        if (!IsInstanceValid(_categoryTabs))
            return;

        _updatingNavigation = true;
        _categoryTabs.TabCount = _pageGroups.Count;
        for (int index = 0; index < _pageGroups.Count; index++)
            _categoryTabs.SetTabTitle(index, _pageGroups[index].Title);
        _categoryTabs.CurrentTab = 0;
        ConfigurePageTabs(0);
        _updatingNavigation = false;
    }

    private void ConfigurePageTabs(int categoryIndex)
    {
        if (!IsInstanceValid(_pageTabs) || categoryIndex < 0 || categoryIndex >= _pageGroups.Count)
            return;

        DebuggerPageGroup group = _pageGroups[categoryIndex];
        _pageTabs.TabCount = group.Pages.Count;
        for (int index = 0; index < group.Pages.Count; index++)
            _pageTabs.SetTabTitle(index, group.Pages[index].Title);

        int selectedIndex = Math.Clamp(group.SelectedIndex, 0, group.Pages.Count - 1);
        _pageTabs.CurrentTab = selectedIndex;
        _pageTabs.Visible = _expanded && group.Pages.Count > 1;
        _selectedPage = group.Pages[selectedIndex];
        if (IsInstanceValid(_titleLabel))
            _titleLabel.Text = _selectedPage.Title;
    }

    private void OnCategoryTabChanged(long tab)
    {
        if (_updatingNavigation)
            return;

        int categoryIndex = checked((int)tab);
        _updatingNavigation = true;
        ConfigurePageTabs(categoryIndex);
        _updatingNavigation = false;
        RefreshDebugger();
    }

    private void OnPageTabChanged(long tab)
    {
        if (_updatingNavigation || !IsInstanceValid(_categoryTabs))
            return;

        int categoryIndex = _categoryTabs.CurrentTab;
        if (categoryIndex < 0 || categoryIndex >= _pageGroups.Count)
            return;

        DebuggerPageGroup group = _pageGroups[categoryIndex];
        int pageIndex = checked((int)tab);
        if (pageIndex < 0 || pageIndex >= group.Pages.Count)
            return;

        group.SelectedIndex = pageIndex;
        _selectedPage = group.Pages[pageIndex];
        if (IsInstanceValid(_titleLabel))
            _titleLabel.Text = _selectedPage.Title;
        RefreshDebugger();
    }

    private void AppendOverview()
    {
        AppendRuntimeLine();
        AppendSceneLine();
        AppendAudioLine();
        AppendOverviewCounts();
    }

    private void AppendOverviewCounts()
    {
        Type[] services = Services.GetDebugSnapshot();
        EventChannel.EventDebugEntry[] events = EventChannel.GetDebugSnapshot();
        int listenerCount = 0;
        for (int index = 0; index < events.Length; index++)
            listenerCount += events[index].ListenerCount;

        AppendSection("【框架状态】");
        _textBuilder.Append("已注册服务：").Append(services.Length)
            .Append("    事件类型：").Append(events.Length)
            .Append("    监听器：").AppendLine(listenerCount.ToString());

        if (Services.TryGet<IInputService>(out IInputService? input) && input is not null)
            _textBuilder.Append("输入设备：").AppendLine(input.ActiveDevice.ToString());
        if (Services.TryGet<ISchedulerService>(out ISchedulerService? scheduler) &&
            scheduler is SchedulerService schedulerService)
        {
            SchedulerDebugSnapshot snapshot = schedulerService.GetDebugSnapshot();
            _textBuilder.Append("调度任务：").Append(snapshot.ActiveCount)
                .Append("    暂停：").AppendLine(snapshot.PausedCount.ToString());
        }
    }

    private void AppendInput()
    {
        if (!Services.TryGet<IInputService>(out IInputService? input) || input is not InputService inputService)
        {
            _textBuilder.AppendLine("InputService 不可用。");
            return;
        }

        InputDebugSnapshot snapshot = inputService.GetDebugSnapshot();
        AppendSection("【输入后端】");
        _textBuilder.Append("状态：").Append(snapshot.IsReady ? "Ready" : "未安装")
            .Append("    采样：").Append(snapshot.HasSample ? "正常" : "等待首次采样").AppendLine()
            .Append("实现：").AppendLine(snapshot.IsReady ? snapshot.BackendName : "无")
            .Append("活动设备：").Append(snapshot.ActiveDevice)
            .Append("    Frame：").AppendLine(snapshot.Sequence.ToString(CultureInfo.InvariantCulture))
            .Append("能力：").AppendLine(snapshot.Capabilities.ToString());

        AppendSection("【Context 栈】");
        if (snapshot.Contexts.Length == 0)
        {
            _textBuilder.AppendLine("未设置");
        }
        else
        {
            for (int index = 0; index < snapshot.Contexts.Length; index++)
            {
                InputDebugContextEntry entry = snapshot.Contexts[index];
                _textBuilder.Append(index + 1).Append(". ")
                    .Append(entry.Context.Value).Append("  ")
                    .Append(entry.Mode).Append("  ")
                    .AppendLine(entry.IsEffective ? "有效" : "被屏蔽");
            }
        }

        BeginSection("【Action】 共 ");
        _textBuilder
            .Append(snapshot.Actions.Length).AppendLine(" 个");
        int displayedCount = Math.Min(snapshot.Actions.Length, MaxDisplayedInputActions);
        for (int index = 0; index < displayedCount; index++)
            AppendInputAction(snapshot.Actions[index]);
        if (snapshot.Actions.Length > displayedCount)
        {
            _textBuilder.Append("… 省略 ")
                .Append(snapshot.Actions.Length - displayedCount).AppendLine(" 个 Action");
        }
    }

    private void AppendInputAction(InputDebugActionEntry entry)
    {
        _textBuilder.Append(entry.Action.Value).Append("  ")
            .Append(entry.ValueType).Append("  ");

        switch (entry.ValueType)
        {
            case InputActionValueType.Bool:
                _textBuilder.Append(entry.Pressed ? "Pressed" : "Released");
                break;
            case InputActionValueType.Axis1D:
                AppendFloat(entry.Value.X);
                break;
            case InputActionValueType.Axis2D:
                _textBuilder.Append('(');
                AppendFloat(entry.Value.X);
                _textBuilder.Append(", ");
                AppendFloat(entry.Value.Y);
                _textBuilder.Append(')');
                break;
            case InputActionValueType.Axis3D:
                _textBuilder.Append('(');
                AppendFloat(entry.Value.X);
                _textBuilder.Append(", ");
                AppendFloat(entry.Value.Y);
                _textBuilder.Append(", ");
                AppendFloat(entry.Value.Z);
                _textBuilder.Append(')');
                break;
        }

        if (entry.JustPressed)
            _textBuilder.Append("  JustPressed");
        if (entry.JustReleased)
            _textBuilder.Append("  JustReleased");
        _textBuilder.AppendLine();
    }

    private void AppendFloat(float value) =>
        _textBuilder.Append(value.ToString("0.00", CultureInfo.InvariantCulture));

    private void AppendScheduler()
    {
        if (!Services.TryGet<ISchedulerService>(out ISchedulerService? scheduler) ||
            scheduler is not SchedulerService schedulerService)
        {
            _textBuilder.AppendLine("SchedulerService 不可用。");
            return;
        }

        SchedulerDebugSnapshot snapshot = schedulerService.GetDebugSnapshot();
        AppendSection("【任务】");
        _textBuilder.Append("活跃：").Append(snapshot.ActiveCount)
            .Append("    暂停：").Append(snapshot.PausedCount)
            .Append("    重复：").AppendLine(snapshot.RepeatingCount.ToString())
            .Append("下次触发：");
        if (snapshot.NextRemainingSeconds.HasValue)
            _textBuilder.Append(snapshot.NextRemainingSeconds.Value.ToString("0.000", CultureInfo.InvariantCulture)).AppendLine("s");
        else
            _textBuilder.AppendLine("无");

        AppendSection("【Process】");
        _textBuilder.Append("GameTime：").Append(snapshot.GameProcessCount)
            .Append("    Unscaled：").Append(snapshot.UnscaledProcessCount)
            .Append("    RealTime：").AppendLine(snapshot.RealProcessCount.ToString())
            .Append("最近派发：").AppendLine(snapshot.LastProcessDispatchCount.ToString());

        AppendSection("【Physics】");
        _textBuilder.Append("GameTime：").Append(snapshot.GamePhysicsCount)
            .Append("    Unscaled：").Append(snapshot.UnscaledPhysicsCount)
            .Append("    RealTime：").AppendLine(snapshot.RealPhysicsCount.ToString())
            .Append("最近派发：").AppendLine(snapshot.LastPhysicsDispatchCount.ToString());

        AppendSection("【累计统计】");
        _textBuilder.Append("主动取消：").Append(snapshot.CanceledCount)
            .Append("    Owner 失效：").AppendLine(snapshot.OwnerCanceledCount.ToString())
            .Append("回调失败：").AppendLine(snapshot.CallbackFailedCount.ToString());
    }

    private void AppendConsole()
    {
        LogEntry[] logs = LogHub.GetDebugSnapshot();
        AppendConsoleCounts(logs);
        AppendLogs(logs);
        AppendWarnings();
    }

    private void AppendDebugConsole()
    {
        LogEntry[] logs = LogHub.GetDebugSnapshot();
        AppendConsoleCounts(logs);
        AppendLogs(logs, LogLevel.Debug);
    }

    private void AppendInfoConsole()
    {
        LogEntry[] logs = LogHub.GetDebugSnapshot();
        AppendConsoleCounts(logs);
        AppendLogs(logs, LogLevel.Info);
    }

    private void AppendWarningConsole()
    {
        AppendConsoleCounts(LogHub.GetDebugSnapshot());
        AppendWarnings(DebuggerErrorFilter.Warning);
    }

    private void AppendErrorConsole()
    {
        AppendConsoleCounts(LogHub.GetDebugSnapshot());
        AppendWarnings(DebuggerErrorFilter.Error);
    }

    private void AppendConsoleCounts(LogEntry[] logs)
    {
        int debugCount = 0;
        int infoCount = 0;
        for (int index = 0; index < logs.Length; index++)
        {
            if (logs[index].Level == LogLevel.Debug)
                debugCount++;
            else
                infoCount++;
        }

        int warningCount = 0;
        int errorCount = 0;
        foreach (DebuggerErrorEntry entry in _recentWarnings)
        {
            if (entry.Level >= ErrorLevel.Error)
                errorCount++;
            else
                warningCount++;
        }

        _textBuilder.Append("全部：").Append(logs.Length + warningCount + errorCount)
            .Append("    Debug：").Append(debugCount)
            .Append("    Info：").Append(infoCount)
            .Append("    Warning：").Append(warningCount)
            .Append("    Error：").AppendLine(errorCount.ToString());
    }

    private void AppendRuntimeLine()
    {
        AppendSection("【运行时】");
        _textBuilder.Append("主线程：").Append(MainThreadGuard.IsMainThread ? "正常" : "异常")
            .Append("    资源加载：").AppendLine(ResourceHub.ActiveOperationCount.ToString());
    }

    private void AppendSceneLine()
    {
        AppendSection("【场景】");
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
        AppendSection("【音频】");
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
        AppendSection("【已注册服务】");
        Type[] services = Services.GetDebugSnapshot();
        if (services.Length == 0)
        {
            _textBuilder.AppendLine("无");
            return;
        }

        _textBuilder.Append("共 ").Append(services.Length).AppendLine(" 个");
        for (int i = 0; i < services.Length; i++)
            _textBuilder.Append(i + 1).Append(". ").AppendLine(services[i].Name);
    }

    private void AppendEventsLine()
    {
        EventChannel.EventDebugEntry[] events = EventChannel.GetDebugSnapshot();
        int listenerCount = 0;
        for (int i = 0; i < events.Length; i++)
            listenerCount += events[i].ListenerCount;

        AppendSection("【事件通道】");
        _textBuilder.Append("事件类型：").Append(events.Length)
            .Append("    监听器：").AppendLine(listenerCount.ToString());

        if (events.Length == 0)
        {
            _textBuilder.AppendLine("暂无监听器");
            return;
        }

        for (int index = 0; index < events.Length; index++)
        {
            EventChannel.EventDebugEntry entry = events[index];
            _textBuilder.Append(entry.EventType.Name)
                .Append("    ").Append(entry.ListenerCount).AppendLine();
        }
    }

    private void AppendWarnings(DebuggerErrorFilter filter = DebuggerErrorFilter.All)
    {
        int matchingCount = 0;
        foreach (DebuggerErrorEntry entry in _recentWarnings)
        {
            if (MatchesFilter(entry, filter))
                matchingCount++;
        }
        if (matchingCount == 0)
            return;

        AppendSection("【最近警告 / 错误】");
        int skipCount = Math.Max(0, matchingCount - MaxDisplayedWarnings);
        int index = 0;
        foreach (DebuggerErrorEntry error in _recentWarnings)
        {
            if (!MatchesFilter(error, filter))
                continue;
            if (index++ < skipCount)
                continue;

            _textBuilder.Append(error.Timestamp.ToString("HH:mm:ss"))
                .Append(' ').Append('[').Append(error.Level).Append("] ")
                .Append(error.Module).Append(": ").AppendLine(error.Message);
        }
    }

    private static bool MatchesFilter(DebuggerErrorEntry entry, DebuggerErrorFilter filter) =>
        filter switch
        {
            DebuggerErrorFilter.Warning => entry.Level == ErrorLevel.Warning,
            DebuggerErrorFilter.Error => entry.Level >= ErrorLevel.Error,
            _ => true,
        };

    private void AppendLogs(LogEntry[] logs, LogLevel? filter = null)
    {
        int matchingCount = 0;
        for (int index = 0; index < logs.Length; index++)
        {
            if (!filter.HasValue || logs[index].Level == filter.Value)
                matchingCount++;
        }
        if (matchingCount == 0)
            return;

        AppendSection("【最近日志】");
        int skipCount = Math.Max(0, matchingCount - MaxDisplayedLogs);
        int matchingIndex = 0;
        for (int i = 0; i < logs.Length; i++)
        {
            LogEntry log = logs[i];
            if (filter.HasValue && log.Level != filter.Value)
                continue;
            if (matchingIndex++ < skipCount)
                continue;
            _textBuilder.Append(log.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"))
                .Append(' ').Append('[').Append(log.Level).Append("] ")
                .Append(log.Module).Append(": ");

            if (!string.IsNullOrWhiteSpace(log.Context))
                _textBuilder.Append('(').Append(log.Context).Append(") ");

            _textBuilder.AppendLine(log.Message);
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

    private sealed class DebuggerPageGroup
    {
        public string Path { get; }
        public string Title { get; }
        public List<DebuggerPage> Pages { get; } = new();
        public int SelectedIndex { get; set; }

        public DebuggerPageGroup(string path, string title)
        {
            Path = path;
            Title = title;
        }
    }

    private sealed class DebuggerPage
    {
        public string Title { get; }
        public Action Render { get; }

        public DebuggerPage(string title, Action render)
        {
            Title = title;
            Render = render;
        }
    }

    private enum DebuggerErrorFilter
    {
        All,
        Warning,
        Error,
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
