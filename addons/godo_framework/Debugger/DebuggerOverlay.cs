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
    private const double ConsoleBottomThreshold = 1d;
    private const int MaxStoredWarnings = 16;
    private const int MaxDisplayedWarnings = 12;
    private const int ConsoleLogsPerPage = 100;
    private const int MaxDisplayedInputActions = 32;
    private const float MinimumInputContextHeight = 100f;
    private const float MaximumInputContextHeight = 164f;
    private const float InputContextRowHeight = 22f;
    private const float DefaultExpandedWidth = 720f;
    private const float DefaultExpandedHeight = 440f;
    private const float MinimumExpandedWidth = 480f;
    private const float MinimumExpandedHeight = 300f;
    private const float ScreenMargin = 12f;
    private const float VisibleHeaderWidth = 96f;
    private const float VisibleHeaderHeight = 36f;

    private readonly Queue<DebuggerErrorEntry> _recentWarnings = new(MaxStoredWarnings);
    private readonly StringBuilder _textBuilder = new(1024);
    private readonly List<DebuggerPageGroup> _pageGroups = new();
    private readonly Dictionary<TreeItem, DebuggerPage> _pagesByTreeItem = new();
    private PanelContainer? _panel;
    private Control? _header;
    private Button? _toggleButton;
    private Label? _titleLabel;
    private Button? _resetLayoutButton;
    private Control? _body;
    private Tree? _navigationTree;
    private Control? _overviewDashboard;
    private Label? _overviewFpsValue;
    private Label? _overviewWarningValue;
    private Label? _overviewErrorValue;
    private Label? _overviewServicesValue;
    private Label? _overviewEventsValue;
    private Label? _overviewEventsDetail;
    private Label? _overviewResourcesValue;
    private Label? _overviewResourcesDetail;
    private Label? _overviewSceneValue;
    private Label? _overviewSceneDetail;
    private Label? _overviewAudioValue;
    private Label? _overviewAudioDetail;
    private Label? _overviewInputValue;
    private Label? _overviewInputDetail;
    private Label? _overviewSchedulerValue;
    private Label? _overviewSchedulerDetail;
    private Control? _inputDashboard;
    private Label? _inputBackendValue;
    private Label? _inputBackendDetail;
    private Label? _inputDeviceValue;
    private Label? _inputFrameValue;
    private Label? _inputFrameDetail;
    private Label? _inputActionsValue;
    private Label? _inputCapabilities;
    private Tree? _inputContextsTree;
    private LineEdit? _inputActionsSearch;
    private Label? _inputActionsMatchStatus;
    private Tree? _inputActionsTree;
    private Control? _schedulerDashboard;
    private Label? _schedulerActiveValue;
    private Label? _schedulerPausedValue;
    private Label? _schedulerRepeatingValue;
    private Label? _schedulerNextValue;
    private Label? _schedulerProcessGameValue;
    private Label? _schedulerProcessUnscaledValue;
    private Label? _schedulerProcessRealValue;
    private Label? _schedulerProcessDispatchValue;
    private Label? _schedulerPhysicsGameValue;
    private Label? _schedulerPhysicsUnscaledValue;
    private Label? _schedulerPhysicsRealValue;
    private Label? _schedulerPhysicsDispatchValue;
    private Label? _schedulerCanceledValue;
    private Label? _schedulerOwnerCanceledValue;
    private Label? _schedulerFailedValue;
    private Control? _audioDashboard;
    private Label? _audioBgmStateValue;
    private Label? _audioBgmStateDetail;
    private Label? _audioBgmResourceValue;
    private Label? _audioSfxValue;
    private Label? _audioSfxDetail;
    private Label? _audioMasterVolumeValue;
    private Label? _audioBgmVolumeValue;
    private Label? _audioSfxVolumeValue;
    private Control? _servicesDashboard;
    private LineEdit? _servicesSearch;
    private Label? _servicesContractsValue;
    private Label? _servicesImplementationsValue;
    private Label? _servicesMatchStatus;
    private Tree? _servicesTree;
    private Label? _servicesSelectionDetail;
    private Control? _eventsDashboard;
    private LineEdit? _eventsSearch;
    private Label? _eventsTypesValue;
    private Label? _eventsListenersValue;
    private Label? _eventsMatchStatus;
    private Tree? _eventsTree;
    private Label? _eventsSelectionDetail;
    private RichTextLabel? _debuggerLabel;
    private VScrollBar? _consoleScrollBar;
    private Control? _consoleToolbar;
    private Control? _consoleFilters;
    private Control? _consolePagination;
    private LineEdit? _consoleSearch;
    private Button? _allConsoleFilterButton;
    private Button? _debugConsoleFilterButton;
    private Button? _infoConsoleFilterButton;
    private Button? _warningConsoleFilterButton;
    private Button? _errorConsoleFilterButton;
    private Button? _pauseConsoleButton;
    private Button? _copyConsoleButton;
    private Button? _olderConsolePageButton;
    private Button? _newerConsolePageButton;
    private Button? _latestConsolePageButton;
    private Label? _consolePageStatus;
    private Control? _resizeRow;
    private Control? _resizeGrip;
    private DebuggerPage? _selectedPage;
    private double _refreshElapsed;
    private Vector2 _expandedSize = new(DefaultExpandedWidth, DefaultExpandedHeight);
    private Vector2 _pointerStart;
    private Vector2 _panelPositionStart;
    private Vector2 _panelSizeStart;
    private string _consoleSearchQuery = string.Empty;
    private string _inputActionsSearchQuery = string.Empty;
    private int _inputContextsSignature = int.MinValue;
    private int _inputActionsSignature = int.MinValue;
    private string _servicesSearchQuery = string.Empty;
    private int _servicesSnapshotSignature = int.MinValue;
    private string _eventsSearchQuery = string.Empty;
    private int _eventsSnapshotSignature = int.MinValue;
    private ConsoleLevelFilter _consoleLevelFilter = ConsoleLevelFilter.All;
    private int _consoleErrorVersion;
    private int _lastConsoleErrorVersion = -1;
    private int _lastConsoleLogVersion = -1;
    private int _consolePageOffset;
    private bool _expanded;
    private bool _dragging;
    private bool _resizing;
    private bool _consoleRefreshPaused;
    private bool _consoleFollowLatest = true;
    private bool _applyingConsoleScroll;
    private bool _consoleScrollDeferred;
    private bool _consoleScrollEvaluationDeferred;

    internal int ConsoleRenderCount { get; private set; }
    internal int ConsoleScrollToBottomCount { get; private set; }
    internal int ConsoleScrollEvaluationCount { get; private set; }

    /// <summary>展开面板的节点路径。</summary>
    [Export] public NodePath PanelPath { get; set; } = null!;
    /// <summary>处理窗口拖动的标题栏节点路径。</summary>
    [Export] public NodePath HeaderPath { get; set; } = null!;
    /// <summary>折叠状态按钮的节点路径。</summary>
    [Export] public NodePath ToggleButtonPath { get; set; } = null!;
    /// <summary>恢复默认窗口布局按钮的节点路径。</summary>
    [Export] public NodePath ResetLayoutButtonPath { get; set; } = null!;
    /// <summary>展开状态内容区域的节点路径。</summary>
    [Export] public NodePath BodyPath { get; set; } = null!;
    /// <summary>调试摘要标签的节点路径。</summary>
    [Export] public NodePath DebuggerLabelPath { get; set; } = null!;
    /// <summary>展开状态标题的节点路径。</summary>
    [Export] public NodePath TitleLabelPath { get; set; } = null!;
    /// <summary>树状页面导航的节点路径。</summary>
    [Export] public NodePath NavigationTreePath { get; set; } = null!;
    /// <summary>概览仪表盘根节点路径。</summary>
    [Export] public NodePath OverviewDashboardPath { get; set; } = null!;
    /// <summary>Input 仪表盘根节点路径。</summary>
    [Export] public NodePath InputDashboardPath { get; set; } = null!;
    /// <summary>Scheduler 仪表盘根节点路径。</summary>
    [Export] public NodePath SchedulerDashboardPath { get; set; } = null!;
    /// <summary>Audio 仪表盘根节点路径。</summary>
    [Export] public NodePath AudioDashboardPath { get; set; } = null!;
    /// <summary>Services 检查器根节点路径。</summary>
    [Export] public NodePath ServicesDashboardPath { get; set; } = null!;
    /// <summary>Events 检查器根节点路径。</summary>
    [Export] public NodePath EventsDashboardPath { get; set; } = null!;
    /// <summary>控制台工具栏节点路径。</summary>
    [Export] public NodePath ConsoleToolbarPath { get; set; } = null!;
    /// <summary>控制台等级筛选栏节点路径。</summary>
    [Export] public NodePath ConsoleFiltersPath { get; set; } = null!;
    /// <summary>控制台分页栏节点路径。</summary>
    [Export] public NodePath ConsolePaginationPath { get; set; } = null!;
    /// <summary>控制台搜索输入框节点路径。</summary>
    [Export] public NodePath ConsoleSearchPath { get; set; } = null!;
    /// <summary>控制台自动刷新暂停按钮节点路径。</summary>
    [Export] public NodePath PauseConsoleButtonPath { get; set; } = null!;
    /// <summary>复制当前页面文本按钮节点路径。</summary>
    [Export] public NodePath CopyConsoleButtonPath { get; set; } = null!;
    /// <summary>控制台查看更早日志按钮节点路径。</summary>
    [Export] public NodePath OlderConsolePageButtonPath { get; set; } = null!;
    /// <summary>控制台查看更新日志按钮节点路径。</summary>
    [Export] public NodePath NewerConsolePageButtonPath { get; set; } = null!;
    /// <summary>控制台返回最新日志按钮节点路径。</summary>
    [Export] public NodePath LatestConsolePageButtonPath { get; set; } = null!;
    /// <summary>控制台分页状态标签节点路径。</summary>
    [Export] public NodePath ConsolePageStatusPath { get; set; } = null!;
    /// <summary>窗口缩放操作行节点路径。</summary>
    [Export] public NodePath ResizeRowPath { get; set; } = null!;
    /// <summary>窗口右下角缩放手柄节点路径。</summary>
    [Export] public NodePath ResizeGripPath { get; set; } = null!;

    /// <inheritdoc />
    public override void _EnterTree()
    {
        ErrorHub.OnError += OnErrorReported;
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        _panel = GetNodeOrNull<PanelContainer>(PanelPath);
        _header = GetNodeOrNull<Control>(HeaderPath);
        _toggleButton = GetNodeOrNull<Button>(ToggleButtonPath);
        _resetLayoutButton = GetNodeOrNull<Button>(ResetLayoutButtonPath);
        _body = GetNodeOrNull<Control>(BodyPath);
        _titleLabel = GetNodeOrNull<Label>(TitleLabelPath);
        _navigationTree = GetNodeOrNull<Tree>(NavigationTreePath);
        _overviewDashboard = GetNodeOrNull<Control>(OverviewDashboardPath);
        _inputDashboard = GetNodeOrNull<Control>(InputDashboardPath);
        _servicesDashboard = GetNodeOrNull<Control>(ServicesDashboardPath);
        _eventsDashboard = GetNodeOrNull<Control>(EventsDashboardPath);
        _schedulerDashboard = GetNodeOrNull<Control>(SchedulerDashboardPath);
        _audioDashboard = GetNodeOrNull<Control>(AudioDashboardPath);
        _debuggerLabel = GetNodeOrNull<RichTextLabel>(DebuggerLabelPath);
        _consoleScrollBar = _debuggerLabel?.GetVScrollBar();
        _consoleToolbar = GetNodeOrNull<Control>(ConsoleToolbarPath);
        _consoleFilters = GetNodeOrNull<Control>(ConsoleFiltersPath);
        _consolePagination = GetNodeOrNull<Control>(ConsolePaginationPath);
        _consoleSearch = GetNodeOrNull<LineEdit>(ConsoleSearchPath);
        _pauseConsoleButton = GetNodeOrNull<Button>(PauseConsoleButtonPath);
        _copyConsoleButton = GetNodeOrNull<Button>(CopyConsoleButtonPath);
        _olderConsolePageButton = GetNodeOrNull<Button>(OlderConsolePageButtonPath);
        _newerConsolePageButton = GetNodeOrNull<Button>(NewerConsolePageButtonPath);
        _latestConsolePageButton = GetNodeOrNull<Button>(LatestConsolePageButtonPath);
        _consolePageStatus = GetNodeOrNull<Label>(ConsolePageStatusPath);
        _resizeRow = GetNodeOrNull<Control>(ResizeRowPath);
        _resizeGrip = GetNodeOrNull<Control>(ResizeGripPath);

        if (!IsInstanceValid(_panel) ||
            !IsInstanceValid(_header) ||
            !IsInstanceValid(_toggleButton) ||
            !IsInstanceValid(_resetLayoutButton) ||
            !IsInstanceValid(_body) ||
            !IsInstanceValid(_titleLabel) ||
            !IsInstanceValid(_navigationTree) ||
            !IsInstanceValid(_overviewDashboard) ||
            !IsInstanceValid(_inputDashboard) ||
            !IsInstanceValid(_servicesDashboard) ||
            !IsInstanceValid(_eventsDashboard) ||
            !IsInstanceValid(_schedulerDashboard) ||
            !IsInstanceValid(_audioDashboard) ||
            !IsInstanceValid(_debuggerLabel) ||
            !IsInstanceValid(_consoleScrollBar) ||
            !IsInstanceValid(_consoleToolbar) ||
            !IsInstanceValid(_consoleFilters) ||
            !IsInstanceValid(_consolePagination) ||
            !IsInstanceValid(_consoleSearch) ||
            !IsInstanceValid(_pauseConsoleButton) ||
            !IsInstanceValid(_copyConsoleButton) ||
            !IsInstanceValid(_olderConsolePageButton) ||
            !IsInstanceValid(_newerConsolePageButton) ||
            !IsInstanceValid(_latestConsolePageButton) ||
            !IsInstanceValid(_consolePageStatus) ||
            !IsInstanceValid(_resizeRow) ||
            !IsInstanceValid(_resizeGrip))
        {
            throw new InvalidOperationException("DebuggerOverlay 场景缺少必要的导出节点引用。");
        }

        CacheOverviewNodes();
        CacheInputNodes();
        CacheSchedulerNodes();
        CacheAudioNodes();
        CacheServicesNodes();
        CacheEventsNodes();
        CacheConsoleFilterNodes();
        RegisterPages();
        ConfigureNavigationTree();
        _debuggerLabel.FocusMode = Control.FocusModeEnum.None;
        _toggleButton.Pressed += OnTogglePressed;
        _resetLayoutButton.Pressed += OnResetLayoutPressed;
        _navigationTree.ItemSelected += OnNavigationItemSelected;
        _inputActionsSearch!.TextChanged += OnInputActionsSearchChanged;
        _inputActionsSearch.TextSubmitted += OnInputActionsSearchSubmitted;
        _servicesSearch!.TextChanged += OnServicesSearchChanged;
        _servicesSearch.TextSubmitted += OnServicesSearchSubmitted;
        _servicesTree!.ItemSelected += OnServiceItemSelected;
        _eventsSearch!.TextChanged += OnEventsSearchChanged;
        _eventsSearch.TextSubmitted += OnEventsSearchSubmitted;
        _eventsTree!.ItemSelected += OnEventItemSelected;
        _consoleSearch.TextChanged += OnConsoleSearchChanged;
        _consoleSearch.TextSubmitted += OnConsoleSearchSubmitted;
        _allConsoleFilterButton!.Pressed += OnAllConsoleFilterPressed;
        _debugConsoleFilterButton!.Pressed += OnDebugConsoleFilterPressed;
        _infoConsoleFilterButton!.Pressed += OnInfoConsoleFilterPressed;
        _warningConsoleFilterButton!.Pressed += OnWarningConsoleFilterPressed;
        _errorConsoleFilterButton!.Pressed += OnErrorConsoleFilterPressed;
        _pauseConsoleButton.Pressed += OnPauseConsolePressed;
        _copyConsoleButton.Pressed += OnCopyConsolePressed;
        _olderConsolePageButton.Pressed += OnOlderConsolePagePressed;
        _newerConsolePageButton.Pressed += OnNewerConsolePagePressed;
        _latestConsolePageButton.Pressed += OnLatestConsolePagePressed;
        _consoleScrollBar.ValueChanged += OnConsoleScrollValueChanged;
        _header.GuiInput += OnHeaderGuiInput;
        _resizeGrip.GuiInput += OnResizeGripGuiInput;
        ResetLayout();
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
        if (IsInstanceValid(_resetLayoutButton))
            _resetLayoutButton.Pressed -= OnResetLayoutPressed;
        if (IsInstanceValid(_navigationTree))
            _navigationTree.ItemSelected -= OnNavigationItemSelected;
        if (IsInstanceValid(_inputActionsSearch))
        {
            _inputActionsSearch.TextChanged -= OnInputActionsSearchChanged;
            _inputActionsSearch.TextSubmitted -= OnInputActionsSearchSubmitted;
        }
        if (IsInstanceValid(_servicesSearch))
        {
            _servicesSearch.TextChanged -= OnServicesSearchChanged;
            _servicesSearch.TextSubmitted -= OnServicesSearchSubmitted;
        }
        if (IsInstanceValid(_servicesTree))
            _servicesTree.ItemSelected -= OnServiceItemSelected;
        if (IsInstanceValid(_eventsSearch))
        {
            _eventsSearch.TextChanged -= OnEventsSearchChanged;
            _eventsSearch.TextSubmitted -= OnEventsSearchSubmitted;
        }
        if (IsInstanceValid(_eventsTree))
            _eventsTree.ItemSelected -= OnEventItemSelected;
        if (IsInstanceValid(_consoleSearch))
        {
            _consoleSearch.TextChanged -= OnConsoleSearchChanged;
            _consoleSearch.TextSubmitted -= OnConsoleSearchSubmitted;
        }
        if (IsInstanceValid(_allConsoleFilterButton))
            _allConsoleFilterButton.Pressed -= OnAllConsoleFilterPressed;
        if (IsInstanceValid(_debugConsoleFilterButton))
            _debugConsoleFilterButton.Pressed -= OnDebugConsoleFilterPressed;
        if (IsInstanceValid(_infoConsoleFilterButton))
            _infoConsoleFilterButton.Pressed -= OnInfoConsoleFilterPressed;
        if (IsInstanceValid(_warningConsoleFilterButton))
            _warningConsoleFilterButton.Pressed -= OnWarningConsoleFilterPressed;
        if (IsInstanceValid(_errorConsoleFilterButton))
            _errorConsoleFilterButton.Pressed -= OnErrorConsoleFilterPressed;
        if (IsInstanceValid(_pauseConsoleButton))
            _pauseConsoleButton.Pressed -= OnPauseConsolePressed;
        if (IsInstanceValid(_copyConsoleButton))
            _copyConsoleButton.Pressed -= OnCopyConsolePressed;
        if (IsInstanceValid(_olderConsolePageButton))
            _olderConsolePageButton.Pressed -= OnOlderConsolePagePressed;
        if (IsInstanceValid(_newerConsolePageButton))
            _newerConsolePageButton.Pressed -= OnNewerConsolePagePressed;
        if (IsInstanceValid(_latestConsolePageButton))
            _latestConsolePageButton.Pressed -= OnLatestConsolePagePressed;
        if (IsInstanceValid(_consoleScrollBar))
            _consoleScrollBar.ValueChanged -= OnConsoleScrollValueChanged;
        if (IsInstanceValid(_header))
            _header.GuiInput -= OnHeaderGuiInput;
        if (IsInstanceValid(_resizeGrip))
            _resizeGrip.GuiInput -= OnResizeGripGuiInput;

        _panel = null;
        _header = null;
        _toggleButton = null;
        _resetLayoutButton = null;
        _body = null;
        _titleLabel = null;
        _navigationTree = null;
        _overviewDashboard = null;
        _overviewFpsValue = null;
        _overviewWarningValue = null;
        _overviewErrorValue = null;
        _overviewServicesValue = null;
        _overviewEventsValue = null;
        _overviewEventsDetail = null;
        _overviewResourcesValue = null;
        _overviewResourcesDetail = null;
        _overviewSceneValue = null;
        _overviewSceneDetail = null;
        _overviewAudioValue = null;
        _overviewAudioDetail = null;
        _overviewInputValue = null;
        _overviewInputDetail = null;
        _overviewSchedulerValue = null;
        _overviewSchedulerDetail = null;
        _inputDashboard = null;
        _inputBackendValue = null;
        _inputBackendDetail = null;
        _inputDeviceValue = null;
        _inputFrameValue = null;
        _inputFrameDetail = null;
        _inputActionsValue = null;
        _inputCapabilities = null;
        _inputContextsTree = null;
        _inputActionsSearch = null;
        _inputActionsMatchStatus = null;
        _inputActionsTree = null;
        _schedulerDashboard = null;
        _schedulerActiveValue = null;
        _schedulerPausedValue = null;
        _schedulerRepeatingValue = null;
        _schedulerNextValue = null;
        _schedulerProcessGameValue = null;
        _schedulerProcessUnscaledValue = null;
        _schedulerProcessRealValue = null;
        _schedulerProcessDispatchValue = null;
        _schedulerPhysicsGameValue = null;
        _schedulerPhysicsUnscaledValue = null;
        _schedulerPhysicsRealValue = null;
        _schedulerPhysicsDispatchValue = null;
        _schedulerCanceledValue = null;
        _schedulerOwnerCanceledValue = null;
        _schedulerFailedValue = null;
        _audioDashboard = null;
        _audioBgmStateValue = null;
        _audioBgmStateDetail = null;
        _audioBgmResourceValue = null;
        _audioSfxValue = null;
        _audioSfxDetail = null;
        _audioMasterVolumeValue = null;
        _audioBgmVolumeValue = null;
        _audioSfxVolumeValue = null;
        _servicesDashboard = null;
        _servicesSearch = null;
        _servicesContractsValue = null;
        _servicesImplementationsValue = null;
        _servicesMatchStatus = null;
        _servicesTree = null;
        _servicesSelectionDetail = null;
        _eventsDashboard = null;
        _eventsSearch = null;
        _eventsTypesValue = null;
        _eventsListenersValue = null;
        _eventsMatchStatus = null;
        _eventsTree = null;
        _eventsSelectionDetail = null;
        _debuggerLabel = null;
        _consoleScrollBar = null;
        _consoleToolbar = null;
        _consoleFilters = null;
        _consolePagination = null;
        _consoleSearch = null;
        _allConsoleFilterButton = null;
        _debugConsoleFilterButton = null;
        _infoConsoleFilterButton = null;
        _warningConsoleFilterButton = null;
        _errorConsoleFilterButton = null;
        _pauseConsoleButton = null;
        _copyConsoleButton = null;
        _olderConsolePageButton = null;
        _newerConsolePageButton = null;
        _latestConsolePageButton = null;
        _consolePageStatus = null;
        _resizeRow = null;
        _resizeGrip = null;
        _selectedPage = null;
        _pageGroups.Clear();
        _pagesByTreeItem.Clear();
    }

    private void OnTogglePressed()
    {
        _expanded = !_expanded;
        if (!_expanded && IsInstanceValid(_consoleSearch))
            _consoleSearch.ReleaseFocus();
        ApplyExpandedState();
        if (_expanded)
            RefreshDebugger(force: true);
    }

    private void ApplyExpandedState()
    {
        if (!IsInstanceValid(_toggleButton) ||
            !IsInstanceValid(_titleLabel) ||
            !IsInstanceValid(_resetLayoutButton) ||
            !IsInstanceValid(_body) ||
            !IsInstanceValid(_consoleToolbar) ||
            !IsInstanceValid(_consoleFilters) ||
            !IsInstanceValid(_consolePagination) ||
            !IsInstanceValid(_resizeRow) ||
            !IsInstanceValid(_resizeGrip))
            return;

        _titleLabel.Visible = _expanded;
        _resetLayoutButton.Visible = _expanded;
        _body.Visible = _expanded;
        _consoleToolbar.Visible = _expanded && _selectedPage?.IsConsole == true;
        _consoleFilters.Visible = _expanded && _selectedPage?.IsConsole == true;
        _consolePagination.Visible = _expanded && _selectedPage?.IsConsole == true;
        _resizeRow.Visible = _expanded;
        _resizeGrip.Visible = _expanded;
        _toggleButton.SizeFlagsHorizontal = _expanded
            ? Control.SizeFlags.ShrinkBegin
            : Control.SizeFlags.ExpandFill;
        ApplyPanelSize();
    }

    private void ApplyPanelSize()
    {
        if (!IsInstanceValid(_panel))
            return;

        if (!_expanded)
        {
            _panel.Size = new Vector2(176f, 48f);
            ClampPanelPosition();
            return;
        }

        _expandedSize = ClampExpandedSize(_expandedSize);
        _panel.Size = _expandedSize;
        ClampPanelPosition();
    }

    private Vector2 ClampExpandedSize(Vector2 requestedSize)
    {
        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        float availableWidth = Mathf.Max(240f, viewportSize.X - _panel!.Position.X - ScreenMargin);
        float availableHeight = Mathf.Max(180f, viewportSize.Y - _panel.Position.Y - ScreenMargin);
        return new Vector2(
            Mathf.Clamp(requestedSize.X, Mathf.Min(MinimumExpandedWidth, availableWidth), availableWidth),
            Mathf.Clamp(requestedSize.Y, Mathf.Min(MinimumExpandedHeight, availableHeight), availableHeight));
    }

    private void ClampPanelPosition()
    {
        if (!IsInstanceValid(_panel))
            return;

        Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
        _panel.Position = new Vector2(
            Mathf.Clamp(_panel.Position.X, 0f, Mathf.Max(0f, viewportSize.X - VisibleHeaderWidth)),
            Mathf.Clamp(_panel.Position.Y, 0f, Mathf.Max(0f, viewportSize.Y - VisibleHeaderHeight)));
    }

    private void OnResetLayoutPressed()
    {
        ResetLayout();
        ApplyPanelSize();
        if (_selectedPage?.IsConsole == true &&
            _consolePageOffset == 0 &&
            _consoleFollowLatest)
            ScheduleConsoleScrollToBottom();
    }

    private void ResetLayout()
    {
        if (!IsInstanceValid(_panel))
            return;

        _panel.Position = new Vector2(ScreenMargin, ScreenMargin);
        _expandedSize = new Vector2(DefaultExpandedWidth, DefaultExpandedHeight);
    }

    private void OnHeaderGuiInput(InputEvent inputEvent)
    {
        if (!_expanded || !IsInstanceValid(_panel) || !IsInstanceValid(_header))
            return;

        if (inputEvent is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left)
        {
            _dragging = mouseButton.Pressed;
            if (_dragging)
            {
                _pointerStart = GetViewport().GetMousePosition();
                _panelPositionStart = _panel.Position;
            }
            _header.AcceptEvent();
            return;
        }

        if (_dragging && inputEvent is InputEventMouseMotion)
        {
            _panel.Position =
                _panelPositionStart + GetViewport().GetMousePosition() - _pointerStart;
            ClampPanelPosition();
            _header.AcceptEvent();
        }
    }

    private void OnResizeGripGuiInput(InputEvent inputEvent)
    {
        if (!_expanded || !IsInstanceValid(_panel) || !IsInstanceValid(_resizeGrip))
            return;

        if (inputEvent is InputEventMouseButton mouseButton &&
            mouseButton.ButtonIndex == MouseButton.Left)
        {
            bool finishedResizing = _resizing && !mouseButton.Pressed;
            _resizing = mouseButton.Pressed;
            if (_resizing)
            {
                _pointerStart = GetViewport().GetMousePosition();
                _panelSizeStart = _panel.Size;
            }
            else if (finishedResizing &&
                _selectedPage?.IsConsole == true &&
                _consolePageOffset == 0 &&
                _consoleFollowLatest)
            {
                ScheduleConsoleScrollToBottom();
            }
            _resizeGrip.AcceptEvent();
            return;
        }

        if (_resizing && inputEvent is InputEventMouseMotion)
        {
            _expandedSize = ClampExpandedSize(
                _panelSizeStart + GetViewport().GetMousePosition() - _pointerStart);
            _panel.Size = _expandedSize;
            _resizeGrip.AcceptEvent();
        }
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

    private void RefreshDebugger(bool force = false)
    {
        if (!IsInstanceValid(_debuggerLabel))
            return;

        bool isConsole = _selectedPage?.IsConsole == true;
        if (!force && isConsole)
        {
            if (_consoleRefreshPaused ||
                (_lastConsoleLogVersion == LogHub.DebugHistoryVersion &&
                 _lastConsoleErrorVersion == _consoleErrorVersion))
                return;
        }

        _textBuilder.Clear();
        _selectedPage?.Render();
        string text = _textBuilder.ToString().ReplaceLineEndings("\n");
        if (!string.Equals(_debuggerLabel.Text, text, StringComparison.Ordinal))
            _debuggerLabel.Text = text;

        if (isConsole)
        {
            _lastConsoleLogVersion = LogHub.DebugHistoryVersion;
            _lastConsoleErrorVersion = _consoleErrorVersion;
            if (_consolePageOffset == 0 && _consoleFollowLatest)
                ScrollConsoleToBottom();
        }
    }

    private void ScrollConsoleToBottom()
    {
        if (!IsInstanceValid(_debuggerLabel))
            return;

        ConsoleScrollToBottomCount++;
        ScheduleConsoleScrollToBottom();
    }

    private void ScheduleConsoleScrollToBottom()
    {
        if (_consoleScrollDeferred)
            return;

        _consoleScrollDeferred = true;
        Callable.From(ApplyDeferredConsoleScrollToBottom).CallDeferred();
    }

    private void ApplyDeferredConsoleScrollToBottom()
    {
        try
        {
            if (_consolePageOffset == 0 && _consoleFollowLatest)
                ApplyConsoleScrollToBottom();
        }
        finally
        {
            _consoleScrollDeferred = false;
        }
    }

    private void ApplyConsoleScrollToBottom()
    {
        if (!IsInstanceValid(_debuggerLabel) || !IsInstanceValid(_consoleScrollBar))
            return;

        _applyingConsoleScroll = true;
        try
        {
            _consoleScrollBar.Value = _consoleScrollBar.MaxValue;
        }
        finally
        {
            _applyingConsoleScroll = false;
        }
        UpdateLatestConsoleButtonState();
    }

    private void OnConsoleScrollValueChanged(double value)
    {
        if (_applyingConsoleScroll || _consolePageOffset != 0)
            return;

        ScheduleConsoleScrollEvaluation();
    }

    private void ScheduleConsoleScrollEvaluation()
    {
        if (_consoleScrollEvaluationDeferred)
            return;

        _consoleScrollEvaluationDeferred = true;
        Callable.From(ApplyDeferredConsoleScrollEvaluation).CallDeferred();
    }

    private void ApplyDeferredConsoleScrollEvaluation()
    {
        _consoleScrollEvaluationDeferred = false;
        if (_selectedPage?.IsConsole != true ||
            _consolePageOffset != 0 ||
            !IsInstanceValid(_consoleScrollBar))
            return;

        ConsoleScrollEvaluationCount++;
        _consoleFollowLatest = IsConsoleAtBottom();
        UpdateLatestConsoleButtonState();
    }

    private bool IsConsoleAtBottom()
    {
        if (!IsInstanceValid(_consoleScrollBar))
            return true;

        double bottom = Math.Max(
            _consoleScrollBar.MinValue,
            _consoleScrollBar.MaxValue - _consoleScrollBar.Page);
        return _consoleScrollBar.Value >= bottom - ConsoleBottomThreshold;
    }

    private void CacheOverviewNodes()
    {
        _overviewFpsValue = GetOverviewLabel("Content/StatusGrid/FpsCard/Content/Value");
        _overviewWarningValue = GetOverviewLabel("Content/StatusGrid/WarningCard/Content/Value");
        _overviewErrorValue = GetOverviewLabel("Content/StatusGrid/ErrorCard/Content/Value");
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

    private void CacheServicesNodes()
    {
        _servicesSearch = GetServicesNode<LineEdit>("Search");
        _servicesContractsValue = GetServicesNode<Label>("Summary/ContractsCard/Content/Value");
        _servicesImplementationsValue =
            GetServicesNode<Label>("Summary/ImplementationsCard/Content/Value");
        _servicesMatchStatus = GetServicesNode<Label>("MatchStatus");
        _servicesTree = GetServicesNode<Tree>("ServiceList");
        _servicesSelectionDetail = GetServicesNode<Label>("SelectionDetail");
        _servicesTree.SetColumnTitle(0, "服务接口");
        _servicesTree.SetColumnTitle(1, "实现");
        _servicesTree.SetColumnExpand(0, true);
        _servicesTree.SetColumnExpand(1, true);
    }

    private T GetServicesNode<T>(string path) where T : Node
    {
        T? node = _servicesDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerServices 场景缺少节点：{path}");
    }

    private void CacheEventsNodes()
    {
        _eventsSearch = GetEventsNode<LineEdit>("Search");
        _eventsTypesValue = GetEventsNode<Label>("Summary/TypesCard/Content/Value");
        _eventsListenersValue = GetEventsNode<Label>("Summary/ListenersCard/Content/Value");
        _eventsMatchStatus = GetEventsNode<Label>("MatchStatus");
        _eventsTree = GetEventsNode<Tree>("EventList");
        _eventsSelectionDetail = GetEventsNode<Label>("SelectionDetail");
        _eventsTree.SetColumnTitle(0, "事件");
        _eventsTree.SetColumnTitle(1, "监听器");
        _eventsTree.SetColumnExpand(0, true);
        _eventsTree.SetColumnExpand(1, false);
        _eventsTree.SetColumnCustomMinimumWidth(1, 72);
    }

    private T GetEventsNode<T>(string path) where T : Node
    {
        T? node = _eventsDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerEvents 场景缺少节点：{path}");
    }

    private void CacheConsoleFilterNodes()
    {
        _allConsoleFilterButton = GetConsoleFilterButton("All");
        _debugConsoleFilterButton = GetConsoleFilterButton("Debug");
        _infoConsoleFilterButton = GetConsoleFilterButton("Info");
        _warningConsoleFilterButton = GetConsoleFilterButton("Warning");
        _errorConsoleFilterButton = GetConsoleFilterButton("Error");
        ApplyConsoleFilterButtonStates();
    }

    private Button GetConsoleFilterButton(string path)
    {
        Button? button = _consoleFilters!.GetNodeOrNull<Button>(path);
        return IsInstanceValid(button)
            ? button
            : throw new InvalidOperationException($"DebuggerConsole 场景缺少节点：{path}");
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
        RegisterPage("Overview", "概览", "概览", RefreshOverviewDashboard);
        RegisterPage("Runtime/Input", "运行时", "Input", RefreshInputDashboard);
        RegisterPage("Runtime/Scheduler", "运行时", "Scheduler", RefreshSchedulerDashboard);
        RegisterPage("Runtime/Audio", "运行时", "Audio", RefreshAudioDashboard);
        RegisterPage("Framework/Services", "框架", "Services", RefreshServicesDashboard);
        RegisterPage("Framework/Events", "框架", "Events", RefreshEventsDashboard);
        RegisterPage("Console", "控制台", "控制台", AppendConsole);
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

        group.Pages.Add(new DebuggerPage(path, title, render));
    }

    private void ConfigureNavigationTree()
    {
        if (!IsInstanceValid(_navigationTree))
            return;

        _navigationTree.Clear();
        _pagesByTreeItem.Clear();
        TreeItem root = _navigationTree.CreateItem();
        TreeItem? firstPageItem = null;

        for (int groupIndex = 0; groupIndex < _pageGroups.Count; groupIndex++)
        {
            DebuggerPageGroup group = _pageGroups[groupIndex];
            if (group.Pages.Count == 1)
            {
                TreeItem pageItem = _navigationTree.CreateItem(root);
                pageItem.SetText(0, group.Title);
                _pagesByTreeItem.Add(pageItem, group.Pages[0]);
                firstPageItem ??= pageItem;
                continue;
            }

            TreeItem groupItem = _navigationTree.CreateItem(root);
            groupItem.SetText(0, group.Title);
            groupItem.SetSelectable(0, false);
            groupItem.Collapsed = false;
            for (int pageIndex = 0; pageIndex < group.Pages.Count; pageIndex++)
            {
                DebuggerPage page = group.Pages[pageIndex];
                TreeItem pageItem = _navigationTree.CreateItem(groupItem);
                pageItem.SetText(0, page.Title);
                _pagesByTreeItem.Add(pageItem, page);
                firstPageItem ??= pageItem;
            }
        }

        if (firstPageItem is null)
            return;

        firstPageItem.Select(0);
        SelectPage(_pagesByTreeItem[firstPageItem], forceRefresh: false);
    }

    private void OnNavigationItemSelected()
    {
        if (!IsInstanceValid(_navigationTree))
            return;

        TreeItem? selectedItem = _navigationTree.GetSelected();
        if (selectedItem is null || !_pagesByTreeItem.TryGetValue(selectedItem, out DebuggerPage? page))
            return;

        SelectPage(page, forceRefresh: true);
    }

    private void SelectPage(DebuggerPage page, bool forceRefresh)
    {
        _selectedPage = page;
        if (IsInstanceValid(_titleLabel))
            _titleLabel.Text = page.Title;
        if (IsInstanceValid(_consoleToolbar))
            _consoleToolbar.Visible = _expanded && page.IsConsole;
        if (IsInstanceValid(_consoleFilters))
            _consoleFilters.Visible = _expanded && page.IsConsole;
        if (IsInstanceValid(_consolePagination))
            _consolePagination.Visible = _expanded && page.IsConsole;
        if (IsInstanceValid(_overviewDashboard))
            _overviewDashboard.Visible = page.IsOverview;
        if (IsInstanceValid(_inputDashboard))
            _inputDashboard.Visible = page.IsInput;
        if (IsInstanceValid(_schedulerDashboard))
            _schedulerDashboard.Visible = page.IsScheduler;
        if (IsInstanceValid(_audioDashboard))
            _audioDashboard.Visible = page.IsAudio;
        if (IsInstanceValid(_servicesDashboard))
            _servicesDashboard.Visible = page.IsServices;
        if (IsInstanceValid(_eventsDashboard))
            _eventsDashboard.Visible = page.IsEvents;
        if (IsInstanceValid(_debuggerLabel))
        {
            _debuggerLabel.Visible =
                !page.IsOverview &&
                !page.IsInput &&
                !page.IsScheduler &&
                !page.IsAudio &&
                !page.IsServices &&
                !page.IsEvents;
        }
        if (!page.IsInput && IsInstanceValid(_inputActionsSearch))
            _inputActionsSearch.ReleaseFocus();
        if (!page.IsServices && IsInstanceValid(_servicesSearch))
            _servicesSearch.ReleaseFocus();
        if (!page.IsEvents && IsInstanceValid(_eventsSearch))
            _eventsSearch.ReleaseFocus();
        if (!page.IsConsole && IsInstanceValid(_consoleSearch))
            _consoleSearch.ReleaseFocus();
        RefreshDebugger(forceRefresh);
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

    private void OnServicesSearchChanged(string text)
    {
        _servicesSearchQuery = text.Trim();
        _servicesSnapshotSignature = int.MinValue;
        if (_selectedPage?.IsServices == true)
            RefreshDebugger(force: true);
    }

    private void OnServicesSearchSubmitted(string text)
    {
        _servicesSearchQuery = text.Trim();
        if (IsInstanceValid(_servicesSearch))
            _servicesSearch.ReleaseFocus();
    }

    private void OnServiceItemSelected()
    {
        if (!IsInstanceValid(_servicesTree) || !IsInstanceValid(_servicesSelectionDetail))
            return;

        TreeItem? item = _servicesTree.GetSelected();
        _servicesSelectionDetail.Text = item is null
            ? "选择服务查看完整注册关系"
            : item.GetMetadata(0).AsString();
    }

    private void OnEventsSearchChanged(string text)
    {
        _eventsSearchQuery = text.Trim();
        _eventsSnapshotSignature = int.MinValue;
        if (_selectedPage?.IsEvents == true)
            RefreshDebugger(force: true);
    }

    private void OnEventsSearchSubmitted(string text)
    {
        _eventsSearchQuery = text.Trim();
        if (IsInstanceValid(_eventsSearch))
            _eventsSearch.ReleaseFocus();
    }

    private void OnEventItemSelected()
    {
        if (!IsInstanceValid(_eventsTree) || !IsInstanceValid(_eventsSelectionDetail))
            return;

        TreeItem? item = _eventsTree.GetSelected();
        _eventsSelectionDetail.Text = item is null
            ? "选择事件查看完整类型名"
            : item.GetMetadata(0).AsString();
    }

    private void OnConsoleSearchChanged(string text)
    {
        _consoleSearchQuery = text.Trim();
        _consolePageOffset = 0;
        _consoleFollowLatest = true;
        if (_selectedPage?.IsConsole == true)
            RefreshDebugger(force: true);
    }

    private void OnConsoleSearchSubmitted(string text)
    {
        _consoleSearchQuery = text.Trim();
        if (IsInstanceValid(_consoleSearch))
            _consoleSearch.ReleaseFocus();
    }

    private void OnAllConsoleFilterPressed()
    {
        _consoleLevelFilter = ConsoleLevelFilter.All;
        _consolePageOffset = 0;
        _consoleFollowLatest = true;
        ApplyConsoleFilterButtonStates();
        RefreshDebugger(force: true);
    }

    private void OnDebugConsoleFilterPressed() =>
        ToggleConsoleFilter(ConsoleLevelFilter.Debug);

    private void OnInfoConsoleFilterPressed() =>
        ToggleConsoleFilter(ConsoleLevelFilter.Info);

    private void OnWarningConsoleFilterPressed() =>
        ToggleConsoleFilter(ConsoleLevelFilter.Warning);

    private void OnErrorConsoleFilterPressed() =>
        ToggleConsoleFilter(ConsoleLevelFilter.Error);

    private void ToggleConsoleFilter(ConsoleLevelFilter filter)
    {
        if (_consoleLevelFilter == ConsoleLevelFilter.All)
        {
            _consoleLevelFilter = filter;
        }
        else if ((_consoleLevelFilter & filter) != 0)
        {
            ConsoleLevelFilter remaining = _consoleLevelFilter & ~filter;
            _consoleLevelFilter = remaining == ConsoleLevelFilter.None
                ? ConsoleLevelFilter.All
                : remaining;
        }
        else
        {
            _consoleLevelFilter |= filter;
        }

        _consolePageOffset = 0;
        _consoleFollowLatest = true;
        ApplyConsoleFilterButtonStates();
        RefreshDebugger(force: true);
    }

    private void OnOlderConsolePagePressed()
    {
        _consolePageOffset++;
        _consoleFollowLatest = false;
        RefreshDebugger(force: true);
    }

    private void OnNewerConsolePagePressed()
    {
        if (_consolePageOffset == 0)
            return;

        _consolePageOffset--;
        if (_consolePageOffset == 0)
            _consoleFollowLatest = true;
        RefreshDebugger(force: true);
    }

    private void OnLatestConsolePagePressed()
    {
        _consolePageOffset = 0;
        _consoleFollowLatest = true;
        RefreshDebugger(force: true);
    }

    private void ApplyConsoleFilterButtonStates()
    {
        if (!IsInstanceValid(_allConsoleFilterButton) ||
            !IsInstanceValid(_debugConsoleFilterButton) ||
            !IsInstanceValid(_infoConsoleFilterButton) ||
            !IsInstanceValid(_warningConsoleFilterButton) ||
            !IsInstanceValid(_errorConsoleFilterButton))
            return;

        bool showAll = _consoleLevelFilter == ConsoleLevelFilter.All;
        _allConsoleFilterButton.ButtonPressed = showAll;
        _debugConsoleFilterButton.ButtonPressed =
            !showAll && (_consoleLevelFilter & ConsoleLevelFilter.Debug) != 0;
        _infoConsoleFilterButton.ButtonPressed =
            !showAll && (_consoleLevelFilter & ConsoleLevelFilter.Info) != 0;
        _warningConsoleFilterButton.ButtonPressed =
            !showAll && (_consoleLevelFilter & ConsoleLevelFilter.Warning) != 0;
        _errorConsoleFilterButton.ButtonPressed =
            !showAll && (_consoleLevelFilter & ConsoleLevelFilter.Error) != 0;
    }

    private void OnPauseConsolePressed()
    {
        _consoleRefreshPaused = !_consoleRefreshPaused;
        if (IsInstanceValid(_pauseConsoleButton))
            _pauseConsoleButton.Text = _consoleRefreshPaused ? "继续" : "暂停";
        if (!_consoleRefreshPaused)
            RefreshDebugger(force: true);
    }

    private void OnCopyConsolePressed()
    {
        if (!IsInstanceValid(_debuggerLabel))
            return;

        DisplayServer.ClipboardSet(_debuggerLabel.Text);
    }

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
            _overviewAudioDetail.Text = $"SFX {audio.ActiveSfxCount}/{audio.MaxSfxVoices}";
        }
        else
        {
            _overviewAudioValue.Text = "不可用";
            _overviewAudioDetail.Text = "AudioService";
        }

        if (Services.TryGet<IInputService>(out IInputService? input) && input is not null)
        {
            _overviewInputValue.Text = input.ActiveDevice.ToString();
            _overviewInputDetail.Text = "当前活动设备";
        }
        else
        {
            _overviewInputValue.Text = "不可用";
            _overviewInputDetail.Text = "InputService";
        }

        if (Services.TryGet<ISchedulerService>(out ISchedulerService? scheduler) &&
            scheduler is SchedulerService schedulerService)
        {
            SchedulerDebugSnapshot snapshot = schedulerService.GetDebugSnapshot();
            _overviewSchedulerValue.Text = snapshot.ActiveCount.ToString(CultureInfo.InvariantCulture);
            _overviewSchedulerDetail.Text = $"{snapshot.PausedCount} 个暂停";
        }
        else
        {
            _overviewSchedulerValue.Text = "不可用";
            _overviewSchedulerDetail.Text = "SchedulerService";
        }
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

        if (!Services.TryGet<IInputService>(out IInputService? input) || input is not InputService inputService)
        {
            _inputBackendValue.Text = "不可用";
            _inputBackendDetail.Text = "InputService";
            _inputDeviceValue.Text = "Unknown";
            _inputFrameValue.Text = "—";
            _inputFrameDetail.Text = "无采样";
            _inputActionsValue.Text = "0";
            _inputCapabilities.Text = "能力：无";
            _inputActionsMatchStatus.Text = "InputService 不可用";
            _inputContextsTree.Clear();
            _inputActionsTree.Clear();
            _inputContextsSignature = int.MinValue;
            _inputActionsSignature = int.MinValue;
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
            scheduler is not SchedulerService schedulerService)
        {
            _schedulerActiveValue.Text = "不可用";
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
            _audioBgmStateValue.Text = "不可用";
            _audioBgmStateDetail.Text = "AudioService 未注册";
            _audioBgmResourceValue.Text = "—";
            _audioBgmResourceValue.TooltipText = string.Empty;
            _audioSfxValue.Text = "—";
            _audioSfxDetail.Text = "AudioService 未注册";
            SetAudioVolumeUnavailable();
            return;
        }

        ResourceKey? currentBgm = audio.CurrentBgm;
        _audioBgmStateValue.Text = audio.IsBgmLoading
            ? "加载中"
            : audio.IsBgmPlaying
                ? "播放中"
                : currentBgm.HasValue ? "已加载" : "已停止";
        _audioBgmStateDetail.Text = currentBgm.HasValue
            ? audio.IsBgmPlaying ? "播放器活跃" : "当前未播放"
            : "没有 BGM";

        string bgmResource = currentBgm?.Value ?? "无";
        _audioBgmResourceValue.Text = bgmResource;
        _audioBgmResourceValue.TooltipText = currentBgm?.Value ?? string.Empty;

        int activeSfx = audio.ActiveSfxCount;
        int maxSfx = audio.MaxSfxVoices;
        _audioSfxValue.Text = $"{activeSfx}/{maxSfx}";
        _audioSfxDetail.Text = maxSfx > 0
            ? $"占用 {Mathf.RoundToInt(activeSfx * 100f / maxSfx).ToString(CultureInfo.InvariantCulture)}%"
            : "容量未配置";

        SetAudioVolume(_audioMasterVolumeValue, audio.GetVolume(AudioGroup.Master));
        SetAudioVolume(_audioBgmVolumeValue, audio.GetVolume(AudioGroup.Bgm));
        SetAudioVolume(_audioSfxVolumeValue, audio.GetVolume(AudioGroup.Sfx));
    }

    private void SetAudioVolumeUnavailable()
    {
        _audioMasterVolumeValue!.Text = "—";
        _audioBgmVolumeValue!.Text = "—";
        _audioSfxVolumeValue!.Text = "—";
    }

    private static void SetAudioVolume(Label label, float volume)
    {
        label.Text =
            $"{Mathf.RoundToInt(volume * 100f).ToString(CultureInfo.InvariantCulture)}%";
        label.TooltipText =
            $"线性音量 {volume.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private void AppendConsole()
    {
        ConsoleRenderCount++;
        LogEntry[] logs = LogHub.GetDebugSnapshot();
        UpdateConsoleFilterLabels(logs);
        AppendLogs(logs);
        AppendWarnings();
    }

    private void UpdateConsoleFilterLabels(LogEntry[] logs)
    {
        if (!IsInstanceValid(_allConsoleFilterButton) ||
            !IsInstanceValid(_debugConsoleFilterButton) ||
            !IsInstanceValid(_infoConsoleFilterButton) ||
            !IsInstanceValid(_warningConsoleFilterButton) ||
            !IsInstanceValid(_errorConsoleFilterButton))
            return;

        long debugCount = 0;
        long infoCount = 0;
        for (int index = 0; index < logs.Length; index++)
        {
            if (logs[index].Level == LogLevel.Debug)
                debugCount += logs[index].RepeatCount;
            else
                infoCount += logs[index].RepeatCount;
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

        _allConsoleFilterButton.Text = $"All ({debugCount + infoCount + warningCount + errorCount})";
        _debugConsoleFilterButton.Text = $"Debug ({debugCount})";
        _infoConsoleFilterButton.Text = $"Info ({infoCount})";
        _warningConsoleFilterButton.Text = $"Warning ({warningCount})";
        _errorConsoleFilterButton.Text = $"Error ({errorCount})";
    }

    private void RefreshServicesDashboard()
    {
        if (!IsInstanceValid(_servicesContractsValue) ||
            !IsInstanceValid(_servicesImplementationsValue) ||
            !IsInstanceValid(_servicesMatchStatus) ||
            !IsInstanceValid(_servicesTree) ||
            !IsInstanceValid(_servicesSelectionDetail))
        {
            return;
        }

        Services.ServiceDebugEntry[] services = Services.GetDebugSnapshot();
        var implementationTypes = new HashSet<Type>();
        var signature = new HashCode();
        signature.Add(_servicesSearchQuery, StringComparer.OrdinalIgnoreCase);
        int matchingCount = 0;
        for (int index = 0; index < services.Length; index++)
        {
            Services.ServiceDebugEntry entry = services[index];
            implementationTypes.Add(entry.ImplementationType);
            signature.Add(entry.ServiceType);
            signature.Add(entry.ImplementationType);
            if (MatchesServiceSearch(entry))
                matchingCount++;
        }

        _servicesContractsValue.Text = services.Length.ToString(CultureInfo.InvariantCulture);
        _servicesImplementationsValue.Text =
            implementationTypes.Count.ToString(CultureInfo.InvariantCulture);
        _servicesMatchStatus.Text = string.IsNullOrEmpty(_servicesSearchQuery)
            ? $"全部 {services.Length} 个注册接口"
            : $"找到 {matchingCount} / {services.Length} 个注册接口";

        int snapshotSignature = signature.ToHashCode();
        if (_servicesSnapshotSignature == snapshotSignature)
            return;

        _servicesSnapshotSignature = snapshotSignature;
        _servicesTree.Clear();
        _servicesSelectionDetail.Text = "选择服务查看完整注册关系";
        TreeItem root = _servicesTree.CreateItem();
        for (int index = 0; index < services.Length; index++)
        {
            Services.ServiceDebugEntry entry = services[index];
            if (!MatchesServiceSearch(entry))
                continue;

            string serviceName = entry.ServiceType.FullName ?? entry.ServiceType.Name;
            string implementationName =
                entry.ImplementationType.FullName ?? entry.ImplementationType.Name;
            TreeItem item = _servicesTree.CreateItem(root);
            item.SetText(0, entry.ServiceType.Name);
            item.SetText(1, entry.ImplementationType.Name);
            item.SetTooltipText(0, serviceName);
            item.SetTooltipText(1, implementationName);
            item.SetMetadata(0, $"{serviceName} → {implementationName}");
        }
    }

    private bool MatchesServiceSearch(Services.ServiceDebugEntry entry)
    {
        if (string.IsNullOrEmpty(_servicesSearchQuery))
            return true;

        return TypeMatchesSearch(entry.ServiceType, _servicesSearchQuery) ||
            TypeMatchesSearch(entry.ImplementationType, _servicesSearchQuery);
    }

    private static bool TypeMatchesSearch(Type type, string query)
    {
        return type.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (type.FullName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void RefreshEventsDashboard()
    {
        if (!IsInstanceValid(_eventsTypesValue) ||
            !IsInstanceValid(_eventsListenersValue) ||
            !IsInstanceValid(_eventsMatchStatus) ||
            !IsInstanceValid(_eventsTree) ||
            !IsInstanceValid(_eventsSelectionDetail))
        {
            return;
        }

        EventChannel.EventDebugEntry[] events = EventChannel.GetDebugSnapshot();
        int listenerCount = 0;
        int matchingCount = 0;
        var signature = new HashCode();
        signature.Add(_eventsSearchQuery, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < events.Length; i++)
        {
            EventChannel.EventDebugEntry entry = events[i];
            listenerCount += entry.ListenerCount;
            signature.Add(entry.EventType);
            signature.Add(entry.ListenerCount);
            if (MatchesEventSearch(entry.EventType))
                matchingCount++;
        }

        _eventsTypesValue.Text = events.Length.ToString(CultureInfo.InvariantCulture);
        _eventsListenersValue.Text = listenerCount.ToString(CultureInfo.InvariantCulture);
        _eventsMatchStatus.Text = string.IsNullOrEmpty(_eventsSearchQuery)
            ? $"全部 {events.Length} 个事件类型"
            : $"找到 {matchingCount} / {events.Length} 个事件类型";

        int snapshotSignature = signature.ToHashCode();
        if (_eventsSnapshotSignature == snapshotSignature)
            return;

        _eventsSnapshotSignature = snapshotSignature;
        _eventsTree.Clear();
        _eventsSelectionDetail.Text = "选择事件查看完整类型名";
        TreeItem root = _eventsTree.CreateItem();
        for (int index = 0; index < events.Length; index++)
        {
            EventChannel.EventDebugEntry entry = events[index];
            if (!MatchesEventSearch(entry.EventType))
                continue;

            TreeItem item = _eventsTree.CreateItem(root);
            item.SetText(0, entry.EventType.Name);
            item.SetText(1, entry.ListenerCount.ToString(CultureInfo.InvariantCulture));
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTooltipText(0, entry.EventType.FullName ?? entry.EventType.Name);
            item.SetMetadata(0, entry.EventType.FullName ?? entry.EventType.Name);
        }
    }

    private bool MatchesEventSearch(Type eventType)
    {
        if (string.IsNullOrEmpty(_eventsSearchQuery))
            return true;

        return eventType.Name.Contains(_eventsSearchQuery, StringComparison.OrdinalIgnoreCase) ||
            (eventType.FullName?.Contains(
                _eventsSearchQuery,
                StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private void AppendWarnings()
    {
        int matchingCount = 0;
        foreach (DebuggerErrorEntry entry in _recentWarnings)
        {
            if (MatchesConsoleLevel(entry) && MatchesConsoleSearch(entry))
                matchingCount++;
        }
        if (matchingCount == 0)
            return;

        AppendSection("【最近警告 / 错误】");
        int skipCount = Math.Max(0, matchingCount - MaxDisplayedWarnings);
        int index = 0;
        foreach (DebuggerErrorEntry error in _recentWarnings)
        {
            if (!MatchesConsoleLevel(error) || !MatchesConsoleSearch(error))
                continue;
            if (index++ < skipCount)
                continue;

            _textBuilder.Append(error.Timestamp.ToString("HH:mm:ss"))
                .Append(' ').Append('[').Append(error.Level).Append("] ")
                .Append(error.Module).Append(": ").AppendLine(error.Message);
        }
    }

    private void AppendLogs(LogEntry[] logs)
    {
        int matchingCount = 0;
        for (int index = 0; index < logs.Length; index++)
        {
            if (MatchesConsoleLevel(logs[index]) &&
                MatchesConsoleSearch(logs[index]))
                matchingCount++;
        }
        int pageCount = Math.Max(1, (matchingCount + ConsoleLogsPerPage - 1) / ConsoleLogsPerPage);
        _consolePageOffset = Math.Clamp(_consolePageOffset, 0, pageCount - 1);
        int pageEnd = Math.Max(0, matchingCount - _consolePageOffset * ConsoleLogsPerPage);
        int pageStart = Math.Max(0, pageEnd - ConsoleLogsPerPage);
        UpdateConsolePagination(matchingCount, pageStart, pageEnd, pageCount);
        if (matchingCount == 0)
            return;

        int matchingIndex = 0;
        for (int i = 0; i < logs.Length; i++)
        {
            LogEntry log = logs[i];
            if (!MatchesConsoleLevel(log) ||
                !MatchesConsoleSearch(log))
                continue;
            if (matchingIndex < pageStart)
            {
                matchingIndex++;
                continue;
            }
            if (matchingIndex >= pageEnd)
                break;
            matchingIndex++;

            DateTime lastTimestamp = log.TimestampUtc.ToLocalTime();
            if (log.RepeatCount > 1)
            {
                _textBuilder.Append(log.FirstTimestampUtc.ToLocalTime().ToString("HH:mm:ss"))
                    .Append('–').Append(lastTimestamp.ToString("HH:mm:ss"));
            }
            else
            {
                _textBuilder.Append(lastTimestamp.ToString("HH:mm:ss"));
            }

            _textBuilder.Append(' ').Append('[').Append(log.Level).Append("] ")
                .Append(log.Module).Append(": ");

            if (!string.IsNullOrWhiteSpace(log.Context))
                _textBuilder.Append('(').Append(log.Context).Append(") ");

            _textBuilder.Append(log.Message);
            if (log.RepeatCount > 1)
                _textBuilder.Append(" ×").Append(log.RepeatCount);
            _textBuilder.AppendLine();
        }
    }

    private void UpdateConsolePagination(
        int matchingCount,
        int pageStart,
        int pageEnd,
        int pageCount)
    {
        if (!IsInstanceValid(_olderConsolePageButton) ||
            !IsInstanceValid(_newerConsolePageButton) ||
            !IsInstanceValid(_latestConsolePageButton) ||
            !IsInstanceValid(_consolePageStatus))
            return;

        _olderConsolePageButton.Disabled = pageStart == 0;
        _newerConsolePageButton.Disabled = _consolePageOffset == 0;
        UpdateLatestConsoleButtonState();
        _consolePageStatus.Text = matchingCount == 0
            ? "日志 0"
            : $"日志 {matchingCount} · {pageStart + 1}–{pageEnd} · " +
              $"{pageCount - _consolePageOffset}/{pageCount}";
    }

    private void UpdateLatestConsoleButtonState()
    {
        if (IsInstanceValid(_latestConsolePageButton))
            _latestConsolePageButton.Disabled =
                _consolePageOffset == 0 && _consoleFollowLatest;
    }

    private bool MatchesConsoleLevel(LogEntry entry) =>
        _consoleLevelFilter == ConsoleLevelFilter.All ||
        entry.Level switch
        {
            LogLevel.Debug => (_consoleLevelFilter & ConsoleLevelFilter.Debug) != 0,
            LogLevel.Info => (_consoleLevelFilter & ConsoleLevelFilter.Info) != 0,
            _ => false,
        };

    private bool MatchesConsoleLevel(DebuggerErrorEntry entry) =>
        _consoleLevelFilter == ConsoleLevelFilter.All ||
        (entry.Level == ErrorLevel.Warning
            ? (_consoleLevelFilter & ConsoleLevelFilter.Warning) != 0
            : (_consoleLevelFilter & ConsoleLevelFilter.Error) != 0);

    private bool MatchesConsoleSearch(LogEntry entry)
    {
        string query = _consoleSearchQuery;
        return query.Length == 0 ||
            entry.Level.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Module.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Context?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
            entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesConsoleSearch(DebuggerErrorEntry entry)
    {
        string query = _consoleSearchQuery;
        return query.Length == 0 ||
            entry.Level.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Module.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase);
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
        unchecked
        {
            _consoleErrorVersion++;
        }
    }

    private sealed class DebuggerPageGroup
    {
        public string Path { get; }
        public string Title { get; }
        public List<DebuggerPage> Pages { get; } = new();

        public DebuggerPageGroup(string path, string title)
        {
            Path = path;
            Title = title;
        }
    }

    private sealed class DebuggerPage
    {
        public string Path { get; }
        public string Title { get; }
        public Action Render { get; }
        public bool IsOverview => string.Equals(Path, "Overview", StringComparison.Ordinal);
        public bool IsInput => string.Equals(Path, "Runtime/Input", StringComparison.Ordinal);
        public bool IsScheduler => string.Equals(Path, "Runtime/Scheduler", StringComparison.Ordinal);
        public bool IsAudio => string.Equals(Path, "Runtime/Audio", StringComparison.Ordinal);
        public bool IsServices => string.Equals(Path, "Framework/Services", StringComparison.Ordinal);
        public bool IsEvents => string.Equals(Path, "Framework/Events", StringComparison.Ordinal);
        public bool IsConsole => string.Equals(Path, "Console", StringComparison.Ordinal);

        public DebuggerPage(string path, string title, Action render)
        {
            Path = path;
            Title = title;
            Render = render;
        }
    }

    [Flags]
    private enum ConsoleLevelFilter
    {
        None = 0,
        Debug = 1 << 0,
        Info = 1 << 1,
        Warning = 1 << 2,
        Error = 1 << 3,
        All = Debug | Info | Warning | Error,
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
