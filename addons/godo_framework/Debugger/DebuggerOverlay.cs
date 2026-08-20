using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>由 GoDoRuntime 在 Debug 构建中自动创建的紧凑只读框架状态面板。</summary>
/// <remarks>
/// 该节点不是业务 Service，也不提供运行时修改能力。业务代码不应自行实例化或依赖它；
/// GoDoRuntime 在 Release 构建中不会创建该节点，手动实例化的 Release 版本也会立即排队释放。
/// </remarks>
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
    private bool _integrationPageContentVisible;
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
    private Label? _schedulerActiveTasksStatus;
    private Tree? _schedulerActiveTasksTree;
    private Label? _schedulerRecentResultsStatus;
    private Tree? _schedulerRecentResultsTree;
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
    private Control? _poolDashboard;
    private Label? _poolRegisteredValue;
    private Label? _poolIdleValue;
    private Label? _poolActiveValue;
    private Label? _poolStatus;
    private Tree? _poolTree;
    private Label? _poolActiveRentalsStatus;
    private Tree? _poolActiveRentalsTree;
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
    private Label? _procedureCurrentTitle;
    private Label? _procedureStateTitle;
    private Label? _procedurePendingTitle;
    private Label? _procedureResultTitle;
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
    private Label? _eventsListenerSourcesStatus;
    private Tree? _eventsListenerSourcesTree;
    private string _selectedEventTypeName = string.Empty;
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
    /// <summary>NodePool 仪表盘根节点路径。</summary>
    [Export] public NodePath PoolDashboardPath { get; set; } = null!;
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

    /// <summary>订阅 ErrorHub 的错误摘要事件。</summary>
    public override void _EnterTree()
    {
        ErrorHub.OnError += OnErrorReported;
    }

    /// <summary>验证场景引用、缓存控件并初始化只读页面与交互。</summary>
    /// <exception cref="InvalidOperationException">Debugger 场景缺少必要的导出节点引用。</exception>
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
        _poolDashboard = GetNodeOrNull<Control>(PoolDashboardPath);
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
            !IsInstanceValid(_poolDashboard) ||
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
        CachePoolNodes();
        CacheDataTableNodes();
        CacheUiNodes();
        CacheProcedureNodes();
        CacheServicesNodes();
        CacheEventsNodes();
        CacheConsoleFilterNodes();
        CacheIntegrationPages();
        RegisterPages();
        ConfigureNavigationTree();
        _debuggerLabel.FocusMode = Control.FocusModeEnum.None;
        _toggleButton.Pressed += OnTogglePressed;
        _resetLayoutButton.Pressed += OnResetLayoutPressed;
        _overviewWarningButton!.Pressed += OnOverviewWarningPressed;
        _overviewErrorButton!.Pressed += OnOverviewErrorPressed;
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

    /// <summary>按固定低频间隔刷新健康状态和当前展开页面。</summary>
    /// <param name="delta">Godot 提供的当前普通帧间隔秒数。</param>
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

    /// <summary>解除 ErrorHub、控件和输入事件订阅并停止诊断采样。</summary>
    public override void _ExitTree()
    {
        ErrorHub.OnError -= OnErrorReported;
        if (IsInstanceValid(_toggleButton))
            _toggleButton.Pressed -= OnTogglePressed;
        if (IsInstanceValid(_resetLayoutButton))
            _resetLayoutButton.Pressed -= OnResetLayoutPressed;
        if (IsInstanceValid(_overviewWarningButton))
            _overviewWarningButton.Pressed -= OnOverviewWarningPressed;
        if (IsInstanceValid(_overviewErrorButton))
            _overviewErrorButton.Pressed -= OnOverviewErrorPressed;
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
        _overviewWarningButton = null;
        _overviewErrorButton = null;
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
        _schedulerActiveTasksStatus = null;
        _schedulerActiveTasksTree = null;
        _schedulerRecentResultsStatus = null;
        _schedulerRecentResultsTree = null;
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
        _poolDashboard = null;
        _poolRegisteredValue = null;
        _poolIdleValue = null;
        _poolActiveValue = null;
        _poolStatus = null;
        _poolTree = null;
        _poolActiveRentalsStatus = null;
        _poolActiveRentalsTree = null;
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
        _eventsListenerSourcesStatus = null;
        _eventsListenerSourcesTree = null;
        _selectedEventTypeName = string.Empty;
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
        RegisterPage("Runtime/Pool", "运行时", "Pool", RefreshPoolPage);
        RegisterIntegrationPages();
        RegisterPage("Runtime/DataTable", "运行时", "DataTable", RefreshDataTablePage);
        RegisterPage("Runtime/UI", "运行时", "UI", RefreshUiPage);
        RegisterPage("Runtime/Procedure", "运行时", "Procedure", RefreshProcedurePage);
        RegisterPage("ExecutionFlow", "运行链路", "运行链路", RefreshExecutionFlowPage);
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

    private void SelectPageByPath(string path, bool forceRefresh)
    {
        foreach ((TreeItem item, DebuggerPage page) in _pagesByTreeItem)
        {
            if (!string.Equals(page.Path, path, StringComparison.Ordinal))
                continue;

            bool wasSelected = ReferenceEquals(_selectedPage, page);
            item.Select(0);
            if (!ReferenceEquals(_selectedPage, page))
                SelectPage(page, forceRefresh);
            else if (wasSelected && forceRefresh)
                RefreshDebugger(force: true);
            return;
        }

        throw new InvalidOperationException($"Debugger 缺少页面：{path}");
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
        if (IsInstanceValid(_poolDashboard))
            _poolDashboard.Visible = page.IsPool && !showReadFailure;
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
        _integrationPageContentVisible = false;
        ApplyIntegrationPageContentVisibility(page, showReadFailure);
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
                !page.IsPool &&
                !page.IsDataTable &&
                !page.IsUi &&
                !page.IsProcedure &&
                !page.IsServices &&
                !page.IsEvents &&
                !_integrationPageContentVisible;
        }
    }

    partial void CacheIntegrationPages();

    partial void RegisterIntegrationPages();

    partial void ApplyIntegrationPageContentVisibility(DebuggerPage page, bool showReadFailure);


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
        public bool IsPool => string.Equals(Path, "Runtime/Pool", StringComparison.Ordinal);
        public bool IsDataTable => string.Equals(Path, "Runtime/DataTable", StringComparison.Ordinal);
        public bool IsUi => string.Equals(Path, "Runtime/UI", StringComparison.Ordinal);
        public bool IsProcedure =>
            string.Equals(Path, "Runtime/Procedure", StringComparison.Ordinal) ||
            string.Equals(Path, "ExecutionFlow", StringComparison.Ordinal);
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
        public string? Context { get; }
        public string? Cause { get; }

        public DebuggerErrorEntry(
            DateTime timestampUtc,
            ErrorLevel level,
            string module,
            string message,
            string? context,
            string? cause)
        {
            TimestampUtc = timestampUtc;
            Level = level;
            Module = module;
            Message = message;
            Context = context;
            Cause = cause;
        }
    }
#else
    /// <summary>Release 构建不提供运行时 Debugger，因此节点就绪后立即排队释放。</summary>
    public override void _Ready()
    {
        QueueFree();
    }
#endif
}
