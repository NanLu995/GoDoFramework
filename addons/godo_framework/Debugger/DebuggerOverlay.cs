using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
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
    private const int ConsoleLogsPerPage = 100;
    private const string ConsoleWarningColor = "#e4b85a";
    private const string ConsoleErrorColor = "#ff7770";
    private const ulong SceneNodeCountRefreshIntervalMilliseconds = 1000;
    private const int MaxDisplayedInputActions = 32;
    private const int MaxDisplayedResourceOperations = 32;
    private const int MaxDisplayedResourceHistory = 8;
    private const int MaxDisplayedDataTableDataSets = 32;
    private const int MaxDisplayedDataTableTables = 64;
    private const int MaxDisplayedDataTableHistory = 8;
    private const int MaxDisplayedUiEntries = 64;
    private const int PerformanceSampleCapacity = 120;
    private const int PerformanceMetricCount = 25;
    private const int PerformanceMetricEngineMemory = 0;
    private const int PerformanceMetricEngineMemoryPeak = 1;
    private const int PerformanceMetricManagedMemory = 2;
    private const int PerformanceMetricMessageBufferPeak = 3;
    private const int PerformanceMetricVideoMemory = 4;
    private const int PerformanceMetricTextureMemory = 5;
    private const int PerformanceMetricBufferMemory = 6;
    private const int PerformanceMetricObjects = 7;
    private const int PerformanceMetricResources = 8;
    private const int PerformanceMetricNodes = 9;
    private const int PerformanceMetricOrphans = 10;
    private const int PerformanceMetricRenderObjects = 11;
    private const int PerformanceMetricPrimitives = 12;
    private const int PerformanceMetricDrawCalls = 13;
    private const int PerformanceMetricPhysics2DActive = 14;
    private const int PerformanceMetricPhysics2DPairs = 15;
    private const int PerformanceMetricPhysics2DIslands = 16;
    private const int PerformanceMetricPhysics3DActive = 17;
    private const int PerformanceMetricPhysics3DPairs = 18;
    private const int PerformanceMetricPhysics3DIslands = 19;
    private const int PerformanceMetricPipelineCanvas = 20;
    private const int PerformanceMetricPipelineMesh = 21;
    private const int PerformanceMetricPipelineSurface = 22;
    private const int PerformanceMetricPipelineDraw = 23;
    private const int PerformanceMetricPipelineSpecialization = 24;
    private const int SystemMetricCount = 18;
    private const int SystemMetricGodotVersion = 0;
    private const int SystemMetricDotNetRuntime = 1;
    private const int SystemMetricBuild = 2;
    private const int SystemMetricProcessId = 3;
    private const int SystemMetricProcessArchitecture = 4;
    private const int SystemMetricPlatform = 5;
    private const int SystemMetricOsVersion = 6;
    private const int SystemMetricLocale = 7;
    private const int SystemMetricDisplayServer = 8;
    private const int SystemMetricWindowMode = 9;
    private const int SystemMetricWindowSize = 10;
    private const int SystemMetricScreenSize = 11;
    private const int SystemMetricVsync = 12;
    private const int SystemMetricRenderingMethod = 13;
    private const int SystemMetricRenderingDriver = 14;
    private const int SystemMetricAdapter = 15;
    private const int SystemMetricAdapterVendor = 16;
    private const int SystemMetricAdapterType = 17;
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
    private readonly DebuggerErrorEntry[] _consoleErrorSnapshot =
        new DebuggerErrorEntry[MaxStoredWarnings];
    private readonly StringBuilder _textBuilder = new(1024);
    private readonly StringBuilder _consoleMarkupBuilder = new(1024);
    private readonly List<DebuggerPageGroup> _pageGroups = new();
    private readonly Dictionary<TreeItem, DebuggerPage> _pagesByTreeItem = new();
    private readonly double[] _performanceProcessSamples = new double[PerformanceSampleCapacity];
    private readonly double[] _performancePhysicsSamples = new double[PerformanceSampleCapacity];
    private readonly double[] _performanceEngineMemorySamples = new double[PerformanceSampleCapacity];
    private readonly double[] _performanceManagedMemorySamples = new double[PerformanceSampleCapacity];
    private readonly Vector2[] _performancePrimaryGraphPoints =
        new Vector2[PerformanceSampleCapacity];
    private readonly Vector2[] _performanceSecondaryGraphPoints =
        new Vector2[PerformanceSampleCapacity];
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
    private Control? _systemDashboard;
    private Label? _systemPlatformValue;
    private Label? _systemPlatformDetail;
    private Label? _systemBuildValue;
    private Label? _systemBuildDetail;
    private Label? _systemRendererValue;
    private Label? _systemRendererDetail;
    private Label? _systemUptimeValue;
    private Tree? _systemDetailsTree;
    private readonly TreeItem?[] _systemMetricRows = new TreeItem?[SystemMetricCount];
    private Control? _performanceDashboard;
    private Label? _performanceFpsValue;
    private Label? _performanceProcessValue;
    private Label? _performancePhysicsValue;
    private Label? _performanceMemoryValue;
    private Label? _performanceManagedMemoryValue;
    private Control? _performanceFrameGraph;
    private Control? _performanceMemoryGraph;
    private Tree? _performanceMetricsTree;
    private readonly TreeItem?[] _performanceMetricRows =
        new TreeItem?[PerformanceMetricCount];
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
    private Control? _sceneDashboard;
    private Label? _sceneCurrentValue;
    private Label? _sceneCurrentDetail;
    private Label? _sceneNodeCountValue;
    private Label? _sceneStateValue;
    private Label? _sceneProgressValue;
    private Tree? _sceneDetailsTree;
    private Control? _resourcesDashboard;
    private Label? _resourcesActiveValue;
    private Label? _resourcesRequestsValue;
    private Label? _resourcesMergedValue;
    private Label? _resourcesResultValue;
    private Label? _resourcesActiveStatus;
    private Tree? _resourcesActiveTree;
    private Label? _resourcesHistoryStatus;
    private Tree? _resourcesHistoryTree;
    private Control? _dataTableDashboard;
    private Label? _dataTableLoadedValue;
    private Label? _dataTableTablesValue;
    private Label? _dataTableLoadingValue;
    private Label? _dataTableFailedValue;
    private Label? _dataTableDataSetStatus;
    private Tree? _dataTableDataSetTree;
    private Label? _dataTableHistoryStatus;
    private Tree? _dataTableHistoryTree;
    private Control? _uiDashboard;
    private Label? _uiSceneValue;
    private Label? _uiViewValue;
    private Label? _uiModalValue;
    private Label? _uiOverlayValue;
    private Label? _uiCurrentValue;
    private Label? _uiCurrentDetail;
    private Label? _uiStackStatus;
    private Tree? _uiStackTree;
    private Control? _procedureDashboard;
    private Label? _procedureCurrentValue;
    private Label? _procedureStateValue;
    private Label? _procedurePendingValue;
    private Label? _procedureResultValue;
    private Tree? _procedureDetailsTree;
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
    private Button? _consoleFileLink;
    private Control? _resizeRow;
    private Control? _resizeGrip;
    private DebuggerPage? _selectedPage;
    private float _minimumFpsButtonWidth;
    private double _refreshElapsed;
    private ulong _sceneNodeCountRefreshTicks;
    private ulong _sceneNodeCountRootInstanceId;
    private int _sceneNodeCount;
    private Vector2 _expandedSize = new(DefaultExpandedWidth, DefaultExpandedHeight);
    private Vector2 _pointerStart;
    private Vector2 _panelPositionStart;
    private Vector2 _panelSizeStart;
    private string _consoleSearchQuery = string.Empty;
    private string _consoleFilePath = string.Empty;
    private string _inputActionsSearchQuery = string.Empty;
    private int _inputContextsSignature = int.MinValue;
    private int _inputActionsSignature = int.MinValue;
    private string _servicesSearchQuery = string.Empty;
    private int _servicesSnapshotSignature = int.MinValue;
    private string _eventsSearchQuery = string.Empty;
    private int _eventsSnapshotSignature = int.MinValue;
    private int _dataTableSnapshotVersion = int.MinValue;
    private int _performanceSampleCount;
    private int _performanceSampleWriteIndex;
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
    /// <summary>系统环境仪表盘根节点路径。</summary>
    [Export] public NodePath SystemDashboardPath { get; set; } = null!;
    /// <summary>性能仪表盘根节点路径。</summary>
    [Export] public NodePath PerformanceDashboardPath { get; set; } = null!;
    /// <summary>Input 仪表盘根节点路径。</summary>
    [Export] public NodePath InputDashboardPath { get; set; } = null!;
    /// <summary>Scheduler 仪表盘根节点路径。</summary>
    [Export] public NodePath SchedulerDashboardPath { get; set; } = null!;
    /// <summary>Audio 仪表盘根节点路径。</summary>
    [Export] public NodePath AudioDashboardPath { get; set; } = null!;
    /// <summary>Scene 仪表盘根节点路径。</summary>
    [Export] public NodePath SceneDashboardPath { get; set; } = null!;
    /// <summary>Resources 仪表盘根节点路径。</summary>
    [Export] public NodePath ResourcesDashboardPath { get; set; } = null!;
    /// <summary>DataTable 仪表盘根节点路径。</summary>
    [Export] public NodePath DataTableDashboardPath { get; set; } = null!;
    /// <summary>UI 仪表盘根节点路径。</summary>
    [Export] public NodePath UiDashboardPath { get; set; } = null!;
    /// <summary>Procedure 仪表盘根节点路径。</summary>
    [Export] public NodePath ProcedureDashboardPath { get; set; } = null!;
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
    /// <summary>控制台当前日志文件链接节点路径。</summary>
    [Export] public NodePath ConsoleFileLinkPath { get; set; } = null!;
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
        _systemDashboard = GetNodeOrNull<Control>(SystemDashboardPath);
        _performanceDashboard = GetNodeOrNull<Control>(PerformanceDashboardPath);
        _inputDashboard = GetNodeOrNull<Control>(InputDashboardPath);
        _servicesDashboard = GetNodeOrNull<Control>(ServicesDashboardPath);
        _eventsDashboard = GetNodeOrNull<Control>(EventsDashboardPath);
        _schedulerDashboard = GetNodeOrNull<Control>(SchedulerDashboardPath);
        _audioDashboard = GetNodeOrNull<Control>(AudioDashboardPath);
        _sceneDashboard = GetNodeOrNull<Control>(SceneDashboardPath);
        _resourcesDashboard = GetNodeOrNull<Control>(ResourcesDashboardPath);
        _dataTableDashboard = GetNodeOrNull<Control>(DataTableDashboardPath);
        _uiDashboard = GetNodeOrNull<Control>(UiDashboardPath);
        _procedureDashboard = GetNodeOrNull<Control>(ProcedureDashboardPath);
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
        _consoleFileLink = GetNodeOrNull<Button>(ConsoleFileLinkPath);
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
            !IsInstanceValid(_systemDashboard) ||
            !IsInstanceValid(_performanceDashboard) ||
            !IsInstanceValid(_inputDashboard) ||
            !IsInstanceValid(_servicesDashboard) ||
            !IsInstanceValid(_eventsDashboard) ||
            !IsInstanceValid(_schedulerDashboard) ||
            !IsInstanceValid(_audioDashboard) ||
            !IsInstanceValid(_sceneDashboard) ||
            !IsInstanceValid(_resourcesDashboard) ||
            !IsInstanceValid(_dataTableDashboard) ||
            !IsInstanceValid(_uiDashboard) ||
            !IsInstanceValid(_procedureDashboard) ||
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
            !IsInstanceValid(_consoleFileLink) ||
            !IsInstanceValid(_resizeRow) ||
            !IsInstanceValid(_resizeGrip))
        {
            throw new InvalidOperationException("DebuggerOverlay 场景缺少必要的导出节点引用。");
        }

        _minimumFpsButtonWidth = _toggleButton.GetCombinedMinimumSize().X;
        _toggleButton.CustomMinimumSize = new Vector2(
            _minimumFpsButtonWidth,
            _toggleButton.CustomMinimumSize.Y);
        CacheOverviewNodes();
        CacheSystemNodes();
        CachePerformanceNodes();
        CacheInputNodes();
        CacheSchedulerNodes();
        CacheAudioNodes();
        CacheSceneNodes();
        CacheResourcesNodes();
        CacheDataTableNodes();
        CacheUiNodes();
        CacheProcedureNodes();
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
        _performanceFrameGraph!.Draw += OnPerformanceFrameGraphDraw;
        _performanceMemoryGraph!.Draw += OnPerformanceMemoryGraphDraw;
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
        _consoleFileLink.Pressed += OnConsoleFileLinkPressed;
        _consoleScrollBar.ValueChanged += OnConsoleScrollValueChanged;
        _header.GuiInput += OnHeaderGuiInput;
        _resizeGrip.GuiInput += OnResizeGripGuiInput;
        ResetLayout();
        RefreshHealthStatus();
        ApplyExpandedState();
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
        if (IsInstanceValid(_performanceFrameGraph))
            _performanceFrameGraph.Draw -= OnPerformanceFrameGraphDraw;
        if (IsInstanceValid(_performanceMemoryGraph))
            _performanceMemoryGraph.Draw -= OnPerformanceMemoryGraphDraw;
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
        if (IsInstanceValid(_consoleFileLink))
            _consoleFileLink.Pressed -= OnConsoleFileLinkPressed;
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
        _systemDashboard = null;
        _systemPlatformValue = null;
        _systemPlatformDetail = null;
        _systemBuildValue = null;
        _systemBuildDetail = null;
        _systemRendererValue = null;
        _systemRendererDetail = null;
        _systemUptimeValue = null;
        _systemDetailsTree = null;
        Array.Clear(_systemMetricRows);
        _performanceDashboard = null;
        _performanceFpsValue = null;
        _performanceProcessValue = null;
        _performancePhysicsValue = null;
        _performanceMemoryValue = null;
        _performanceManagedMemoryValue = null;
        _performanceFrameGraph = null;
        _performanceMemoryGraph = null;
        _performanceMetricsTree = null;
        Array.Clear(_performanceMetricRows);
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
        _sceneDashboard = null;
        _sceneCurrentValue = null;
        _sceneCurrentDetail = null;
        _sceneNodeCountValue = null;
        _sceneStateValue = null;
        _sceneProgressValue = null;
        _sceneDetailsTree = null;
        _resourcesDashboard = null;
        _resourcesActiveValue = null;
        _resourcesRequestsValue = null;
        _resourcesMergedValue = null;
        _resourcesResultValue = null;
        _resourcesActiveStatus = null;
        _resourcesActiveTree = null;
        _resourcesHistoryStatus = null;
        _resourcesHistoryTree = null;
        _uiDashboard = null;
        _uiSceneValue = null;
        _uiViewValue = null;
        _uiModalValue = null;
        _uiOverlayValue = null;
        _uiCurrentValue = null;
        _uiCurrentDetail = null;
        _uiStackStatus = null;
        _uiStackTree = null;
        _procedureDashboard = null;
        _procedureCurrentValue = null;
        _procedureStateValue = null;
        _procedurePendingValue = null;
        _procedureResultValue = null;
        _procedureDetailsTree = null;
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
        _consoleFileLink = null;
        _consoleFilePath = string.Empty;
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
        _toggleButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        ApplyPanelSize();
    }

    private void ApplyPanelSize()
    {
        if (!IsInstanceValid(_panel))
            return;

        if (!_expanded)
        {
            _panel.Size = _panel.GetCombinedMinimumSize();
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

        _toggleButton.Text = $"FPS: {Mathf.RoundToInt(Engine.GetFramesPerSecond())}";

        Color statusColor = errorCount > 0
            ? new Color(1f, 0.42f, 0.38f)
            : warningCount > 0 ? new Color(1f, 0.76f, 0.28f) : new Color(0.88f, 0.94f, 1f);
        _toggleButton.AddThemeColorOverride("font_color", statusColor);
        _toggleButton.AddThemeColorOverride("font_outline_color", statusColor);
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

        bool pageReadSucceeded = false;
        _textBuilder.Clear();
        try
        {
            _selectedPage?.Render();
            if (_selectedPage is not null)
                ApplyPageContentVisibility(_selectedPage, showReadFailure: false);
            pageReadSucceeded = true;
        }
        catch (Exception exception)
        {
            ShowPageReadFailure(exception);
        }
        string text = _textBuilder.ToString().ReplaceLineEndings("\n");
        string displayedText = isConsole ? BuildConsoleMarkup(text) : text;
        _debuggerLabel.BbcodeEnabled = isConsole;
        if (!string.Equals(_debuggerLabel.Text, displayedText, StringComparison.Ordinal))
            _debuggerLabel.Text = displayedText;

        if (isConsole && pageReadSucceeded)
        {
            _lastConsoleLogVersion = LogHub.DebugHistoryVersion;
            _lastConsoleErrorVersion = _consoleErrorVersion;
            if (_consolePageOffset == 0 && _consoleFollowLatest)
                ScrollConsoleToBottom();
        }
    }

    private void ShowPageReadFailure(Exception exception)
    {
        if (_selectedPage is null)
            return;

        ApplyPageContentVisibility(_selectedPage, showReadFailure: true);
        _textBuilder.Clear();
        _textBuilder.Append("页面读取失败：")
            .AppendLine(_selectedPage.Title)
            .Append(exception.GetType().Name)
            .Append(": ")
            .Append(exception.Message);
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

    private void CacheSystemNodes()
    {
        _systemPlatformValue = GetSystemNode<Label>("Summary/PlatformCard/Content/Value");
        _systemPlatformDetail = GetSystemNode<Label>("Summary/PlatformCard/Content/Detail");
        _systemBuildValue = GetSystemNode<Label>("Summary/BuildCard/Content/Value");
        _systemBuildDetail = GetSystemNode<Label>("Summary/BuildCard/Content/Detail");
        _systemRendererValue = GetSystemNode<Label>("Summary/RendererCard/Content/Value");
        _systemRendererDetail = GetSystemNode<Label>("Summary/RendererCard/Content/Detail");
        _systemUptimeValue = GetSystemNode<Label>("Summary/UptimeCard/Content/Value");
        _systemDetailsTree = GetSystemNode<Tree>("Details");

        _systemDetailsTree.SetColumnTitle(0, "分类");
        _systemDetailsTree.SetColumnTitle(1, "项目");
        _systemDetailsTree.SetColumnTitle(2, "值");
        _systemDetailsTree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _systemDetailsTree.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _systemDetailsTree.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _systemDetailsTree.SetColumnExpand(0, false);
        _systemDetailsTree.SetColumnExpand(1, false);
        _systemDetailsTree.SetColumnExpand(2, true);
        _systemDetailsTree.SetColumnCustomMinimumWidth(0, 72);
        _systemDetailsTree.SetColumnCustomMinimumWidth(1, 132);
        BuildSystemRows();
        InitializeSystemStaticValues();
    }

    private void BuildSystemRows()
    {
        _systemDetailsTree!.Clear();
        TreeItem root = _systemDetailsTree.CreateItem();
        TreeItem runtime = CreateSystemGroup(root, "运行时");
        _systemMetricRows[SystemMetricGodotVersion] = CreateSystemRow(runtime, "Godot");
        _systemMetricRows[SystemMetricDotNetRuntime] = CreateSystemRow(runtime, ".NET");
        _systemMetricRows[SystemMetricBuild] = CreateSystemRow(runtime, "构建");
        _systemMetricRows[SystemMetricProcessId] = CreateSystemRow(runtime, "Process ID");
        _systemMetricRows[SystemMetricProcessArchitecture] = CreateSystemRow(runtime, "进程架构");

        TreeItem platform = CreateSystemGroup(root, "平台");
        _systemMetricRows[SystemMetricPlatform] = CreateSystemRow(platform, "操作系统");
        _systemMetricRows[SystemMetricOsVersion] = CreateSystemRow(platform, "系统版本");
        _systemMetricRows[SystemMetricLocale] = CreateSystemRow(platform, "Locale");

        TreeItem window = CreateSystemGroup(root, "窗口");
        _systemMetricRows[SystemMetricDisplayServer] = CreateSystemRow(window, "Display Server");
        _systemMetricRows[SystemMetricWindowMode] = CreateSystemRow(window, "窗口模式");
        _systemMetricRows[SystemMetricWindowSize] = CreateSystemRow(window, "窗口尺寸");
        _systemMetricRows[SystemMetricScreenSize] = CreateSystemRow(window, "屏幕尺寸");
        _systemMetricRows[SystemMetricVsync] = CreateSystemRow(window, "VSync");

        TreeItem rendering = CreateSystemGroup(root, "渲染");
        _systemMetricRows[SystemMetricRenderingMethod] = CreateSystemRow(rendering, "Method");
        _systemMetricRows[SystemMetricRenderingDriver] = CreateSystemRow(rendering, "Driver");
        _systemMetricRows[SystemMetricAdapter] = CreateSystemRow(rendering, "显卡");
        _systemMetricRows[SystemMetricAdapterVendor] = CreateSystemRow(rendering, "厂商");
        _systemMetricRows[SystemMetricAdapterType] = CreateSystemRow(rendering, "类型");
    }

    private void InitializeSystemStaticValues()
    {
        string platform = ReadSystemValue(OS.GetName);
        string osVersion = ReadSystemValue(OS.GetVersion);
        string godotVersion = ReadSystemValue(
            () => Engine.GetVersionInfo()["string"].AsString());
        string dotNetRuntime = ReadSystemValue(() => RuntimeInformation.FrameworkDescription);
        string processArchitecture = ReadSystemValue(
            () => RuntimeInformation.ProcessArchitecture.ToString());
        string processId = ReadSystemValue(
            () => OS.GetProcessId().ToString(CultureInfo.InvariantCulture));
        string displayServer = ReadSystemValue(DisplayServer.GetName);
        string renderingMethod = ReadSystemValue(
            () => FormatRenderingMethod(RenderingServer.GetCurrentRenderingMethod()));
        string renderingDriver = ReadSystemValue(
            () => FormatRenderingDriver(RenderingServer.GetCurrentRenderingDriverName()));
        string adapter = ReadSystemValue(RenderingServer.GetVideoAdapterName);
        string adapterVendor = ReadSystemValue(RenderingServer.GetVideoAdapterVendor);
        string adapterType = ReadSystemValue(
            () => FormatVideoAdapterType(RenderingServer.GetVideoAdapterType()));

        _systemPlatformValue!.Text = platform;
        _systemPlatformValue.TooltipText = platform;
        _systemPlatformDetail!.Text = osVersion;
        _systemPlatformDetail.TooltipText = osVersion;
        _systemBuildValue!.Text = "Debug";
        _systemBuildDetail!.Text = $"Godot {godotVersion} / .NET";
        _systemBuildDetail.TooltipText = $"{godotVersion} / {dotNetRuntime}";
        _systemRendererValue!.Text = renderingMethod;
        _systemRendererValue.TooltipText = renderingMethod;
        _systemRendererDetail!.Text = renderingDriver;
        _systemRendererDetail.TooltipText = renderingDriver;

        SetSystemValue(SystemMetricGodotVersion, godotVersion);
        SetSystemValue(SystemMetricDotNetRuntime, dotNetRuntime);
        SetSystemValue(SystemMetricBuild, "Debug");
        SetSystemValue(SystemMetricProcessId, processId);
        SetSystemValue(SystemMetricProcessArchitecture, processArchitecture);
        SetSystemValue(SystemMetricPlatform, platform);
        SetSystemValue(SystemMetricOsVersion, osVersion);
        SetSystemValue(SystemMetricDisplayServer, displayServer);
        SetSystemValue(SystemMetricRenderingMethod, renderingMethod);
        SetSystemValue(SystemMetricRenderingDriver, renderingDriver);
        SetSystemValue(SystemMetricAdapter, adapter);
        SetSystemValue(SystemMetricAdapterVendor, adapterVendor);
        SetSystemValue(SystemMetricAdapterType, adapterType);
    }

    private static string FormatRenderingMethod(string method)
    {
        return method switch
        {
            "forward_plus" => "Forward+",
            "mobile" => "Mobile",
            "gl_compatibility" => "Compatibility",
            _ => method,
        };
    }

    private static string FormatRenderingDriver(string driver)
    {
        return driver switch
        {
            "vulkan" => "Vulkan",
            "d3d12" => "Direct3D 12",
            "metal" => "Metal",
            "opengl3" => "OpenGL 3",
            "opengl3_es" => "OpenGL ES 3",
            "opengl3_angle" => "OpenGL 3 (ANGLE)",
            _ => driver,
        };
    }

    private static string FormatVideoAdapterType(RenderingDevice.DeviceType type)
    {
        return type switch
        {
            RenderingDevice.DeviceType.IntegratedGpu => "集成显卡",
            RenderingDevice.DeviceType.DiscreteGpu => "独立显卡",
            RenderingDevice.DeviceType.VirtualGpu => "虚拟显卡",
            RenderingDevice.DeviceType.Cpu => "软件渲染",
            RenderingDevice.DeviceType.Other => "其他 / 未知",
            _ => type.ToString(),
        };
    }

    private static TreeItem CreateSystemGroup(TreeItem root, string title)
    {
        TreeItem group = root.CreateChild();
        group.SetText(0, title);
        group.SetSelectable(0, false);
        group.Collapsed = false;
        return group;
    }

    private static TreeItem CreateSystemRow(TreeItem group, string property)
    {
        TreeItem row = group.CreateChild();
        row.SetText(1, property);
        row.SetText(2, "不可用");
        row.SetTextAlignment(1, HorizontalAlignment.Left);
        row.SetTextAlignment(2, HorizontalAlignment.Left);
        return row;
    }

    private void SetSystemValue(int index, string value)
    {
        TreeItem? row = _systemMetricRows[index];
        if (row is null)
            return;

        string displayValue = string.IsNullOrWhiteSpace(value) ? "不可用" : value;
        row.SetText(2, displayValue);
        row.SetTooltipText(2, displayValue);
    }

    private T GetSystemNode<T>(string path) where T : Node
    {
        T? node = _systemDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerSystem 场景缺少节点：{path}");
    }

    private static string ReadSystemValue(Func<string> read)
    {
        try
        {
            string value = read();
            return string.IsNullOrWhiteSpace(value) ? "不可用" : value;
        }
        catch
        {
            return "不可用";
        }
    }

    private void CachePerformanceNodes()
    {
        _performanceFpsValue =
            GetPerformanceNode<Label>("Content/Summary/FpsCard/Content/Value");
        _performanceProcessValue =
            GetPerformanceNode<Label>("Content/Summary/ProcessCard/Content/Value");
        _performancePhysicsValue =
            GetPerformanceNode<Label>("Content/Summary/PhysicsCard/Content/Value");
        _performanceMemoryValue =
            GetPerformanceNode<Label>("Content/Summary/MemoryCard/Content/Value");
        _performanceManagedMemoryValue =
            GetPerformanceNode<Label>("Content/Summary/ManagedMemoryCard/Content/Value");
        _performanceFrameGraph =
            GetPerformanceNode<Control>("Content/Trends/FramePanel/Content/Graph");
        _performanceMemoryGraph =
            GetPerformanceNode<Control>("Content/Trends/MemoryPanel/Content/Graph");
        _performanceMetricsTree = GetPerformanceNode<Tree>("Content/Metrics");

        _performanceMetricsTree.SetColumnTitle(0, "分类");
        _performanceMetricsTree.SetColumnTitle(1, "指标");
        _performanceMetricsTree.SetColumnTitle(2, "数值");
        _performanceMetricsTree.SetColumnTitle(3, "说明");
        _performanceMetricsTree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        _performanceMetricsTree.SetColumnTitleAlignment(1, HorizontalAlignment.Left);
        _performanceMetricsTree.SetColumnTitleAlignment(2, HorizontalAlignment.Right);
        _performanceMetricsTree.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
        _performanceMetricsTree.SetColumnExpand(0, false);
        _performanceMetricsTree.SetColumnExpand(1, true);
        _performanceMetricsTree.SetColumnExpand(2, false);
        _performanceMetricsTree.SetColumnExpand(3, true);
        _performanceMetricsTree.SetColumnCustomMinimumWidth(0, 62);
        _performanceMetricsTree.SetColumnCustomMinimumWidth(2, 108);
        BuildPerformanceMetricRows();
    }

    private void BuildPerformanceMetricRows()
    {
        _performanceMetricsTree!.Clear();
        TreeItem root = _performanceMetricsTree.CreateItem();
        TreeItem memory = CreatePerformanceMetricGroup(root, "内存");
        _performanceMetricRows[PerformanceMetricEngineMemory] =
            CreatePerformanceMetricRow(memory, "引擎当前", "字节");
        _performanceMetricRows[PerformanceMetricEngineMemoryPeak] =
            CreatePerformanceMetricRow(memory, "引擎历史峰值", "Debug");
        _performanceMetricRows[PerformanceMetricManagedMemory] =
            CreatePerformanceMetricRow(memory, ".NET 托管堆", "不触发 GC");
        _performanceMetricRows[PerformanceMetricMessageBufferPeak] =
            CreatePerformanceMetricRow(memory, "消息缓冲峰值", "Deferred");
        _performanceMetricRows[PerformanceMetricVideoMemory] =
            CreatePerformanceMetricRow(memory, "显存总量", "纹理 + Buffer");
        _performanceMetricRows[PerformanceMetricTextureMemory] =
            CreatePerformanceMetricRow(memory, "纹理显存", "当前");
        _performanceMetricRows[PerformanceMetricBufferMemory] =
            CreatePerformanceMetricRow(memory, "Buffer 显存", "当前");

        TreeItem objects = CreatePerformanceMetricGroup(root, "对象");
        _performanceMetricRows[PerformanceMetricObjects] =
            CreatePerformanceMetricRow(objects, "Object", "包含 Node");
        _performanceMetricRows[PerformanceMetricResources] =
            CreatePerformanceMetricRow(objects, "Resource", "当前实例");
        _performanceMetricRows[PerformanceMetricNodes] =
            CreatePerformanceMetricRow(objects, "Node", "场景树");
        _performanceMetricRows[PerformanceMetricOrphans] =
            CreatePerformanceMetricRow(objects, "Orphan Node", "非零需检查");

        TreeItem rendering = CreatePerformanceMetricGroup(root, "渲染");
        _performanceMetricRows[PerformanceMetricRenderObjects] =
            CreatePerformanceMetricRow(rendering, "可见对象", "上一渲染帧");
        _performanceMetricRows[PerformanceMetricPrimitives] =
            CreatePerformanceMetricRow(rendering, "Primitive", "含额外 Pass");
        _performanceMetricRows[PerformanceMetricDrawCalls] =
            CreatePerformanceMetricRow(rendering, "Draw Call", "上一渲染帧");

        TreeItem physics2D = CreatePerformanceMetricGroup(root, "物理 2D");
        _performanceMetricRows[PerformanceMetricPhysics2DActive] =
            CreatePerformanceMetricRow(physics2D, "活动对象", "RigidBody2D");
        _performanceMetricRows[PerformanceMetricPhysics2DPairs] =
            CreatePerformanceMetricRow(physics2D, "碰撞对", "当前");
        _performanceMetricRows[PerformanceMetricPhysics2DIslands] =
            CreatePerformanceMetricRow(physics2D, "Island", "当前");

        TreeItem physics3D = CreatePerformanceMetricGroup(root, "物理 3D");
        _performanceMetricRows[PerformanceMetricPhysics3DActive] =
            CreatePerformanceMetricRow(physics3D, "活动对象", "RigidBody / Vehicle");
        _performanceMetricRows[PerformanceMetricPhysics3DPairs] =
            CreatePerformanceMetricRow(physics3D, "碰撞对", "当前");
        _performanceMetricRows[PerformanceMetricPhysics3DIslands] =
            CreatePerformanceMetricRow(physics3D, "Island", "当前");

        TreeItem pipelines = CreatePerformanceMetricGroup(root, "Pipeline");
        _performanceMetricRows[PerformanceMetricPipelineCanvas] =
            CreatePerformanceMetricRow(pipelines, "Canvas", "累计，只增不减");
        _performanceMetricRows[PerformanceMetricPipelineMesh] =
            CreatePerformanceMetricRow(pipelines, "Mesh", "加载阶段");
        _performanceMetricRows[PerformanceMetricPipelineSurface] =
            CreatePerformanceMetricRow(pipelines, "Surface", "可能产生卡顿");
        _performanceMetricRows[PerformanceMetricPipelineDraw] =
            CreatePerformanceMetricRow(pipelines, "Draw", "运行中卡顿风险");
        _performanceMetricRows[PerformanceMetricPipelineSpecialization] =
            CreatePerformanceMetricRow(pipelines, "Specialization", "后台优化");
    }

    private static TreeItem CreatePerformanceMetricGroup(TreeItem root, string title)
    {
        TreeItem group = root.CreateChild();
        group.SetText(0, title);
        group.SetSelectable(0, false);
        group.Collapsed = false;
        return group;
    }

    private static TreeItem CreatePerformanceMetricRow(
        TreeItem group,
        string metric,
        string note)
    {
        TreeItem row = group.CreateChild();
        row.SetText(1, metric);
        row.SetText(2, "0");
        row.SetText(3, note);
        row.SetTextAlignment(1, HorizontalAlignment.Left);
        row.SetTextAlignment(2, HorizontalAlignment.Right);
        row.SetTextAlignment(3, HorizontalAlignment.Left);
        return row;
    }

    private T GetPerformanceNode<T>(string path) where T : Node
    {
        T? node = _performanceDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerPerformance 场景缺少节点：{path}");
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

    private void CacheSceneNodes()
    {
        _sceneCurrentValue = GetSceneNode<Label>("Summary/CurrentCard/Content/Value");
        _sceneCurrentDetail = GetSceneNode<Label>("Summary/CurrentCard/Content/Detail");
        _sceneNodeCountValue = GetSceneNode<Label>("Summary/NodesCard/Content/Value");
        _sceneStateValue = GetSceneNode<Label>("Summary/StateCard/Content/Value");
        _sceneProgressValue = GetSceneNode<Label>("Summary/ProgressCard/Content/Value");
        _sceneDetailsTree = GetSceneNode<Tree>("Details");
        _sceneDetailsTree.SetColumnTitle(0, "项目");
        _sceneDetailsTree.SetColumnTitle(1, "值");
        _sceneDetailsTree.SetColumnExpand(0, false);
        _sceneDetailsTree.SetColumnExpand(1, true);
        _sceneDetailsTree.SetColumnCustomMinimumWidth(0, 92);
    }

    private T GetSceneNode<T>(string path) where T : Node
    {
        T? node = _sceneDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerScene 场景缺少节点：{path}");
    }

    private void CacheResourcesNodes()
    {
        _resourcesActiveValue = GetResourcesNode<Label>("Summary/ActiveCard/Content/Value");
        _resourcesRequestsValue = GetResourcesNode<Label>("Summary/RequestsCard/Content/Value");
        _resourcesMergedValue = GetResourcesNode<Label>("Summary/MergedCard/Content/Value");
        _resourcesResultValue = GetResourcesNode<Label>("Summary/ResultCard/Content/Value");
        _resourcesActiveStatus = GetResourcesNode<Label>("ActiveStatus");
        _resourcesActiveTree = GetResourcesNode<Tree>("ActiveList");
        _resourcesHistoryStatus = GetResourcesNode<Label>("HistoryStatus");
        _resourcesHistoryTree = GetResourcesNode<Tree>("HistoryList");

        ConfigureResourceTree(_resourcesActiveTree, "资源 Key");
        _resourcesActiveTree.SetColumnTitle(0, "资源 Key");
        _resourcesActiveTree.SetColumnTitle(1, "类型");
        _resourcesActiveTree.SetColumnTitle(2, "状态");
        _resourcesActiveTree.SetColumnTitle(3, "进度");
        _resourcesActiveTree.SetColumnTitle(4, "请求");

        ConfigureResourceTree(_resourcesHistoryTree, "资源 Key");
        _resourcesHistoryTree.SetColumnTitle(0, "资源 Key");
        _resourcesHistoryTree.SetColumnTitle(1, "类型");
        _resourcesHistoryTree.SetColumnTitle(2, "方式");
        _resourcesHistoryTree.SetColumnTitle(3, "状态");
        _resourcesHistoryTree.SetColumnTitle(4, "请求");
    }

    private static void ConfigureResourceTree(Tree tree, string firstColumnTitle)
    {
        tree.SetColumnTitle(0, firstColumnTitle);
        tree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        tree.SetColumnExpand(0, true);
        for (int column = 1; column < 5; column++)
        {
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Center);
            tree.SetColumnExpand(column, false);
        }
        tree.SetColumnCustomMinimumWidth(1, 74);
        tree.SetColumnCustomMinimumWidth(2, 64);
        tree.SetColumnCustomMinimumWidth(3, 64);
        tree.SetColumnCustomMinimumWidth(4, 54);
    }

    private T GetResourcesNode<T>(string path) where T : Node
    {
        T? node = _resourcesDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerResources 场景缺少节点：{path}");
    }

    private void CacheDataTableNodes()
    {
        _dataTableLoadedValue =
            GetDataTableNode<Label>("Summary/LoadedCard/Content/Value");
        _dataTableTablesValue =
            GetDataTableNode<Label>("Summary/TablesCard/Content/Value");
        _dataTableLoadingValue =
            GetDataTableNode<Label>("Summary/LoadingCard/Content/Value");
        _dataTableFailedValue =
            GetDataTableNode<Label>("Summary/FailedCard/Content/Value");
        _dataTableDataSetStatus = GetDataTableNode<Label>("DataSetStatus");
        _dataTableDataSetTree = GetDataTableNode<Tree>("DataSetList");
        _dataTableHistoryStatus = GetDataTableNode<Label>("HistoryStatus");
        _dataTableHistoryTree = GetDataTableNode<Tree>("HistoryList");

        _dataTableDataSetTree.SetColumnTitle(0, "数据集 / 表");
        _dataTableDataSetTree.SetColumnTitle(1, "状态 / 类型");
        _dataTableDataSetTree.SetColumnTitle(2, "表数");
        _dataTableDataSetTree.SetColumnTitle(3, "进度");
        _dataTableDataSetTree.SetColumnTitle(4, "目录 / 详情");
        ConfigureDataTableTree(_dataTableDataSetTree, 5);
        _dataTableDataSetTree.SetColumnCustomMinimumWidth(1, 82);
        _dataTableDataSetTree.SetColumnCustomMinimumWidth(2, 54);
        _dataTableDataSetTree.SetColumnCustomMinimumWidth(3, 58);

        _dataTableHistoryTree.SetColumnTitle(0, "数据集");
        _dataTableHistoryTree.SetColumnTitle(1, "结果");
        _dataTableHistoryTree.SetColumnTitle(2, "表数");
        _dataTableHistoryTree.SetColumnTitle(3, "详情");
        ConfigureDataTableTree(_dataTableHistoryTree, 4);
        _dataTableHistoryTree.SetColumnCustomMinimumWidth(1, 72);
        _dataTableHistoryTree.SetColumnCustomMinimumWidth(2, 54);
    }

    private static void ConfigureDataTableTree(Tree tree, int columnCount)
    {
        tree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        tree.SetColumnExpand(0, true);
        for (int column = 1; column < columnCount - 1; column++)
        {
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Center);
            tree.SetColumnExpand(column, false);
        }
        tree.SetColumnTitleAlignment(columnCount - 1, HorizontalAlignment.Left);
        tree.SetColumnExpand(columnCount - 1, true);
    }

    private T GetDataTableNode<T>(string path) where T : Node
    {
        T? node = _dataTableDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerDataTable 场景缺少节点：{path}");
    }

    private void CacheUiNodes()
    {
        _uiSceneValue = GetUiNode<Label>("Summary/SceneCard/Content/Value");
        _uiViewValue = GetUiNode<Label>("Summary/ViewCard/Content/Value");
        _uiModalValue = GetUiNode<Label>("Summary/ModalCard/Content/Value");
        _uiOverlayValue = GetUiNode<Label>("Summary/OverlayCard/Content/Value");
        _uiCurrentValue = GetUiNode<Label>("Summary/CurrentCard/Content/Value");
        _uiCurrentDetail = GetUiNode<Label>("Summary/CurrentCard/Content/Detail");
        _uiStackStatus = GetUiNode<Label>("StackStatus");
        _uiStackTree = GetUiNode<Tree>("StackList");
        _uiStackTree.SetColumnTitle(0, "层");
        _uiStackTree.SetColumnTitle(1, "顺序");
        _uiStackTree.SetColumnTitle(2, "UI / 节点");
        _uiStackTree.SetColumnTitle(3, "资源 Key");
        _uiStackTree.SetColumnTitle(4, "状态");
        _uiStackTree.SetColumnTitleAlignment(0, HorizontalAlignment.Center);
        _uiStackTree.SetColumnTitleAlignment(1, HorizontalAlignment.Center);
        _uiStackTree.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _uiStackTree.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
        _uiStackTree.SetColumnTitleAlignment(4, HorizontalAlignment.Center);
        _uiStackTree.SetColumnExpand(0, false);
        _uiStackTree.SetColumnExpand(1, false);
        _uiStackTree.SetColumnExpand(2, true);
        _uiStackTree.SetColumnExpand(3, true);
        _uiStackTree.SetColumnExpand(4, false);
        _uiStackTree.SetColumnCustomMinimumWidth(0, 58);
        _uiStackTree.SetColumnCustomMinimumWidth(1, 54);
        _uiStackTree.SetColumnCustomMinimumWidth(4, 88);
    }

    private T GetUiNode<T>(string path) where T : Node
    {
        T? node = _uiDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerUI 场景缺少节点：{path}");
    }

    private void CacheProcedureNodes()
    {
        _procedureCurrentValue = GetProcedureNode<Label>("Summary/CurrentCard/Content/Value");
        _procedureStateValue = GetProcedureNode<Label>("Summary/StateCard/Content/Value");
        _procedurePendingValue = GetProcedureNode<Label>("Summary/PendingCard/Content/Value");
        _procedureResultValue = GetProcedureNode<Label>("Summary/ResultCard/Content/Value");
        _procedureDetailsTree = GetProcedureNode<Tree>("Details");
        _procedureDetailsTree.SetColumnTitle(0, "项目");
        _procedureDetailsTree.SetColumnTitle(1, "值");
        _procedureDetailsTree.SetColumnExpand(0, false);
        _procedureDetailsTree.SetColumnExpand(1, true);
        _procedureDetailsTree.SetColumnCustomMinimumWidth(0, 92);
    }

    private T GetProcedureNode<T>(string path) where T : Node
    {
        T? node = _procedureDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerProcedure 场景缺少节点：{path}");
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
        RegisterPage("System", "系统", "系统", RefreshSystemDashboard);
        RegisterPage("Performance", "性能", "性能", RefreshPerformanceDashboard);
        RegisterPage("Console", "控制台", "控制台", AppendConsole);
        RegisterPage("Runtime/Input", "运行时", "Input", RefreshInputDashboard);
        RegisterPage("Runtime/Scheduler", "运行时", "Scheduler", RefreshSchedulerDashboard);
        RegisterPage("Runtime/Audio", "运行时", "Audio", RefreshAudioDashboard);
        RegisterPage("Runtime/Scene", "运行时", "Scene", RefreshScenePage);
        RegisterPage("Runtime/Resources", "运行时", "Resources", RefreshResourcesPage);
        RegisterPage("Runtime/DataTable", "运行时", "DataTable", RefreshDataTablePage);
        RegisterPage("Runtime/UI", "运行时", "UI", RefreshUiPage);
        RegisterPage("Runtime/Procedure", "运行时", "Procedure", RefreshProcedurePage);
        RegisterPage("Framework/Services", "框架", "Services", RefreshServicesDashboard);
        RegisterPage("Framework/Events", "框架", "Events", RefreshEventsDashboard);
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
        ApplyPageContentVisibility(page, showReadFailure: false);
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

    private void ApplyPageContentVisibility(DebuggerPage page, bool showReadFailure)
    {
        if (IsInstanceValid(_consoleToolbar))
            _consoleToolbar.Visible = _expanded && page.IsConsole && !showReadFailure;
        if (IsInstanceValid(_consoleFilters))
            _consoleFilters.Visible = _expanded && page.IsConsole && !showReadFailure;
        if (IsInstanceValid(_consolePagination))
            _consolePagination.Visible = _expanded && page.IsConsole && !showReadFailure;
        if (IsInstanceValid(_overviewDashboard))
            _overviewDashboard.Visible = page.IsOverview && !showReadFailure;
        if (IsInstanceValid(_systemDashboard))
            _systemDashboard.Visible = page.IsSystem && !showReadFailure;
        if (IsInstanceValid(_performanceDashboard))
            _performanceDashboard.Visible = page.IsPerformance && !showReadFailure;
        if (IsInstanceValid(_inputDashboard))
            _inputDashboard.Visible = page.IsInput && !showReadFailure;
        if (IsInstanceValid(_schedulerDashboard))
            _schedulerDashboard.Visible = page.IsScheduler && !showReadFailure;
        if (IsInstanceValid(_audioDashboard))
            _audioDashboard.Visible = page.IsAudio && !showReadFailure;
        if (IsInstanceValid(_sceneDashboard))
            _sceneDashboard.Visible = page.IsScene && !showReadFailure;
        if (IsInstanceValid(_resourcesDashboard))
            _resourcesDashboard.Visible = page.IsResources && !showReadFailure;
        if (IsInstanceValid(_dataTableDashboard))
            _dataTableDashboard.Visible = page.IsDataTable && !showReadFailure;
        if (IsInstanceValid(_uiDashboard))
            _uiDashboard.Visible = page.IsUi && !showReadFailure;
        if (IsInstanceValid(_procedureDashboard))
            _procedureDashboard.Visible = page.IsProcedure && !showReadFailure;
        if (IsInstanceValid(_servicesDashboard))
            _servicesDashboard.Visible = page.IsServices && !showReadFailure;
        if (IsInstanceValid(_eventsDashboard))
            _eventsDashboard.Visible = page.IsEvents && !showReadFailure;
        if (IsInstanceValid(_debuggerLabel))
        {
            _debuggerLabel.Visible =
                showReadFailure ||
                !page.IsOverview &&
                !page.IsSystem &&
                !page.IsPerformance &&
                !page.IsInput &&
                !page.IsScheduler &&
                !page.IsAudio &&
                !page.IsScene &&
                !page.IsResources &&
                !page.IsDataTable &&
                !page.IsUi &&
                !page.IsProcedure &&
                !page.IsServices &&
                !page.IsEvents;
        }
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

        DisplayServer.ClipboardSet(_debuggerLabel.GetParsedText());
    }

    private void OnConsoleFileLinkPressed()
    {
        if (string.IsNullOrWhiteSpace(_consoleFilePath))
            return;

        Error error = OS.ShellShowInFileManager(_consoleFilePath);
        if (error != Error.Ok)
        {
            ErrorHub.Warn(
                "无法在文件管理器中定位日志文件",
                nameof(DebuggerOverlay),
                $"path={_consoleFilePath}; error={error}");
        }
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

    private void RefreshPerformanceDashboard()
    {
        if (!IsInstanceValid(_performanceFpsValue) ||
            !IsInstanceValid(_performanceProcessValue) ||
            !IsInstanceValid(_performancePhysicsValue) ||
            !IsInstanceValid(_performanceMemoryValue) ||
            !IsInstanceValid(_performanceManagedMemoryValue) ||
            !IsInstanceValid(_performanceFrameGraph) ||
            !IsInstanceValid(_performanceMemoryGraph) ||
            !IsInstanceValid(_performanceMetricsTree))
            return;

        double processSeconds = ReadPerformanceMonitor(Performance.Monitor.TimeProcess);
        double physicsSeconds = ReadPerformanceMonitor(Performance.Monitor.TimePhysicsProcess);
        double engineMemory = ReadPerformanceMonitor(Performance.Monitor.MemoryStatic);
        double managedMemory = GC.GetTotalMemory(forceFullCollection: false);
        double processMilliseconds = processSeconds * 1000d;
        double physicsMilliseconds = physicsSeconds * 1000d;

        _performanceFpsValue.Text =
            Mathf.RoundToInt(Engine.GetFramesPerSecond()).ToString(CultureInfo.InvariantCulture);
        _performanceProcessValue.Text = FormatMilliseconds(processMilliseconds);
        _performancePhysicsValue.Text = FormatMilliseconds(physicsMilliseconds);
        _performanceMemoryValue.Text = FormatBytes(engineMemory);
        _performanceManagedMemoryValue.Text = FormatBytes(managedMemory);

        AddPerformanceSample(
            processMilliseconds,
            physicsMilliseconds,
            engineMemory,
            managedMemory);
        UpdatePerformanceMetrics(engineMemory, managedMemory);
        _performanceFrameGraph.QueueRedraw();
        _performanceMemoryGraph.QueueRedraw();
    }

    private void RefreshSystemDashboard()
    {
        if (!IsInstanceValid(_systemUptimeValue) ||
            !IsInstanceValid(_systemDetailsTree))
            return;

        _systemUptimeValue.Text = FormatUptime(Time.GetTicksMsec());
        SetSystemValue(SystemMetricLocale, ReadCurrentLocale());
        SetSystemValue(SystemMetricWindowMode, ReadWindowMode());
        SetSystemValue(SystemMetricWindowSize, ReadWindowSize());
        SetSystemValue(SystemMetricScreenSize, ReadScreenSize());
        SetSystemValue(SystemMetricVsync, ReadVsyncMode());
    }

    private static string ReadCurrentLocale()
    {
        try
        {
            string locale = TranslationServer.GetLocale();
            return string.IsNullOrWhiteSpace(locale) ? "不可用" : locale;
        }
        catch
        {
            return "不可用";
        }
    }

    private static string ReadWindowMode()
    {
        try
        {
            return DisplayServer.WindowGetMode((int)DisplayServer.MainWindowId) switch
            {
                DisplayServer.WindowMode.Windowed => "窗口",
                DisplayServer.WindowMode.Minimized => "最小化",
                DisplayServer.WindowMode.Maximized => "最大化",
                DisplayServer.WindowMode.Fullscreen => "全屏",
                DisplayServer.WindowMode.ExclusiveFullscreen => "独占全屏",
                _ => "不可用",
            };
        }
        catch
        {
            return "不可用";
        }
    }

    private static string ReadWindowSize()
    {
        try
        {
            return FormatSize(DisplayServer.WindowGetSize((int)DisplayServer.MainWindowId));
        }
        catch
        {
            return "不可用";
        }
    }

    private static string ReadScreenSize()
    {
        try
        {
            return FormatSize(DisplayServer.ScreenGetSize((int)DisplayServer.ScreenOfMainWindow));
        }
        catch
        {
            return "不可用";
        }
    }

    private static string ReadVsyncMode()
    {
        try
        {
            return DisplayServer.WindowGetVsyncMode((int)DisplayServer.MainWindowId) switch
            {
                DisplayServer.VSyncMode.Disabled => "关闭",
                DisplayServer.VSyncMode.Enabled => "开启",
                DisplayServer.VSyncMode.Adaptive => "自适应",
                DisplayServer.VSyncMode.Mailbox => "Mailbox",
                _ => "不可用",
            };
        }
        catch
        {
            return "不可用";
        }
    }

    private static string FormatSize(Vector2I size)
    {
        return size.X > 0 && size.Y > 0
            ? $"{size.X.ToString(CultureInfo.InvariantCulture)} × " +
              size.Y.ToString(CultureInfo.InvariantCulture)
            : "不可用";
    }

    private static string FormatUptime(ulong milliseconds)
    {
        ulong totalSeconds = milliseconds / 1000UL;
        ulong days = totalSeconds / 86400UL;
        ulong hours = totalSeconds / 3600UL % 24UL;
        ulong minutes = totalSeconds / 60UL % 60UL;
        ulong seconds = totalSeconds % 60UL;
        return days > 0UL
            ? $"{days.ToString(CultureInfo.InvariantCulture)}d " +
              $"{hours:00}:{minutes:00}:{seconds:00}"
            : $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private void UpdatePerformanceMetrics(double engineMemory, double managedMemory)
    {
        SetPerformanceMetricValue(PerformanceMetricEngineMemory, FormatBytes(engineMemory));
        SetPerformanceMetricValue(
            PerformanceMetricEngineMemoryPeak,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.MemoryStaticMax)));
        SetPerformanceMetricValue(PerformanceMetricManagedMemory, FormatBytes(managedMemory));
        SetPerformanceMetricValue(
            PerformanceMetricMessageBufferPeak,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.MemoryMessageBufferMax)));
        SetPerformanceMetricValue(
            PerformanceMetricVideoMemory,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.RenderVideoMemUsed)));
        SetPerformanceMetricValue(
            PerformanceMetricTextureMemory,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.RenderTextureMemUsed)));
        SetPerformanceMetricValue(
            PerformanceMetricBufferMemory,
            FormatBytes(ReadPerformanceMonitor(Performance.Monitor.RenderBufferMemUsed)));

        SetPerformanceMetricCount(
            PerformanceMetricObjects,
            Performance.Monitor.ObjectCount);
        SetPerformanceMetricCount(
            PerformanceMetricResources,
            Performance.Monitor.ObjectResourceCount);
        SetPerformanceMetricCount(
            PerformanceMetricNodes,
            Performance.Monitor.ObjectNodeCount);
        double orphanCount = ReadPerformanceMonitor(Performance.Monitor.ObjectOrphanNodeCount);
        SetPerformanceMetricValue(
            PerformanceMetricOrphans,
            FormatCount(orphanCount),
            orphanCount > 0d ? new Color(1f, 0.72f, 0.28f) : null);

        SetPerformanceMetricCount(
            PerformanceMetricRenderObjects,
            Performance.Monitor.RenderTotalObjectsInFrame);
        SetPerformanceMetricCount(
            PerformanceMetricPrimitives,
            Performance.Monitor.RenderTotalPrimitivesInFrame);
        SetPerformanceMetricCount(
            PerformanceMetricDrawCalls,
            Performance.Monitor.RenderTotalDrawCallsInFrame);

        SetPerformanceMetricCount(
            PerformanceMetricPhysics2DActive,
            Performance.Monitor.Physics2DActiveObjects);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics2DPairs,
            Performance.Monitor.Physics2DCollisionPairs);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics2DIslands,
            Performance.Monitor.Physics2DIslandCount);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics3DActive,
            Performance.Monitor.Physics3DActiveObjects);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics3DPairs,
            Performance.Monitor.Physics3DCollisionPairs);
        SetPerformanceMetricCount(
            PerformanceMetricPhysics3DIslands,
            Performance.Monitor.Physics3DIslandCount);

        SetPerformanceMetricCount(
            PerformanceMetricPipelineCanvas,
            Performance.Monitor.PipelineCompilationsCanvas);
        SetPerformanceMetricCount(
            PerformanceMetricPipelineMesh,
            Performance.Monitor.PipelineCompilationsMesh);
        SetPerformanceMetricCount(
            PerformanceMetricPipelineSurface,
            Performance.Monitor.PipelineCompilationsSurface);
        double drawPipelineCount =
            ReadPerformanceMonitor(Performance.Monitor.PipelineCompilationsDraw);
        SetPerformanceMetricValue(
            PerformanceMetricPipelineDraw,
            FormatCount(drawPipelineCount),
            drawPipelineCount > 0d ? new Color(1f, 0.72f, 0.28f) : null);
        SetPerformanceMetricCount(
            PerformanceMetricPipelineSpecialization,
            Performance.Monitor.PipelineCompilationsSpecialization);
    }

    private void SetPerformanceMetricCount(int index, Performance.Monitor monitor)
    {
        SetPerformanceMetricValue(index, FormatCount(ReadPerformanceMonitor(monitor)));
    }

    private void SetPerformanceMetricValue(int index, string value, Color? color = null)
    {
        TreeItem? row = _performanceMetricRows[index];
        if (row is null)
            return;

        row.SetText(2, value);
        if (color.HasValue)
            row.SetCustomColor(2, color.Value);
        else
            row.ClearCustomColor(2);
    }

    private void AddPerformanceSample(
        double processMilliseconds,
        double physicsMilliseconds,
        double engineMemory,
        double managedMemory)
    {
        int index = _performanceSampleWriteIndex;
        _performanceProcessSamples[index] = processMilliseconds;
        _performancePhysicsSamples[index] = physicsMilliseconds;
        _performanceEngineMemorySamples[index] = engineMemory;
        _performanceManagedMemorySamples[index] = managedMemory;
        _performanceSampleWriteIndex = (index + 1) % PerformanceSampleCapacity;
        if (_performanceSampleCount < PerformanceSampleCapacity)
            _performanceSampleCount++;
    }

    private void OnPerformanceFrameGraphDraw()
    {
        DrawPerformanceGraph(
            _performanceFrameGraph,
            _performanceProcessSamples,
            _performancePhysicsSamples,
            minimumMaximum: 16.667d,
            new Color(0.31f, 0.68f, 1f),
            new Color(0.65f, 0.49f, 1f),
            formatAsBytes: false);
    }

    private void OnPerformanceMemoryGraphDraw()
    {
        DrawPerformanceGraph(
            _performanceMemoryGraph,
            _performanceEngineMemorySamples,
            _performanceManagedMemorySamples,
            minimumMaximum: 1024d * 1024d,
            new Color(0.24f, 0.82f, 0.65f),
            new Color(1f, 0.68f, 0.28f),
            formatAsBytes: true);
    }

    private void DrawPerformanceGraph(
        Control? graph,
        double[] primarySamples,
        double[] secondarySamples,
        double minimumMaximum,
        Color primaryColor,
        Color secondaryColor,
        bool formatAsBytes)
    {
        if (!IsInstanceValid(graph))
            return;

        Vector2 size = graph.Size;
        const float verticalPadding = 4f;
        const float axisLabelWidth = 44f;
        const float latestValueWidth = 44f;
        float plotLeft = axisLabelWidth;
        float plotRight = Mathf.Max(plotLeft + 1f, size.X - latestValueWidth);
        float width = plotRight - plotLeft;
        float height = Mathf.Max(1f, size.Y - verticalPadding * 2f);
        Color gridColor = new(0.22f, 0.28f, 0.35f, 0.45f);
        for (int line = 0; line < 3; line++)
        {
            float y = verticalPadding + height * line / 2f;
            graph.DrawLine(
                new Vector2(plotLeft, y),
                new Vector2(plotRight, y),
                gridColor);
        }

        if (_performanceSampleCount == 0)
            return;

        double maximum = minimumMaximum;
        int startIndex = _performanceSampleCount < PerformanceSampleCapacity
            ? 0
            : _performanceSampleWriteIndex;
        for (int pointIndex = 0; pointIndex < _performanceSampleCount; pointIndex++)
        {
            int sampleIndex = (startIndex + pointIndex) % PerformanceSampleCapacity;
            maximum = Math.Max(maximum, primarySamples[sampleIndex]);
            maximum = Math.Max(maximum, secondarySamples[sampleIndex]);
        }

        float denominator = Math.Max(1, _performanceSampleCount - 1);
        for (int pointIndex = 0; pointIndex < _performanceSampleCount; pointIndex++)
        {
            int sampleIndex = (startIndex + pointIndex) % PerformanceSampleCapacity;
            float x = plotLeft + width * pointIndex / denominator;
            _performancePrimaryGraphPoints[pointIndex] = new Vector2(
                x,
                verticalPadding + height * (1f - (float)(primarySamples[sampleIndex] / maximum)));
            _performanceSecondaryGraphPoints[pointIndex] = new Vector2(
                x,
                verticalPadding + height * (1f - (float)(secondarySamples[sampleIndex] / maximum)));
        }

        if (_performanceSampleCount >= 2)
        {
            graph.DrawPolyline(
                _performancePrimaryGraphPoints.AsSpan(0, _performanceSampleCount),
                primaryColor,
                1.5f,
                antialiased: true);
            graph.DrawPolyline(
                _performanceSecondaryGraphPoints.AsSpan(0, _performanceSampleCount),
                secondaryColor,
                1.5f,
                antialiased: true);
        }

        DrawPerformanceGraphLabels(
            graph,
            maximum,
            primarySamples[(_performanceSampleWriteIndex - 1 + PerformanceSampleCapacity) %
                PerformanceSampleCapacity],
            secondarySamples[(_performanceSampleWriteIndex - 1 + PerformanceSampleCapacity) %
                PerformanceSampleCapacity],
            _performancePrimaryGraphPoints[_performanceSampleCount - 1].Y,
            _performanceSecondaryGraphPoints[_performanceSampleCount - 1].Y,
            plotLeft,
            plotRight,
            height,
            primaryColor,
            secondaryColor,
            formatAsBytes);
    }

    private static void DrawPerformanceGraphLabels(
        Control graph,
        double maximum,
        double primaryLatest,
        double secondaryLatest,
        float primaryY,
        float secondaryY,
        float plotLeft,
        float plotRight,
        float plotHeight,
        Color primaryColor,
        Color secondaryColor,
        bool formatAsBytes)
    {
        Font font = graph.GetThemeDefaultFont();
        int fontSize = Math.Clamp(graph.GetThemeDefaultFontSize(), 8, 9);
        Color axisColor = new(0.4f, 0.47f, 0.56f);
        float topBaseline = fontSize;
        float middleBaseline = 4f + plotHeight / 2f + fontSize * 0.35f;
        float bottomBaseline = graph.Size.Y - 2f;
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(0f, topBaseline),
            FormatPerformanceGraphValue(maximum, formatAsBytes),
            plotLeft - 6f,
            HorizontalAlignment.Right,
            fontSize,
            axisColor);
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(0f, middleBaseline),
            FormatPerformanceGraphValue(maximum / 2d, formatAsBytes),
            plotLeft - 6f,
            HorizontalAlignment.Right,
            fontSize,
            axisColor);
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(0f, bottomBaseline),
            FormatPerformanceGraphValue(0d, formatAsBytes),
            plotLeft - 6f,
            HorizontalAlignment.Right,
            fontSize,
            axisColor);

        float primaryBaseline = Mathf.Clamp(
            primaryY + fontSize * 0.35f,
            topBaseline,
            bottomBaseline);
        float secondaryBaseline = Mathf.Clamp(
            secondaryY + fontSize * 0.35f,
            topBaseline,
            bottomBaseline);
        float minimumSeparation = fontSize + 2f;
        if (Mathf.Abs(primaryBaseline - secondaryBaseline) < minimumSeparation)
        {
            float middle = (primaryBaseline + secondaryBaseline) / 2f;
            primaryBaseline = Mathf.Clamp(
                middle - minimumSeparation / 2f,
                topBaseline,
                bottomBaseline - minimumSeparation);
            secondaryBaseline = primaryBaseline + minimumSeparation;
        }

        float latestX = plotRight + 3f;
        float latestWidth = Mathf.Max(1f, graph.Size.X - latestX);
        graph.DrawLine(
            new Vector2(plotRight, primaryY),
            new Vector2(latestX - 1f, primaryBaseline - fontSize * 0.35f),
            primaryColor);
        graph.DrawLine(
            new Vector2(plotRight, secondaryY),
            new Vector2(latestX - 1f, secondaryBaseline - fontSize * 0.35f),
            secondaryColor);
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(latestX, primaryBaseline),
            FormatPerformanceGraphValue(primaryLatest, formatAsBytes),
            latestWidth,
            HorizontalAlignment.Right,
            fontSize,
            primaryColor);
        DrawPerformanceGraphText(
            graph,
            font,
            new Vector2(latestX, secondaryBaseline),
            FormatPerformanceGraphValue(secondaryLatest, formatAsBytes),
            latestWidth,
            HorizontalAlignment.Right,
            fontSize,
            secondaryColor);
    }

    private static void DrawPerformanceGraphText(
        Control graph,
        Font font,
        Vector2 position,
        string text,
        float width,
        HorizontalAlignment alignment,
        int fontSize,
        Color color)
    {
        graph.DrawString(font, position, text, alignment, width, fontSize, color);
    }

    private static string FormatPerformanceGraphValue(double value, bool formatAsBytes)
    {
        return formatAsBytes
            ? FormatBytes(value)
            : FormatMilliseconds(value);
    }

    private static double ReadPerformanceMonitor(Performance.Monitor monitor)
    {
        return Math.Max(0d, Performance.GetMonitor(monitor));
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return $"{milliseconds.ToString("0.00", CultureInfo.InvariantCulture)} ms";
    }

    private static string FormatCount(double value)
    {
        return Math.Round(Math.Max(0d, value))
            .ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatBytes(double bytes)
    {
        double value = Math.Max(0d, bytes);
        string unit = "B";
        if (value >= 1024d)
        {
            value /= 1024d;
            unit = "KiB";
        }
        if (value >= 1024d)
        {
            value /= 1024d;
            unit = "MiB";
        }
        if (value >= 1024d)
        {
            value /= 1024d;
            unit = "GiB";
        }

        string format = value >= 100d ? "0" : value >= 10d ? "0.0" : "0.00";
        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {unit}";
    }

    private void RefreshScenePage()
    {
        if (!IsInstanceValid(_sceneCurrentValue) ||
            !IsInstanceValid(_sceneCurrentDetail) ||
            !IsInstanceValid(_sceneNodeCountValue) ||
            !IsInstanceValid(_sceneStateValue) ||
            !IsInstanceValid(_sceneProgressValue) ||
            !IsInstanceValid(_sceneDetailsTree))
            return;

        Node? currentScene = GetTree().CurrentScene;
        if (IsInstanceValid(currentScene))
        {
            string scenePath = string.IsNullOrEmpty(currentScene.SceneFilePath)
                ? "<运行时节点>"
                : currentScene.SceneFilePath;
            _sceneCurrentValue.Text = currentScene.Name;
            _sceneCurrentValue.TooltipText = currentScene.Name;
            _sceneCurrentDetail.Text = scenePath;
            _sceneCurrentDetail.TooltipText = scenePath;
            _sceneNodeCountValue.Text =
                GetSceneNodeCount(currentScene).ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            _sceneCurrentValue.Text = "未设置";
            _sceneCurrentValue.TooltipText = string.Empty;
            _sceneCurrentDetail.Text = "SceneTree.CurrentScene";
            _sceneCurrentDetail.TooltipText = string.Empty;
            _sceneNodeCountValue.Text = "—";
        }

        _sceneDetailsTree.Clear();
        TreeItem root = _sceneDetailsTree.CreateItem();
        if (!Services.TryGet<ISceneService>(out ISceneService? scene) || scene is null)
        {
            _sceneStateValue.Text = "未注册";
            _sceneProgressValue.Text = "—";
            AddSceneDetail(root, "服务", "SceneService 未注册");
            return;
        }

        _sceneStateValue.Text = scene.IsChanging ? "切换中" : "空闲";
        _sceneProgressValue.Text =
            $"{Mathf.RoundToInt(scene.Progress * 100f).ToString(CultureInfo.InvariantCulture)}%";
        if (scene is not SceneService sceneService)
        {
            AddSceneDetail(root, "诊断", "当前实现不支持 Debug 快照");
            return;
        }

        SceneDebugSnapshot snapshot = sceneService.GetDebugSnapshot();
        AddSceneDetail(root, "正在加载", snapshot.CurrentChangeKey?.Value ?? "—");
        AddSceneDetail(root, "最近切换", snapshot.LastChangeKey?.Value ?? "—");
        AddSceneDetail(root, "最近结果", snapshot.LastChangeKey.HasValue
            ? snapshot.LastChangeSucceeded ? "成功" : "失败"
            : "—");
    }

    private void AddSceneDetail(TreeItem root, string name, string value)
    {
        TreeItem item = _sceneDetailsTree!.CreateItem(root);
        item.SetText(0, name);
        item.SetText(1, value);
        item.SetTooltipText(1, value);
    }

    private int GetSceneNodeCount(Node currentScene)
    {
        ulong instanceId = currentScene.GetInstanceId();
        ulong now = Time.GetTicksMsec();
        if (_sceneNodeCountRootInstanceId == instanceId &&
            now - _sceneNodeCountRefreshTicks < SceneNodeCountRefreshIntervalMilliseconds)
        {
            return _sceneNodeCount;
        }

        _sceneNodeCountRootInstanceId = instanceId;
        _sceneNodeCountRefreshTicks = now;
        _sceneNodeCount = CountSceneNodes(currentScene);
        return _sceneNodeCount;
    }

    private static int CountSceneNodes(Node node)
    {
        int count = 1;
        int childCount = node.GetChildCount();
        for (int index = 0; index < childCount; index++)
            count += CountSceneNodes(node.GetChild(index));

        return count;
    }

    private void RefreshResourcesPage()
    {
        if (!IsInstanceValid(_resourcesActiveValue) ||
            !IsInstanceValid(_resourcesRequestsValue) ||
            !IsInstanceValid(_resourcesMergedValue) ||
            !IsInstanceValid(_resourcesResultValue) ||
            !IsInstanceValid(_resourcesActiveStatus) ||
            !IsInstanceValid(_resourcesActiveTree) ||
            !IsInstanceValid(_resourcesHistoryStatus) ||
            !IsInstanceValid(_resourcesHistoryTree))
            return;

        ResourceDebugSnapshot snapshot = ResourceHub.GetDebugSnapshot();
        _resourcesActiveValue.Text =
            snapshot.ActiveOperations.Length.ToString(CultureInfo.InvariantCulture);
        _resourcesRequestsValue.Text =
            $"{snapshot.SynchronousRequestCount.ToString(CultureInfo.InvariantCulture)} / " +
            snapshot.AsynchronousRequestCount.ToString(CultureInfo.InvariantCulture);
        _resourcesMergedValue.Text =
            snapshot.MergedRequestCount.ToString(CultureInfo.InvariantCulture);
        _resourcesResultValue.Text =
            $"{snapshot.SucceededRequestCount.ToString(CultureInfo.InvariantCulture)} / " +
            snapshot.FailedRequestCount.ToString(CultureInfo.InvariantCulture);

        Array.Sort(snapshot.ActiveOperations, CompareResourceOperations);
        int displayedOperationCount =
            Math.Min(snapshot.ActiveOperations.Length, MaxDisplayedResourceOperations);
        _resourcesActiveStatus.Text = snapshot.ActiveOperations.Length <= MaxDisplayedResourceOperations
            ? $"当前请求 {snapshot.ActiveOperations.Length}"
            : $"当前请求 {snapshot.ActiveOperations.Length}，显示前 {displayedOperationCount}";
        _resourcesActiveTree.Clear();
        TreeItem activeRoot = _resourcesActiveTree.CreateItem();
        for (int index = 0; index < displayedOperationCount; index++)
        {
            ResourceDebugActiveEntry entry = snapshot.ActiveOperations[index];
            TreeItem item = _resourcesActiveTree.CreateItem(activeRoot);
            item.SetText(0, entry.Key.Value);
            item.SetText(1, entry.ResourceType.Name);
            item.SetText(2, entry.Status.ToString());
            item.SetText(3,
                $"{Mathf.RoundToInt(entry.Progress * 100f).ToString(CultureInfo.InvariantCulture)}%");
            item.SetText(4, entry.MergedRequestCount.ToString(CultureInfo.InvariantCulture));
            item.SetTooltipText(0, entry.Key.Value);
            for (int column = 1; column < 5; column++)
                item.SetTextAlignment(column, HorizontalAlignment.Center);
        }

        int displayedHistoryCount = Math.Min(snapshot.History.Length, MaxDisplayedResourceHistory);
        _resourcesHistoryStatus.Text =
            $"最近请求 {displayedHistoryCount} / 保留 {snapshot.History.Length}";
        _resourcesHistoryTree.Clear();
        TreeItem historyRoot = _resourcesHistoryTree.CreateItem();
        int firstHistoryIndex = Math.Max(0, snapshot.History.Length - displayedHistoryCount);
        for (int index = snapshot.History.Length - 1; index >= firstHistoryIndex; index--)
        {
            ResourceDebugHistoryEntry entry = snapshot.History[index];
            TreeItem item = _resourcesHistoryTree.CreateItem(historyRoot);
            item.SetText(0, entry.Key.Value);
            item.SetText(1, entry.ResourceType.Name);
            item.SetText(2, entry.Mode == ResourceDebugLoadMode.Synchronous ? "同步" : "异步");
            item.SetText(3, entry.Status.ToString());
            item.SetText(4, entry.MergedRequestCount.ToString(CultureInfo.InvariantCulture));
            item.SetTooltipText(0, entry.Key.Value);
            for (int column = 1; column < 5; column++)
                item.SetTextAlignment(column, HorizontalAlignment.Center);
        }
    }

    private static int CompareResourceOperations(
        ResourceDebugActiveEntry left,
        ResourceDebugActiveEntry right) =>
        string.CompareOrdinal(left.Key.Value, right.Key.Value);

    private void RefreshDataTablePage()
    {
        if (!IsInstanceValid(_dataTableLoadedValue) ||
            !IsInstanceValid(_dataTableTablesValue) ||
            !IsInstanceValid(_dataTableLoadingValue) ||
            !IsInstanceValid(_dataTableFailedValue) ||
            !IsInstanceValid(_dataTableDataSetStatus) ||
            !IsInstanceValid(_dataTableDataSetTree) ||
            !IsInstanceValid(_dataTableHistoryStatus) ||
            !IsInstanceValid(_dataTableHistoryTree))
            return;

        if (!Services.TryGet<IDataTableService>(out IDataTableService? service) || service is null)
        {
            SetDataTableUnavailable("未注册", "DataTableService 未注册");
            return;
        }
        if (service is not DataTableService dataTableService)
        {
            SetDataTableUnavailable("不支持", "当前实现不支持 Debug 快照");
            return;
        }

        int snapshotVersion = dataTableService.DebugVersion;
        if (_dataTableSnapshotVersion == snapshotVersion)
            return;

        DataTableDebugSnapshot snapshot = dataTableService.GetDebugSnapshot();
        _dataTableLoadedValue.Text =
            snapshot.LoadedDataSetCount.ToString(CultureInfo.InvariantCulture);
        _dataTableTablesValue.Text =
            snapshot.CachedTableCount.ToString(CultureInfo.InvariantCulture);
        _dataTableLoadingValue.Text =
            snapshot.LoadingDataSetCount.ToString(CultureInfo.InvariantCulture);
        _dataTableFailedValue.Text =
            snapshot.FailedLoadCount.ToString(CultureInfo.InvariantCulture);

        int displayedDataSetCount =
            Math.Min(snapshot.DataSets.Length, MaxDisplayedDataTableDataSets);
        _dataTableDataSetStatus.Text =
            snapshot.DataSets.Length <= MaxDisplayedDataTableDataSets
                ? $"当前数据集 {snapshot.DataSets.Length}"
                : $"当前数据集 {snapshot.DataSets.Length}，显示前 {displayedDataSetCount}";
        _dataTableDataSetTree.Clear();
        TreeItem dataSetRoot = _dataTableDataSetTree.CreateItem();
        int remainingDisplayedTables = MaxDisplayedDataTableTables;
        for (int index = 0; index < displayedDataSetCount; index++)
        {
            DataTableDebugDataSetEntry entry = snapshot.DataSets[index];
            TreeItem dataSetItem = _dataTableDataSetTree.CreateItem(dataSetRoot);
            dataSetItem.SetText(0, entry.DataSetId);
            dataSetItem.SetText(1, GetDataTableStateText(entry.State));
            dataSetItem.SetText(
                2,
                $"{entry.LoadedTableCount.ToString(CultureInfo.InvariantCulture)} / " +
                entry.TotalTableCount.ToString(CultureInfo.InvariantCulture));
            dataSetItem.SetText(3, FormatDataTableProgress(entry));
            string dataSetDetail =
                entry.State == DataTableDebugState.Loading && entry.LastTableId is not null
                    ? $"{entry.RuntimeDirectory} · 最近 {entry.LastTableId}"
                    : entry.RuntimeDirectory;
            dataSetItem.SetText(4, dataSetDetail);
            dataSetItem.SetCustomColor(1, GetDataTableStateColor(entry.State));
            dataSetItem.SetTooltipText(0, entry.DataSetId);
            dataSetItem.SetTooltipText(4, entry.RuntimeDirectory);
            for (int column = 1; column < 4; column++)
                dataSetItem.SetTextAlignment(column, HorizontalAlignment.Center);

            int displayedTableCount = Math.Min(entry.Tables.Length, remainingDisplayedTables);
            for (int tableIndex = 0; tableIndex < displayedTableCount; tableIndex++)
            {
                DataTableDebugTableEntry table = entry.Tables[tableIndex];
                TreeItem tableItem = _dataTableDataSetTree.CreateItem(dataSetItem);
                tableItem.SetText(0, table.TableId);
                tableItem.SetText(1, table.TableType.Name);
                tableItem.SetText(2, "1");
                tableItem.SetText(3, "—");
                tableItem.SetText(4, "已缓存");
                tableItem.SetTooltipText(0, table.TableId);
                tableItem.SetTooltipText(1, table.TableType.FullName ?? table.TableType.Name);
                for (int column = 1; column < 4; column++)
                    tableItem.SetTextAlignment(column, HorizontalAlignment.Center);
            }
            remainingDisplayedTables -= displayedTableCount;
            if (displayedTableCount < entry.Tables.Length)
            {
                TreeItem omittedItem = _dataTableDataSetTree.CreateItem(dataSetItem);
                omittedItem.SetText(
                    0,
                    $"…还有 {(entry.Tables.Length - displayedTableCount).ToString(CultureInfo.InvariantCulture)} 张表");
                omittedItem.SetSelectable(0, false);
            }
        }

        int displayedHistoryCount =
            Math.Min(snapshot.History.Length, MaxDisplayedDataTableHistory);
        _dataTableHistoryStatus.Text =
            $"最近结果 {displayedHistoryCount} / 保留 {snapshot.History.Length}";
        _dataTableHistoryTree.Clear();
        TreeItem historyRoot = _dataTableHistoryTree.CreateItem();
        int firstHistoryIndex = Math.Max(0, snapshot.History.Length - displayedHistoryCount);
        for (int index = snapshot.History.Length - 1; index >= firstHistoryIndex; index--)
        {
            DataTableDebugHistoryEntry entry = snapshot.History[index];
            TreeItem item = _dataTableHistoryTree.CreateItem(historyRoot);
            item.SetText(0, entry.DataSetId);
            item.SetText(1, GetDataTableStateText(entry.State));
            item.SetText(2, entry.TableCount.ToString(CultureInfo.InvariantCulture));
            item.SetText(3, entry.Detail);
            item.SetCustomColor(1, GetDataTableStateColor(entry.State));
            item.SetTooltipText(0, entry.DataSetId);
            item.SetTooltipText(3, entry.Detail);
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTextAlignment(2, HorizontalAlignment.Center);
        }
        _dataTableSnapshotVersion = snapshotVersion;
    }

    private void SetDataTableUnavailable(string state, string detail)
    {
        _dataTableLoadedValue!.Text = state;
        _dataTableTablesValue!.Text = "—";
        _dataTableLoadingValue!.Text = "—";
        _dataTableFailedValue!.Text = "—";
        _dataTableDataSetStatus!.Text = detail;
        _dataTableHistoryStatus!.Text = "最近结果 0 / 保留 0";
        _dataTableDataSetTree!.Clear();
        _dataTableHistoryTree!.Clear();
        _dataTableSnapshotVersion = int.MinValue;
    }

    private static string FormatDataTableProgress(DataTableDebugDataSetEntry entry)
    {
        if (entry.State == DataTableDebugState.Loaded)
            return "100%";
        if (entry.TotalTableCount <= 0)
            return "0%";
        int percentage = Mathf.RoundToInt(
            (float)entry.LoadedTableCount / entry.TotalTableCount * 100f);
        return $"{percentage.ToString(CultureInfo.InvariantCulture)}%";
    }

    private static string GetDataTableStateText(DataTableDebugState state) => state switch
    {
        DataTableDebugState.Loading => "加载中",
        DataTableDebugState.Loaded => "已加载",
        DataTableDebugState.Failed => "失败",
        DataTableDebugState.Canceled => "已取消",
        DataTableDebugState.Unloaded => "已卸载",
        _ => state.ToString(),
    };

    private static Color GetDataTableStateColor(DataTableDebugState state) => state switch
    {
        DataTableDebugState.Loading => new Color(0.95f, 0.75f, 0.32f),
        DataTableDebugState.Loaded => new Color(0.49f, 0.76f, 1f),
        DataTableDebugState.Failed => new Color(1f, 0.38f, 0.38f),
        DataTableDebugState.Canceled => new Color(0.72f, 0.66f, 0.52f),
        DataTableDebugState.Unloaded => new Color(0.5f, 0.57f, 0.66f),
        _ => new Color(0.72f, 0.78f, 0.85f),
    };

    private void RefreshUiPage()
    {
        if (!IsInstanceValid(_uiSceneValue) ||
            !IsInstanceValid(_uiViewValue) ||
            !IsInstanceValid(_uiModalValue) ||
            !IsInstanceValid(_uiOverlayValue) ||
            !IsInstanceValid(_uiCurrentValue) ||
            !IsInstanceValid(_uiCurrentDetail) ||
            !IsInstanceValid(_uiStackStatus) ||
            !IsInstanceValid(_uiStackTree))
            return;

        if (!Services.TryGet<IUiService>(out IUiService? service) || service is null)
        {
            SetUiUnavailable("未注册", "UiService 未注册");
            return;
        }
        if (service is not UiService uiService)
        {
            SetUiUnavailable("不支持", "当前实现不支持 Debug 快照");
            return;
        }

        UiDebugSnapshot snapshot = uiService.GetDebugSnapshot();
        int sceneCount = 0;
        int viewCount = 0;
        int modalCount = 0;
        int overlayCount = 0;
        int cachedCount = 0;
        int invalidCount = 0;
        int openingRequestCount = 0;
        UiDebugEntry? current = null;
        for (int index = 0; index < snapshot.Openings.Length; index++)
            openingRequestCount += snapshot.Openings[index].RequestCount;

        for (int index = 0; index < snapshot.Entries.Length; index++)
        {
            UiDebugEntry entry = snapshot.Entries[index];
            if (entry.IsCached)
            {
                cachedCount++;
                continue;
            }

            switch (entry.Layer)
            {
                case UiLayer.Scene:
                    sceneCount++;
                    break;
                case UiLayer.View:
                    viewCount++;
                    current = entry;
                    break;
                case UiLayer.Modal:
                    modalCount++;
                    current = entry;
                    break;
                case UiLayer.Overlay:
                    overlayCount++;
                    current = entry;
                    break;
            }

            if (!entry.IsValid)
                invalidCount++;
        }

        _uiSceneValue.Text = sceneCount.ToString(CultureInfo.InvariantCulture);
        _uiViewValue.Text = viewCount.ToString(CultureInfo.InvariantCulture);
        _uiModalValue.Text = modalCount.ToString(CultureInfo.InvariantCulture);
        _uiOverlayValue.Text = overlayCount.ToString(CultureInfo.InvariantCulture);
        _uiCurrentValue.Text = current?.NodeName ?? "空闲";
        _uiCurrentDetail.Text = current?.Key.Value ?? "无 View / Modal / Overlay";
        _uiCurrentValue.TooltipText = current?.NodeName ?? string.Empty;
        _uiCurrentDetail.TooltipText = current?.Key.Value ?? string.Empty;
        int displayedOpeningCount = Math.Min(snapshot.Openings.Length, MaxDisplayedUiEntries);
        int displayedEntryCount = Math.Min(
            snapshot.Entries.Length,
            MaxDisplayedUiEntries - displayedOpeningCount);
        int totalRowCount = snapshot.Openings.Length + snapshot.Entries.Length;
        int displayedRowCount = displayedOpeningCount + displayedEntryCount;
        string displayDetail = totalRowCount <= MaxDisplayedUiEntries
            ? string.Empty
            : $" · 显示 {displayedRowCount}";
        int openCount = snapshot.Entries.Length - cachedCount;
        string status = $"受管理界面 {openCount}";
        if (openingRequestCount > 0)
            status += $" · 打开中 {openingRequestCount}";
        status += $" · 缓存 {cachedCount}";
        if (invalidCount > 0)
            status += $" · 异常 {invalidCount}";
        _uiStackStatus.Text = status + displayDetail;

        _uiStackTree.Clear();
        TreeItem root = _uiStackTree.CreateItem();
        for (int index = 0; index < displayedOpeningCount; index++)
        {
            UiDebugOpeningEntry opening = snapshot.Openings[index];
            TreeItem item = _uiStackTree.CreateItem(root);
            item.SetText(0, opening.Layer.ToString());
            item.SetText(1, "—");
            item.SetText(2, opening.Id.IsValid ? opening.Id.Value : "Direct");
            item.SetText(3, opening.Key.Value);
            item.SetText(
                4,
                opening.RequestCount == 1
                    ? "加载中"
                    : $"加载中 ×{opening.RequestCount.ToString(CultureInfo.InvariantCulture)}");
            item.SetTooltipText(
                2,
                opening.Id.IsValid
                    ? opening.Id.Value
                    : "通过 ResourceKey 直接打开");
            item.SetTooltipText(3, opening.Key.Value);
            item.SetTooltipText(4, "异步打开请求尚未完成");
            item.SetTextAlignment(0, HorizontalAlignment.Center);
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTextAlignment(4, HorizontalAlignment.Center);
            item.SetCustomColor(4, new Color(0.96f, 0.75f, 0.32f));
        }

        int firstDisplayedIndex = Math.Max(0, snapshot.Entries.Length - displayedEntryCount);
        for (int index = snapshot.Entries.Length - 1; index >= firstDisplayedIndex; index--)
        {
            UiDebugEntry entry = snapshot.Entries[index];
            TreeItem item = _uiStackTree.CreateItem(root);
            item.SetText(0, entry.Layer.ToString());
            item.SetText(1, GetUiOrderText(entry));
            item.SetText(
                2,
                entry.Id.IsValid
                    ? $"{entry.Id.Value} · {entry.NodeName}"
                    : entry.NodeName);
            item.SetText(3, entry.Key.Value);
            item.SetText(4, GetUiStateText(entry));
            item.SetTooltipText(2, entry.NodeName);
            item.SetTooltipText(3, entry.Key.Value);
            item.SetTooltipText(
                4,
                entry.HasFocus
                    ? $"焦点控件：{entry.FocusNodeName}"
                    : item.GetText(4));
            item.SetTextAlignment(0, HorizontalAlignment.Center);
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTextAlignment(4, HorizontalAlignment.Center);
            if (!entry.IsValid)
                item.SetCustomColor(4, new Color(1f, 0.38f, 0.38f));
            else if (entry.IsCached)
                item.SetCustomColor(4, new Color(0.48f, 0.72f, 0.92f));
            else if (entry.HasFocus)
                item.SetCustomColor(4, new Color(0.45f, 0.88f, 0.62f));
        }
    }

    private void SetUiUnavailable(string state, string detail)
    {
        _uiSceneValue!.Text = state;
        _uiViewValue!.Text = "—";
        _uiModalValue!.Text = "—";
        _uiOverlayValue!.Text = "—";
        _uiCurrentValue!.Text = "—";
        _uiCurrentDetail!.Text = detail;
        _uiStackStatus!.Text = detail;
        _uiStackTree!.Clear();
    }

    private static string GetUiOrderText(UiDebugEntry entry) =>
        entry.IsCached
            ? "—"
            : entry.Layer == UiLayer.Scene
            ? (entry.Index + 1).ToString(CultureInfo.InvariantCulture)
            : $"#{(entry.Index + 1).ToString(CultureInfo.InvariantCulture)}";

    private static string GetUiStateText(UiDebugEntry entry)
    {
        if (!entry.IsValid)
            return "已失效";

        if (entry.IsCached)
            return "缓存";

        if (entry.HasFocus)
            return entry.IsVisible ? "显示 · 焦点" : "隐藏 · 焦点";

        return entry.IsVisible ? "显示" : "隐藏";
    }

    private void RefreshProcedurePage()
    {
        if (!IsInstanceValid(_procedureCurrentValue) ||
            !IsInstanceValid(_procedureStateValue) ||
            !IsInstanceValid(_procedurePendingValue) ||
            !IsInstanceValid(_procedureResultValue) ||
            !IsInstanceValid(_procedureDetailsTree))
            return;

        if (!Services.TryGet<IProcedureService>(out IProcedureService? service) || service is null)
        {
            SetProcedureUnavailable("未注册", "ProcedureService 未注册");
            return;
        }
        if (service is not ProcedureService procedureService)
        {
            SetProcedureUnavailable("不支持", "当前实现不支持 Debug 快照");
            return;
        }

        ProcedureDebugSnapshot snapshot = procedureService.GetDebugSnapshot();
        _procedureCurrentValue.Text = snapshot.CurrentName ?? "空闲";
        _procedureStateValue.Text = snapshot.Phase switch
        {
            ProcedureDebugPhase.Exiting => "退出中",
            ProcedureDebugPhase.Entering => "进入中",
            _ => service.IsChanging ? "切换中" : "空闲",
        };
        _procedurePendingValue.Text = snapshot.PendingName ?? "无";
        _procedureResultValue.Text = snapshot.LastFailure is null ? "无" : "有";
        _procedureDetailsTree.Clear();
        TreeItem root = _procedureDetailsTree.CreateItem();
        AddProcedureDetail(root, "上一个流程", snapshot.PreviousName ?? "—");
        AddProcedureDetail(root, "切换目标", snapshot.TargetName ?? "—");
        AddProcedureDetail(root, "最近成功", snapshot.LastSucceededName ?? "—");
        AddProcedureDetail(root, "最近失败", snapshot.LastFailure ?? "—");
    }

    private void SetProcedureUnavailable(string state, string detail)
    {
        _procedureCurrentValue!.Text = state;
        _procedureStateValue!.Text = "—";
        _procedurePendingValue!.Text = "—";
        _procedureResultValue!.Text = "—";
        _procedureDetailsTree!.Clear();
        TreeItem root = _procedureDetailsTree.CreateItem();
        AddProcedureDetail(root, "诊断", detail);
    }

    private void AddProcedureDetail(TreeItem root, string name, string value)
    {
        TreeItem item = _procedureDetailsTree!.CreateItem(root);
        item.SetText(0, name);
        item.SetText(1, value);
        item.SetTooltipText(1, value);
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

    private void AppendConsole()
    {
        ConsoleRenderCount++;
        LogEntry[] logs = LogHub.GetDebugSnapshot();
        int errorCount = 0;
        foreach (DebuggerErrorEntry error in _recentWarnings)
            _consoleErrorSnapshot[errorCount++] = error;
        FileLogDebugSnapshot fileLog = LogHub.GetFileLogDebugSnapshot();
        UpdateConsoleFilterLabels(logs);
        AppendConsoleEntries(logs, _consoleErrorSnapshot, errorCount);
        UpdateConsoleFileLogStatus(fileLog);
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

    private void AppendConsoleEntries(
        LogEntry[] logs,
        DebuggerErrorEntry[] errors,
        int errorCount)
    {
        int matchingCount = 0;
        for (int index = 0; index < logs.Length; index++)
        {
            if (MatchesConsoleLevel(logs[index]) &&
                MatchesConsoleSearch(logs[index]))
                matchingCount++;
        }
        for (int index = 0; index < errorCount; index++)
        {
            if (MatchesConsoleLevel(errors[index]) &&
                MatchesConsoleSearch(errors[index]))
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
        int logIndex = 0;
        int errorIndex = 0;
        while (logIndex < logs.Length || errorIndex < errorCount)
        {
            bool useLog = errorIndex >= errorCount ||
                (logIndex < logs.Length &&
                 logs[logIndex].TimestampUtc <= errors[errorIndex].TimestampUtc);
            if (useLog)
            {
                LogEntry log = logs[logIndex++];
                if (!MatchesConsoleLevel(log) || !MatchesConsoleSearch(log))
                    continue;
                if (matchingIndex >= pageStart && matchingIndex < pageEnd)
                    AppendConsoleLog(log);
                matchingIndex++;
            }
            else
            {
                DebuggerErrorEntry error = errors[errorIndex++];
                if (!MatchesConsoleLevel(error) || !MatchesConsoleSearch(error))
                    continue;
                if (matchingIndex >= pageStart && matchingIndex < pageEnd)
                    AppendConsoleError(error);
                matchingIndex++;
            }

            if (matchingIndex >= pageEnd)
                break;
        }
    }

    private void AppendConsoleLog(LogEntry log)
    {
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

    private void AppendConsoleError(DebuggerErrorEntry error)
    {
        _textBuilder.Append(error.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"))
            .Append(' ').Append('[').Append(error.Level).Append("] ")
            .Append(error.Module).Append(": ").AppendLine(error.Message);
    }

    private string BuildConsoleMarkup(string text)
    {
        _consoleMarkupBuilder.Clear();
        int lineStart = 0;
        while (lineStart < text.Length)
        {
            int newline = text.IndexOf('\n', lineStart);
            int lineEnd = newline >= 0 ? newline : text.Length;
            ReadOnlySpan<char> line = text.AsSpan(lineStart, lineEnd - lineStart);
            string? color = GetConsoleLineColor(line);
            if (color != null)
                _consoleMarkupBuilder.Append("[color=").Append(color).Append(']');

            AppendEscapedBbcode(_consoleMarkupBuilder, line);
            if (color != null)
                _consoleMarkupBuilder.Append("[/color]");
            if (newline < 0)
                break;

            _consoleMarkupBuilder.Append('\n');
            lineStart = newline + 1;
        }

        return _consoleMarkupBuilder.ToString();
    }

    private static string? GetConsoleLineColor(ReadOnlySpan<char> line)
    {
        if (line.Length <= 9 || line[8] != ' ')
            return null;

        ReadOnlySpan<char> level = line[9..];
        if (level.StartsWith("[Warning]"))
            return ConsoleWarningColor;
        if (level.StartsWith("[Error]") || level.StartsWith("[Fatal]"))
            return ConsoleErrorColor;
        return null;
    }

    private static void AppendEscapedBbcode(
        StringBuilder builder,
        ReadOnlySpan<char> text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '[')
                builder.Append("[lb]");
            else
                builder.Append(text[index]);
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

    private void UpdateConsoleFileLogStatus(FileLogDebugSnapshot snapshot)
    {
        if (!IsInstanceValid(_consoleFileLink))
            return;

        if (!snapshot.IsEnabled)
        {
            _consoleFilePath = string.Empty;
            _consoleFileLink.Text = "文件日志未启用";
            _consoleFileLink.TooltipText = string.Empty;
            _consoleFileLink.Disabled = true;
            return;
        }

        _consoleFilePath = snapshot.Path;
        string status = snapshot.HasFailed
            ? "已停用"
            : snapshot.IsReady ? "正常" : "启动中";
        string fileName = System.IO.Path.GetFileName(snapshot.Path);
        _consoleFileLink.Text = $"{fileName} ({status})";
        _consoleFileLink.Disabled = !snapshot.IsReady;
        _consoleFileLink.TooltipText =
            $"{snapshot.Path}\n" +
            $"状态：{status} · 已刷新：{FormatBytes(snapshot.CurrentFileBytes)} · " +
            $"丢弃：{snapshot.DroppedLineCount.ToString(CultureInfo.InvariantCulture)}" +
            (snapshot.HasFailed
                ? $"\n原因：{snapshot.FailureDetail ?? "未知写入错误"}"
                : "\n点击在文件管理器中定位");
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
        public bool IsSystem => string.Equals(Path, "System", StringComparison.Ordinal);
        public bool IsPerformance => string.Equals(Path, "Performance", StringComparison.Ordinal);
        public bool IsInput => string.Equals(Path, "Runtime/Input", StringComparison.Ordinal);
        public bool IsScheduler => string.Equals(Path, "Runtime/Scheduler", StringComparison.Ordinal);
        public bool IsAudio => string.Equals(Path, "Runtime/Audio", StringComparison.Ordinal);
        public bool IsScene => string.Equals(Path, "Runtime/Scene", StringComparison.Ordinal);
        public bool IsResources => string.Equals(Path, "Runtime/Resources", StringComparison.Ordinal);
        public bool IsDataTable => string.Equals(Path, "Runtime/DataTable", StringComparison.Ordinal);
        public bool IsUi => string.Equals(Path, "Runtime/UI", StringComparison.Ordinal);
        public bool IsProcedure => string.Equals(Path, "Runtime/Procedure", StringComparison.Ordinal);
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
        public DateTime TimestampUtc { get; }
        public ErrorLevel Level { get; }
        public string Module { get; }
        public string Message { get; }

        public DebuggerErrorEntry(
            DateTime timestampUtc,
            ErrorLevel level,
            string module,
            string message)
        {
            TimestampUtc = timestampUtc;
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
