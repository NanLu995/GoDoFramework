using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>验证 Debugger 折叠状态、树状导航、布局与控制台交互。</summary>
public sealed partial class DebuggerOverlayRegression : Node
{
    /// <inheritdoc />
#if DEBUG
    public override async void _Ready()
#else
    public override void _Ready()
#endif
    {
        try
        {
#if DEBUG
            DebuggerOverlay overlay = GetNode<DebuggerOverlay>("/root/GoDoRuntime/GoDoDebugger");
            PanelContainer panel = overlay.GetNode<PanelContainer>("Panel");
            Button toggle = overlay.GetNode<Button>("Panel/Margin/VBox/Header/FpsButton");
            Label title = overlay.GetNode<Label>("Panel/Margin/VBox/Header/TitleLabel");
            Button reset = overlay.GetNode<Button>("Panel/Margin/VBox/Header/ResetLayoutButton");
            HSplitContainer body = overlay.GetNode<HSplitContainer>("Panel/Margin/VBox/Body");
            Tree navigation = overlay.GetNode<Tree>("Panel/Margin/VBox/Body/Navigation");
            HBoxContainer consoleToolbar =
                overlay.GetNode<HBoxContainer>("Panel/Margin/VBox/Body/Page/ConsoleToolbar");
            HBoxContainer consoleFilters =
                overlay.GetNode<HBoxContainer>("Panel/Margin/VBox/Body/Page/ConsoleFilters");
            HBoxContainer consolePagination =
                overlay.GetNode<HBoxContainer>("Panel/Margin/VBox/Body/Page/ConsolePagination");
            Button allFilter = consoleFilters.GetNode<Button>("All");
            Button debugFilter = consoleFilters.GetNode<Button>("Debug");
            Button infoFilter = consoleFilters.GetNode<Button>("Info");
            Button warningFilter = consoleFilters.GetNode<Button>("Warning");
            Button errorFilter = consoleFilters.GetNode<Button>("Error");
            Label pageStatus = consolePagination.GetNode<Label>("Status");
            Button olderPage = consolePagination.GetNode<Button>("Older");
            Button newerPage = consolePagination.GetNode<Button>("Newer");
            Button latestPage = consolePagination.GetNode<Button>("Latest");
            ScrollContainer overviewDashboard =
                overlay.GetNode<ScrollContainer>("Panel/Margin/VBox/Body/Page/OverviewDashboard");
            Label overviewFps = overviewDashboard.GetNode<Label>("Content/StatusGrid/FpsCard/Content/Value");
            Label overviewWarnings =
                overviewDashboard.GetNode<Label>("Content/StatusGrid/WarningCard/Content/Value");
            Label overviewErrors =
                overviewDashboard.GetNode<Label>("Content/StatusGrid/ErrorCard/Content/Value");
            Label overviewServices =
                overviewDashboard.GetNode<Label>("Content/MetricGrid/ServicesCard/Content/Value");
            Label overviewEventsDetail =
                overviewDashboard.GetNode<Label>("Content/MetricGrid/EventsCard/Content/Detail");
            Label overviewScene =
                overviewDashboard.GetNode<Label>("Content/MetricGrid/SceneCard/Content/Value");
            Label overviewAudio =
                overviewDashboard.GetNode<Label>("Content/ActivityGrid/AudioCard/Content/Value");
            Label overviewInput =
                overviewDashboard.GetNode<Label>("Content/ActivityGrid/InputCard/Content/Value");
            Label overviewScheduler =
                overviewDashboard.GetNode<Label>("Content/ActivityGrid/SchedulerCard/Content/Value");
            VBoxContainer systemDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/SystemDashboard");
            Label systemPlatform =
                systemDashboard.GetNode<Label>("Summary/PlatformCard/Content/Value");
            Label systemBuild =
                systemDashboard.GetNode<Label>("Summary/BuildCard/Content/Value");
            Label systemRenderer =
                systemDashboard.GetNode<Label>("Summary/RendererCard/Content/Value");
            Label systemUptime =
                systemDashboard.GetNode<Label>("Summary/UptimeCard/Content/Value");
            Tree systemDetails = systemDashboard.GetNode<Tree>("Details");
            VBoxContainer performanceDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/PerformanceDashboard");
            Label performanceFps =
                performanceDashboard.GetNode<Label>("Content/Summary/FpsCard/Content/Value");
            Label performanceProcess =
                performanceDashboard.GetNode<Label>("Content/Summary/ProcessCard/Content/Value");
            Label performancePhysics =
                performanceDashboard.GetNode<Label>("Content/Summary/PhysicsCard/Content/Value");
            Label performanceMemory =
                performanceDashboard.GetNode<Label>("Content/Summary/MemoryCard/Content/Value");
            Label performanceManagedMemory =
                performanceDashboard.GetNode<Label>("Content/Summary/ManagedMemoryCard/Content/Value");
            HBoxContainer performanceFrameLegend =
                performanceDashboard.GetNode<HBoxContainer>("Content/Trends/FramePanel/Content/Legend");
            HBoxContainer performanceMemoryLegend =
                performanceDashboard.GetNode<HBoxContainer>("Content/Trends/MemoryPanel/Content/Legend");
            ColorRect performanceProcessColor =
                performanceFrameLegend.GetNode<ColorRect>("ProcessColor");
            ColorRect performancePhysicsColor =
                performanceFrameLegend.GetNode<ColorRect>("PhysicsColor");
            ColorRect performanceEngineColor =
                performanceMemoryLegend.GetNode<ColorRect>("EngineColor");
            ColorRect performanceManagedColor =
                performanceMemoryLegend.GetNode<ColorRect>("ManagedColor");
            Label performanceEngineLegend =
                performanceMemoryLegend.GetNode<Label>("EngineLabel");
            Label performanceManagedLegend =
                performanceMemoryLegend.GetNode<Label>("ManagedLabel");
            Control performanceFrameGraph =
                performanceDashboard.GetNode<Control>("Content/Trends/FramePanel/Content/Graph");
            Control performanceMemoryGraph =
                performanceDashboard.GetNode<Control>("Content/Trends/MemoryPanel/Content/Graph");
            Tree performanceMetrics =
                performanceDashboard.GetNode<Tree>("Content/Metrics");
            VBoxContainer inputDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/InputDashboard");
            Label inputBackend =
                inputDashboard.GetNode<Label>("StatusGrid/BackendCard/Content/Value");
            Label inputFrame =
                inputDashboard.GetNode<Label>("StatusGrid/FrameCard/Content/Value");
            Label inputActions =
                inputDashboard.GetNode<Label>("StatusGrid/ActionsCard/Content/Value");
            Tree inputContextsTree = inputDashboard.GetNode<Tree>("ContextList");
            Label inputActionHeader = inputDashboard.GetNode<Label>("ActionHeader");
            LineEdit inputActionsSearch = inputDashboard.GetNode<LineEdit>("ActionSearch");
            Label inputActionsMatchStatus = inputDashboard.GetNode<Label>("ActionMatchStatus");
            Tree inputActionsTree = inputDashboard.GetNode<Tree>("ActionList");
            ScrollContainer schedulerDashboard =
                overlay.GetNode<ScrollContainer>("Panel/Margin/VBox/Body/Page/SchedulerDashboard");
            ScrollContainer audioDashboard =
                overlay.GetNode<ScrollContainer>("Panel/Margin/VBox/Body/Page/AudioDashboard");
            Label audioBgmState =
                audioDashboard.GetNode<Label>("Content/PlaybackGrid/BgmStateCard/Content/Value");
            Label audioBgmResource =
                audioDashboard.GetNode<Label>("Content/BgmCard/Content/Value");
            Label audioSfx =
                audioDashboard.GetNode<Label>("Content/PlaybackGrid/SfxCard/Content/Value");
            Label audioMasterVolume =
                audioDashboard.GetNode<Label>("Content/VolumeGrid/MasterCard/Content/Value");
            Label audioBgmVolume =
                audioDashboard.GetNode<Label>("Content/VolumeGrid/BgmCard/Content/Value");
            Label audioSfxVolume =
                audioDashboard.GetNode<Label>("Content/VolumeGrid/SfxCard/Content/Value");
            VBoxContainer sceneDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/SceneDashboard");
            Label sceneCurrent =
                sceneDashboard.GetNode<Label>("Summary/CurrentCard/Content/Value");
            Label sceneNodes =
                sceneDashboard.GetNode<Label>("Summary/NodesCard/Content/Value");
            Label sceneState =
                sceneDashboard.GetNode<Label>("Summary/StateCard/Content/Value");
            Label sceneProgress =
                sceneDashboard.GetNode<Label>("Summary/ProgressCard/Content/Value");
            Tree sceneDetails = sceneDashboard.GetNode<Tree>("Details");
            VBoxContainer resourcesDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/ResourcesDashboard");
            Label resourcesActive =
                resourcesDashboard.GetNode<Label>("Summary/ActiveCard/Content/Value");
            Label resourcesRequests =
                resourcesDashboard.GetNode<Label>("Summary/RequestsCard/Content/Value");
            Label resourcesActiveStatus = resourcesDashboard.GetNode<Label>("ActiveStatus");
            Tree resourcesActiveTree = resourcesDashboard.GetNode<Tree>("ActiveList");
            Label resourcesHistoryStatus = resourcesDashboard.GetNode<Label>("HistoryStatus");
            Tree resourcesHistoryTree = resourcesDashboard.GetNode<Tree>("HistoryList");
            VBoxContainer dataTableDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/DataTableDashboard");
            Label dataTableLoaded =
                dataTableDashboard.GetNode<Label>("Summary/LoadedCard/Content/Value");
            Label dataTableTables =
                dataTableDashboard.GetNode<Label>("Summary/TablesCard/Content/Value");
            Label dataTableLoading =
                dataTableDashboard.GetNode<Label>("Summary/LoadingCard/Content/Value");
            Label dataTableFailed =
                dataTableDashboard.GetNode<Label>("Summary/FailedCard/Content/Value");
            Label dataTableDataSetStatus = dataTableDashboard.GetNode<Label>("DataSetStatus");
            Tree dataTableDataSetTree = dataTableDashboard.GetNode<Tree>("DataSetList");
            Label dataTableHistoryStatus = dataTableDashboard.GetNode<Label>("HistoryStatus");
            Tree dataTableHistoryTree = dataTableDashboard.GetNode<Tree>("HistoryList");
            VBoxContainer uiDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/UiDashboard");
            Label uiScene =
                uiDashboard.GetNode<Label>("Summary/SceneCard/Content/Value");
            Label uiView =
                uiDashboard.GetNode<Label>("Summary/ViewCard/Content/Value");
            Label uiModal =
                uiDashboard.GetNode<Label>("Summary/ModalCard/Content/Value");
            Label uiCurrent =
                uiDashboard.GetNode<Label>("Summary/CurrentCard/Content/Value");
            Label uiCurrentDetail =
                uiDashboard.GetNode<Label>("Summary/CurrentCard/Content/Detail");
            Label uiStackStatus = uiDashboard.GetNode<Label>("StackStatus");
            Tree uiStackTree = uiDashboard.GetNode<Tree>("StackList");
            VBoxContainer procedureDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/ProcedureDashboard");
            Label procedureCurrent =
                procedureDashboard.GetNode<Label>("Summary/CurrentCard/Content/Value");
            Label procedureState =
                procedureDashboard.GetNode<Label>("Summary/StateCard/Content/Value");
            Label procedureResult =
                procedureDashboard.GetNode<Label>("Summary/ResultCard/Content/Value");
            Tree procedureDetails = procedureDashboard.GetNode<Tree>("Details");
            VBoxContainer servicesDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/ServicesDashboard");
            LineEdit servicesSearch = servicesDashboard.GetNode<LineEdit>("Search");
            Label servicesContracts =
                servicesDashboard.GetNode<Label>("Summary/ContractsCard/Content/Value");
            Label servicesImplementations =
                servicesDashboard.GetNode<Label>("Summary/ImplementationsCard/Content/Value");
            Label servicesMatchStatus = servicesDashboard.GetNode<Label>("MatchStatus");
            Tree servicesTree = servicesDashboard.GetNode<Tree>("ServiceList");
            Label servicesSelectionDetail = servicesDashboard.GetNode<Label>("SelectionDetail");
            VBoxContainer eventsDashboard =
                overlay.GetNode<VBoxContainer>("Panel/Margin/VBox/Body/Page/EventsDashboard");
            LineEdit eventsSearch = eventsDashboard.GetNode<LineEdit>("Search");
            Label eventsTypes =
                eventsDashboard.GetNode<Label>("Summary/TypesCard/Content/Value");
            Label eventsListeners =
                eventsDashboard.GetNode<Label>("Summary/ListenersCard/Content/Value");
            Label eventsMatchStatus = eventsDashboard.GetNode<Label>("MatchStatus");
            Tree eventsTree = eventsDashboard.GetNode<Tree>("EventList");
            Label eventsSelectionDetail = eventsDashboard.GetNode<Label>("SelectionDetail");
            Label schedulerActive =
                schedulerDashboard.GetNode<Label>("Content/StatusGrid/ActiveCard/Content/Value");
            Label schedulerPaused =
                schedulerDashboard.GetNode<Label>("Content/StatusGrid/PausedCard/Content/Value");
            Label schedulerRepeating =
                schedulerDashboard.GetNode<Label>("Content/StatusGrid/RepeatingCard/Content/Value");
            Label schedulerNext =
                schedulerDashboard.GetNode<Label>("Content/StatusGrid/NextCard/Content/Value");
            Label schedulerProcessGame =
                schedulerDashboard.GetNode<Label>(
                    "Content/PhaseGrid/ProcessCard/Content/Clocks/Game/Value");
            Label schedulerProcessUnscaled =
                schedulerDashboard.GetNode<Label>(
                    "Content/PhaseGrid/ProcessCard/Content/Clocks/Unscaled/Value");
            Label schedulerProcessReal =
                schedulerDashboard.GetNode<Label>(
                    "Content/PhaseGrid/ProcessCard/Content/Clocks/Real/Value");
            Label schedulerProcessDispatch =
                schedulerDashboard.GetNode<Label>("Content/PhaseGrid/ProcessCard/Content/Dispatch/Value");
            Label schedulerPhysicsGame =
                schedulerDashboard.GetNode<Label>(
                    "Content/PhaseGrid/PhysicsCard/Content/Clocks/Game/Value");
            Label schedulerPhysicsUnscaled =
                schedulerDashboard.GetNode<Label>(
                    "Content/PhaseGrid/PhysicsCard/Content/Clocks/Unscaled/Value");
            Label schedulerPhysicsReal =
                schedulerDashboard.GetNode<Label>(
                    "Content/PhaseGrid/PhysicsCard/Content/Clocks/Real/Value");
            Label schedulerPhysicsDispatch =
                schedulerDashboard.GetNode<Label>("Content/PhaseGrid/PhysicsCard/Content/Dispatch/Value");
            Label schedulerCanceled =
                schedulerDashboard.GetNode<Label>("Content/LifetimeGrid/CanceledCard/Content/Value");
            Label schedulerOwnerCanceled =
                schedulerDashboard.GetNode<Label>("Content/LifetimeGrid/OwnerCard/Content/Value");
            Label schedulerFailed =
                schedulerDashboard.GetNode<Label>("Content/LifetimeGrid/FailedCard/Content/Value");
            LineEdit search =
                overlay.GetNode<LineEdit>("Panel/Margin/VBox/Body/Page/ConsoleToolbar/Search");
            Button pause =
                overlay.GetNode<Button>("Panel/Margin/VBox/Body/Page/ConsoleToolbar/Pause");
            Button copy =
                overlay.GetNode<Button>("Panel/Margin/VBox/Body/Page/ConsoleToolbar/Copy");
            RichTextLabel debuggerLabel =
                overlay.GetNode<RichTextLabel>("Panel/Margin/VBox/Body/Page/Content");
            VScrollBar consoleScrollBar = debuggerLabel.GetVScrollBar();
            HBoxContainer resizeRow =
                overlay.GetNode<HBoxContainer>("Panel/Margin/VBox/ResizeRow");
            Button resizeGrip = overlay.GetNode<Button>("Panel/Margin/VBox/ResizeRow/ResizeGrip");

            Assert(toggle.FocusMode == Control.FocusModeEnum.None &&
                navigation.FocusMode == Control.FocusModeEnum.None &&
                overviewDashboard.FocusMode == Control.FocusModeEnum.None &&
                systemDashboard.FocusMode == Control.FocusModeEnum.None &&
                systemDetails.FocusMode == Control.FocusModeEnum.None &&
                performanceDashboard.FocusMode == Control.FocusModeEnum.None &&
                performanceMetrics.FocusMode == Control.FocusModeEnum.None &&
                inputContextsTree.FocusMode == Control.FocusModeEnum.None &&
                inputActionsTree.FocusMode == Control.FocusModeEnum.None &&
                schedulerDashboard.FocusMode == Control.FocusModeEnum.None &&
                audioDashboard.FocusMode == Control.FocusModeEnum.None &&
                sceneDetails.FocusMode == Control.FocusModeEnum.None &&
                resourcesActiveTree.FocusMode == Control.FocusModeEnum.None &&
                resourcesHistoryTree.FocusMode == Control.FocusModeEnum.None &&
                dataTableDataSetTree.FocusMode == Control.FocusModeEnum.None &&
                dataTableHistoryTree.FocusMode == Control.FocusModeEnum.None &&
                uiStackTree.FocusMode == Control.FocusModeEnum.None &&
                procedureDetails.FocusMode == Control.FocusModeEnum.None &&
                servicesTree.FocusMode == Control.FocusModeEnum.None &&
                eventsTree.FocusMode == Control.FocusModeEnum.None &&
                debuggerLabel.FocusMode == Control.FocusModeEnum.None &&
                allFilter.FocusMode == Control.FocusModeEnum.None &&
                debugFilter.FocusMode == Control.FocusModeEnum.None &&
                infoFilter.FocusMode == Control.FocusModeEnum.None &&
                warningFilter.FocusMode == Control.FocusModeEnum.None &&
                errorFilter.FocusMode == Control.FocusModeEnum.None &&
                olderPage.FocusMode == Control.FocusModeEnum.None &&
                newerPage.FocusMode == Control.FocusModeEnum.None &&
                latestPage.FocusMode == Control.FocusModeEnum.None &&
                pause.FocusMode == Control.FocusModeEnum.None &&
                copy.FocusMode == Control.FocusModeEnum.None &&
                reset.FocusMode == Control.FocusModeEnum.None &&
                resizeGrip.FocusMode == Control.FocusModeEnum.None &&
                search.FocusMode == Control.FocusModeEnum.Click &&
                inputActionsSearch.FocusMode == Control.FocusModeEnum.Click &&
                servicesSearch.FocusMode == Control.FocusModeEnum.Click &&
                eventsSearch.FocusMode == Control.FocusModeEnum.Click,
                $"Debugger 焦点策略错误：toggle={toggle.FocusMode}, navigation={navigation.FocusMode}, " +
                $"content={debuggerLabel.FocusMode}, pause={pause.FocusMode}, copy={copy.FocusMode}, " +
                $"reset={reset.FocusMode}, resize={resizeGrip.FocusMode}, search={search.FocusMode}");
            Assert(inputContextsTree.GetThemeFontSize("title_button_font_size") == 11 &&
                systemDetails.GetThemeFontSize("title_button_font_size") == 11 &&
                performanceMetrics.GetThemeFontSize("title_button_font_size") == 11 &&
                inputActionsTree.GetThemeFontSize("title_button_font_size") == 11 &&
                sceneDetails.GetThemeFontSize("title_button_font_size") == 11 &&
                resourcesActiveTree.GetThemeFontSize("title_button_font_size") == 11 &&
                resourcesHistoryTree.GetThemeFontSize("title_button_font_size") == 11 &&
                dataTableDataSetTree.GetThemeFontSize("title_button_font_size") == 11 &&
                dataTableHistoryTree.GetThemeFontSize("title_button_font_size") == 11 &&
                uiStackTree.GetThemeFontSize("title_button_font_size") == 11 &&
                procedureDetails.GetThemeFontSize("title_button_font_size") == 11 &&
                servicesTree.GetThemeFontSize("title_button_font_size") == 11 &&
                eventsTree.GetThemeFontSize("title_button_font_size") == 11,
                "Debugger 表格表头字号没有统一为 11");
            Assert(!body.Visible && !title.Visible && !reset.Visible && !resizeRow.Visible,
                "Debugger 默认未折叠");
            Assert(toggle.SizeFlagsHorizontal == Control.SizeFlags.ShrinkBegin &&
                toggle.Text.StartsWith("FPS: ", StringComparison.Ordinal) &&
                !toggle.Text.Contains('W') &&
                !toggle.Text.Contains('E') &&
                toggle.GetThemeFontSize("font_size") == 16 &&
                toggle.GetThemeConstant("outline_size") == 1 &&
                toggle.CustomMinimumSize.X > 0f &&
                panel.Size == panel.GetCombinedMinimumSize(),
                "Debugger 折叠入口的 FPS 文本、字号或背景宽度错误");
            toggle.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(body.Visible && navigation.Visible && title.Visible && reset.Visible &&
                resizeRow.Visible && resizeGrip.Visible,
                "Debugger 点击后未展开");
            Assert(toggle.SizeFlagsHorizontal == Control.SizeFlags.ShrinkBegin &&
                toggle.Text.StartsWith("FPS: ", StringComparison.Ordinal) &&
                !toggle.Text.Contains('W') &&
                !toggle.Text.Contains('E'),
                "Debugger 展开后 FPS 状态没有恢复紧凑显示");
            Assert(resizeGrip.Text == "拖动调整大小 ↘" &&
                resizeGrip.CustomMinimumSize.X >= 120f,
                "Debugger 整体缩放入口不够明显");

            TreeItem root = navigation.GetRoot();
            Assert(root.GetChildCount() == 6, "Debugger 一级分类数量错误");
            TreeItem overview = root.GetFirstChild();
            TreeItem system = overview.GetNext();
            TreeItem performance = system.GetNext();
            TreeItem console = performance.GetNext();
            TreeItem runtime = console.GetNext();
            TreeItem framework = runtime.GetNext();
            Assert(overview.GetText(0) == "概览" &&
                system.GetText(0) == "系统" &&
                performance.GetText(0) == "性能" &&
                console.GetText(0) == "控制台" &&
                runtime.GetText(0) == "运行时" &&
                framework.GetText(0) == "框架",
                "Debugger 树状分类错误");
            Assert(overviewDashboard.Visible && !debuggerLabel.Visible,
                "Overview 没有切换到结构化仪表盘");
            Assert(overviewFps.Text.Length > 0 &&
                int.TryParse(overviewWarnings.Text, out _) &&
                int.TryParse(overviewErrors.Text, out _) &&
                int.TryParse(overviewServices.Text, out int serviceCount) &&
                serviceCount > 0,
                "Overview 顶部状态或服务指标未刷新");
            Assert(overviewEventsDetail.Text.Contains("监听器", StringComparison.Ordinal) &&
                overviewScene.Text.Length > 0 &&
                overviewAudio.Text.Length > 0 &&
                overviewInput.Text.Length > 0 &&
                overviewScheduler.Text.Length > 0,
                "Overview 框架或活动指标未完整刷新");

            SelectNavigationItem(navigation, system);
            overlay._Process(0.3d);
            Assert(title.Text == "系统" &&
                systemDashboard.Visible &&
                !debuggerLabel.Visible &&
                systemPlatform.Text.Length > 0 &&
                systemBuild.Text == "Debug" &&
                systemRenderer.Text.Length > 0 &&
                !systemRenderer.Text.Contains('_') &&
                systemUptime.Text.Contains(':') &&
                systemDetails.GetRoot()?.GetChildCount() == 4,
                "System 结构化诊断页没有完整显示环境摘要与分组详情");
            TreeItem systemRuntimeGroup = systemDetails.GetRoot()!.GetFirstChild();
            TreeItem systemPlatformGroup = systemRuntimeGroup.GetNext();
            TreeItem systemWindowGroup = systemPlatformGroup.GetNext();
            TreeItem systemRenderingGroup = systemWindowGroup.GetNext();
            Assert(systemRuntimeGroup.GetChildCount() == 5 &&
                systemPlatformGroup.GetChildCount() == 3 &&
                systemWindowGroup.GetChildCount() == 5 &&
                systemRenderingGroup.GetChildCount() == 5,
                "System 运行时、平台、窗口或渲染信息分组不完整");

            SelectNavigationItem(navigation, performance);
            overlay._Process(0.3d);
            Assert(title.Text == "性能" &&
                performanceDashboard.Visible &&
                !debuggerLabel.Visible &&
                int.TryParse(performanceFps.Text, out _) &&
                performanceProcess.Text.EndsWith(" ms", StringComparison.Ordinal) &&
                performancePhysics.Text.EndsWith(" ms", StringComparison.Ordinal) &&
                performanceMemory.Text.Length > 2 &&
                performanceManagedMemory.Text.Length > 2 &&
                performanceProcessColor.Color == new Color(0.31f, 0.68f, 1f) &&
                performancePhysicsColor.Color == new Color(0.65f, 0.49f, 1f) &&
                performanceEngineColor.Color == new Color(0.24f, 0.82f, 0.65f) &&
                performanceManagedColor.Color == new Color(1f, 0.68f, 0.28f) &&
                performanceEngineLegend.Text == "Godot 引擎内存" &&
                performanceManagedLegend.Text == ".NET 托管堆" &&
                performanceFrameGraph.CustomMinimumSize.Y >= 70f &&
                performanceMemoryGraph.CustomMinimumSize.Y >= 70f &&
                performanceMetrics.CustomMinimumSize.Y <= 160f &&
                performanceMetrics.GetRoot()?.GetChildCount() == 6,
                "Performance 结构化诊断页没有完整显示摘要、趋势与分组指标");
            TreeItem firstPerformanceMetric =
                performanceMetrics.GetRoot()!.GetFirstChild().GetFirstChild();
            Assert(firstPerformanceMetric.GetTextAlignment(1) == HorizontalAlignment.Left &&
                firstPerformanceMetric.GetTextAlignment(2) == HorizontalAlignment.Right &&
                firstPerformanceMetric.GetTextAlignment(3) == HorizontalAlignment.Left,
                "Performance 指标、数值和说明没有按规则对齐");

            Assert(runtime.GetChildCount() == 8, "运行时二级页面错误");
            TreeItem scenePage = runtime.GetFirstChild().GetNext().GetNext().GetNext();
            TreeItem resourcesPage = scenePage.GetNext();
            TreeItem dataTablePage = resourcesPage.GetNext();
            TreeItem uiPage = dataTablePage.GetNext();
            TreeItem procedurePage = uiPage.GetNext();
            SelectNavigationItem(navigation, scenePage);
            Assert(title.Text == "Scene" &&
                sceneDashboard.Visible &&
                !debuggerLabel.Visible &&
                sceneCurrent.Text.Length > 0 &&
                int.TryParse(sceneNodes.Text, out int sceneNodeCount) &&
                sceneNodeCount > 0 &&
                sceneState.Text.Length > 0 &&
                sceneProgress.Text.EndsWith('%') &&
                sceneDetails.GetRoot()?.GetChildCount() >= 3,
                "Scene 结构化诊断页没有完整显示场景摘要与切换状态");
            SelectNavigationItem(navigation, resourcesPage);
            Assert(title.Text == "Resources" &&
                resourcesDashboard.Visible &&
                !debuggerLabel.Visible &&
                int.TryParse(resourcesActive.Text, out _) &&
                resourcesRequests.Text.Contains(" / ", StringComparison.Ordinal) &&
                resourcesActiveStatus.Text.StartsWith("当前请求 ", StringComparison.Ordinal) &&
                resourcesActiveTree.GetRoot() is not null &&
                resourcesHistoryStatus.Text.StartsWith("最近请求 ", StringComparison.Ordinal) &&
                resourcesHistoryTree.GetRoot() is not null,
                "Resources 结构化诊断页没有完整显示请求统计与历史");
            SelectNavigationItem(navigation, dataTablePage);
            Assert(title.Text == "DataTable" &&
                dataTableDashboard.Visible &&
                !debuggerLabel.Visible &&
                dataTableLoaded.Text == "0" &&
                dataTableTables.Text == "0" &&
                dataTableLoading.Text == "0" &&
                dataTableFailed.Text == "0" &&
                dataTableDataSetStatus.Text == "当前数据集 0" &&
                dataTableDataSetTree.GetRoot() is not null &&
                dataTableHistoryTree.GetRoot() is not null,
                "DataTable 空状态诊断页没有完整渲染");

            IDataTableService originalDataTable = Services.Get<IDataTableService>();
            var loadingDataSet = new DataTableSetDefinition(
                "debugger.loading",
                2,
                1,
                new DataTableDefinition[]
                {
                    new("First", "first.gdtb", _ => new object()),
                    new("Second", "second.gdtb", _ => new object()),
                });
            Task activeDataTableLoad = originalDataTable.LoadAsync(
                loadingDataSet,
                "res://Verification/Automated/Fixtures/DataTableService/LoadingManifest");
            Assert(!activeDataTableLoad.IsCompleted,
                "DataTable 多表加载没有暴露可观察的加载中状态");
            SelectNavigationItem(navigation, dataTablePage);
            Assert(dataTableLoading.Text == "1" &&
                dataTableDataSetTree.GetRoot()?.GetFirstChild()?.GetText(3) == "50%",
                "DataTable 加载中数量或表级进度没有显示");
            await activeDataTableLoad;
            SelectNavigationItem(navigation, dataTablePage);
            Assert(dataTableLoaded.Text == "1" &&
                dataTableTables.Text == "2" &&
                dataTableLoading.Text == "0" &&
                dataTableDataSetTree.GetRoot()?.GetFirstChild()?.GetChildCount() == 2,
                "DataTable 已加载表清单没有完整显示");
            Assert(originalDataTable.Unload("debugger.loading"),
                "DataTable Debugger 回归无法卸载多表数据集");

            var emptyDataSet = new DataTableSetDefinition(
                "debugger.empty",
                2,
                1,
                Array.Empty<DataTableDefinition>());
            await originalDataTable.LoadAsync(
                emptyDataSet,
                "res://Verification/Automated/Fixtures/DataTableService/EmptyManifest");
            SelectNavigationItem(navigation, dataTablePage);
            Assert(dataTableLoaded.Text == "1" &&
                dataTableTables.Text == "0" &&
                dataTableDataSetTree.GetRoot()?.GetChildCount() == 1 &&
                dataTableHistoryStatus.Text.StartsWith("最近结果 3", StringComparison.Ordinal),
                "DataTable 已发布数据集没有显示在快照中");
            Assert(originalDataTable.Unload("debugger.empty"),
                "DataTable Debugger 回归无法卸载空数据集");

            try
            {
                await originalDataTable.LoadAsync(
                    new DataTableSetDefinition(
                        "game.base",
                        2,
                        1,
                        Array.Empty<DataTableDefinition>()),
                    "res://Verification/Automated/Fixtures/DataTableService/InvalidManifest");
                throw new InvalidOperationException("无效 DataTable Manifest 没有失败");
            }
            catch (DataTableLoadException)
            {
            }
            SelectNavigationItem(navigation, dataTablePage);
            Assert(dataTableLoaded.Text == "0" &&
                dataTableFailed.Text == "1" &&
                dataTableHistoryTree.GetRoot()?.GetChildCount() == 5,
                "DataTable 卸载或失败结果没有写入有界历史");

            Assert(Services.Unregister(originalDataTable), "DataTableService 测试注销失败");
            try
            {
                SelectNavigationItem(navigation, dataTablePage);
                Assert(dataTableLoaded.Text == "未注册" &&
                    dataTableTables.Text == "—" &&
                    dataTableDataSetTree.GetRoot() is null,
                    "DataTableService 未注册状态没有完整降级");

                var unsupportedDataTable = new UnsupportedDataTableService();
                Services.Register<IDataTableService>(unsupportedDataTable);
                SelectNavigationItem(navigation, dataTablePage);
                Assert(dataTableLoaded.Text == "不支持" &&
                    dataTableDataSetStatus.Text == "当前实现不支持 Debug 快照",
                    "DataTableService 非内置实现没有区分 Debug 快照能力");
                Assert(Services.Unregister<IDataTableService>(unsupportedDataTable),
                    "DataTableService 测试实现注销失败");
            }
            finally
            {
                if (!Services.TryGet<IDataTableService>(out _))
                    Services.Register(originalDataTable);
            }

            SelectNavigationItem(navigation, uiPage);
            Assert(title.Text == "UI" &&
                uiDashboard.Visible &&
                !debuggerLabel.Visible &&
                uiScene.Text == "0" &&
                uiView.Text == "0" &&
                uiModal.Text == "0" &&
                uiCurrent.Text == "空闲" &&
                uiStackStatus.Text == "受管理界面 0" &&
                uiStackTree.GetRoot() is not null,
                "UI 空状态诊断页没有完整渲染");

            IUiService originalUi = Services.Get<IUiService>();
            ResourceKey uiControlAKey =
                ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiControlA.tscn");
            ResourceKey uiControlBKey =
                ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiControlB.tscn");
            Control sceneUi = originalUi.Open(uiControlAKey, UiLayer.Scene);
            Control firstView = originalUi.Open(uiControlAKey, UiLayer.View);
            Control secondView = originalUi.Open(uiControlBKey, UiLayer.View);
            Control modal = originalUi.Open(uiControlAKey, UiLayer.Modal);
            SelectNavigationItem(navigation, uiPage);
            TreeItem? topUiEntry = uiStackTree.GetRoot()?.GetFirstChild();
            Assert(uiScene.Text == "1" &&
                uiView.Text == "2" &&
                uiModal.Text == "1" &&
                uiCurrent.Text == "UiControlA" &&
                uiCurrentDetail.Text == uiControlAKey.Value &&
                uiStackStatus.Text == "受管理界面 4" &&
                topUiEntry?.GetText(0) == "Modal" &&
                topUiEntry.GetText(4) == "显示" &&
                topUiEntry.GetNext()?.GetText(2) == "UiControlB",
                "UI 层数量、当前顶层或栈顺序显示错误");
            originalUi.Close(modal);
            originalUi.Close(secondView);
            originalUi.Close(firstView);
            originalUi.Close(sceneUi);

            Assert(Services.Unregister(originalUi), "UiService 测试注销失败");
            try
            {
                SelectNavigationItem(navigation, uiPage);
                Assert(uiScene.Text == "未注册" &&
                    uiView.Text == "—" &&
                    uiStackTree.GetRoot() is null,
                    "UiService 未注册状态没有完整降级");

                var unsupportedUi = new UnsupportedUiService();
                Services.Register<IUiService>(unsupportedUi);
                SelectNavigationItem(navigation, uiPage);
                Assert(uiScene.Text == "不支持" &&
                    uiStackStatus.Text == "当前实现不支持 Debug 快照",
                    "UiService 非内置实现没有区分 Debug 快照能力");
                Assert(Services.Unregister<IUiService>(unsupportedUi),
                    "UiService 测试实现注销失败");
            }
            finally
            {
                if (!Services.TryGet<IUiService>(out _))
                    Services.Register(originalUi);
            }

            SelectNavigationItem(navigation, procedurePage);
            Assert(title.Text == "Procedure" &&
                procedureDashboard.Visible &&
                !debuggerLabel.Visible &&
                procedureCurrent.Text == "空闲" &&
                procedureState.Text == "空闲" &&
                procedureDetails.GetRoot()?.GetChildCount() == 4,
                "Procedure 空状态诊断页没有完整渲染");
            IProcedureService procedures = Services.Get<IProcedureService>();
            await procedures.ChangeAsync(new DebuggerProcedure("Debugger.Active"));
            SelectNavigationItem(navigation, procedurePage);
            Assert(procedureCurrent.Text == "Debugger.Active" &&
                procedureResult.Text == "无" &&
                procedureDetails.GetRoot()?.GetFirstChild()?.GetNext()?.GetNext()
                    ?.GetText(1) == "Debugger.Active",
                "Procedure 成功切换没有更新当前流程和最近结果");
            try
            {
                await procedures.ChangeAsync(
                    new DebuggerProcedure("Debugger.Failed", failEnter: true));
                throw new InvalidOperationException("Procedure 失败用例没有抛出异常");
            }
            catch (ProcedureChangeException)
            {
            }
            SelectNavigationItem(navigation, procedurePage);
            TreeItem? failureDetail = procedureDetails.GetRoot()?.GetFirstChild()
                ?.GetNext()?.GetNext()?.GetNext();
            Assert(procedureCurrent.Text == "空闲" &&
                procedureResult.Text == "有" &&
                failureDetail?.GetText(1).Contains("Debugger.Failed", StringComparison.Ordinal) == true,
                "Procedure 进入失败没有显示最近失败");

            SelectNavigationItem(navigation, runtime.GetFirstChild());
            bool hasInputActionCount = int.TryParse(inputActions.Text, out int inputActionCount);
            Assert(title.Text == "Input" &&
                !overviewDashboard.Visible &&
                inputDashboard.Visible &&
                !debuggerLabel.Visible &&
                inputBackend.Text.Length > 0 &&
                ulong.TryParse(inputFrame.Text, out _) &&
                hasInputActionCount &&
                inputActionsTree.GetRoot()?.GetChildCount() ==
                    Math.Min(inputActionCount, 32) &&
                inputContextsTree.CustomMinimumSize.Y >= 100f &&
                inputContextsTree.GetColumnTitleAlignment(0) == HorizontalAlignment.Left &&
                inputActionsTree.GetColumnTitleAlignment(0) == HorizontalAlignment.Left &&
                inputActionsTree.GetColumnTitleAlignment(2) == HorizontalAlignment.Left &&
                inputActionHeader.Text == "Action 状态",
                $"Input 结构化仪表盘没有完整渲染：backend={inputBackend.Text}, " +
                $"frame={inputFrame.Text}, actions={inputActions.Text}, " +
                $"rows={inputActionsTree.GetRoot()?.GetChildCount() ?? -1}");
            Assert(inputContextsTree.GetRoot() is not null,
                "Input Context 列表没有创建根节点");
            if (inputActionCount > 0)
            {
                TreeItem firstInputAction = inputActionsTree.GetRoot()!.GetFirstChild();
                inputActionsSearch.Text = firstInputAction.GetText(0);
                inputActionsSearch.EmitSignal(
                    LineEdit.SignalName.TextChanged,
                    inputActionsSearch.Text);
                Assert(inputActionsTree.GetRoot()?.GetChildCount() >= 1 &&
                    inputActionsMatchStatus.Text.StartsWith("找到 ", StringComparison.Ordinal),
                    "Input Action 搜索没有匹配名称");
            }
            inputActionsSearch.Text = "missing-input-action";
            inputActionsSearch.EmitSignal(
                LineEdit.SignalName.TextChanged,
                inputActionsSearch.Text);
            Assert(inputActionsTree.GetRoot()?.GetChildCount() == 0 &&
                inputActionsMatchStatus.Text == "找到 0 个 Action",
                "Input Action 搜索空结果状态错误");
            inputActionsSearch.Text = string.Empty;
            inputActionsSearch.EmitSignal(
                LineEdit.SignalName.TextChanged,
                inputActionsSearch.Text);

            IInputService originalInput = Services.Get<IInputService>();
            Assert(Services.Unregister(originalInput), "InputService 测试注销失败");
            try
            {
                SelectNavigationItem(navigation, overview);
                Assert(overviewInput.Text == "未注册",
                    "Overview 没有显示 InputService 未注册");
                SelectNavigationItem(navigation, runtime.GetFirstChild());
                Assert(inputBackend.Text == "未注册" &&
                    inputActions.Text == "0" &&
                    inputContextsTree.GetRoot() is null &&
                    inputActionsTree.GetRoot() is null,
                    "InputService 未注册状态没有完整降级");

                var unsupportedInput = new UnsupportedInputService();
                Services.Register<IInputService>(unsupportedInput);
                SelectNavigationItem(navigation, overview);
                Assert(overviewInput.Text == "不支持",
                    "Overview 没有区分 InputService 不支持 Debug 快照");
                SelectNavigationItem(navigation, runtime.GetFirstChild());
                Assert(inputBackend.Text == "不支持" &&
                    inputActionsMatchStatus.Text == "当前实现不支持 Debug 快照",
                    "InputService 已注册但不支持 Debug 快照时状态错误");
                Assert(Services.Unregister<IInputService>(unsupportedInput),
                    "InputService 测试实现注销失败");
            }
            finally
            {
                if (!Services.TryGet<IInputService>(out _))
                    Services.Register(originalInput);
            }
            overlay._Process(0.3d);
            Assert(ulong.TryParse(inputFrame.Text, out _),
                "InputService 恢复后页面没有重新读取快照");

            SelectNavigationItem(navigation, runtime.GetFirstChild().GetNext());
            Assert(title.Text == "Scheduler", "Scheduler 页面切换失败");
            Assert(schedulerDashboard.Visible && !debuggerLabel.Visible &&
                int.TryParse(schedulerActive.Text, out _) &&
                schedulerNext.Text.Length > 0 &&
                int.TryParse(schedulerProcessDispatch.Text, out _) &&
                int.TryParse(schedulerPhysicsDispatch.Text, out _) &&
                int.TryParse(schedulerFailed.Text, out _),
                "Scheduler 仪表盘没有完整渲染");

            ISchedulerService originalScheduler = Services.Get<ISchedulerService>();
            Assert(Services.Unregister(originalScheduler), "SchedulerService 测试注销失败");
            try
            {
                SelectNavigationItem(navigation, overview);
                Assert(overviewScheduler.Text == "未注册",
                    "Overview 没有显示 SchedulerService 未注册");
                SelectNavigationItem(navigation, runtime.GetFirstChild().GetNext());
                Label[] unavailableSchedulerValues =
                {
                    schedulerPaused,
                    schedulerRepeating,
                    schedulerNext,
                    schedulerProcessGame,
                    schedulerProcessUnscaled,
                    schedulerProcessReal,
                    schedulerProcessDispatch,
                    schedulerPhysicsGame,
                    schedulerPhysicsUnscaled,
                    schedulerPhysicsReal,
                    schedulerPhysicsDispatch,
                    schedulerCanceled,
                    schedulerOwnerCanceled,
                    schedulerFailed,
                };
                Assert(schedulerActive.Text == "未注册" &&
                    Array.TrueForAll(unavailableSchedulerValues, label => label.Text == "—"),
                    "SchedulerService 未注册后仍残留旧指标");

                var unsupportedScheduler = new UnsupportedSchedulerService();
                Services.Register<ISchedulerService>(unsupportedScheduler);
                SelectNavigationItem(navigation, overview);
                Assert(overviewScheduler.Text == "不支持",
                    "Overview 没有区分 SchedulerService 不支持 Debug 快照");
                SelectNavigationItem(navigation, runtime.GetFirstChild().GetNext());
                Assert(schedulerActive.Text == "不支持 Debug 快照" &&
                    Array.TrueForAll(unavailableSchedulerValues, label => label.Text == "—"),
                    "SchedulerService 已注册但不支持 Debug 快照时状态错误");
                Assert(Services.Unregister<ISchedulerService>(unsupportedScheduler),
                    "SchedulerService 测试实现注销失败");
            }
            finally
            {
                if (!Services.TryGet<ISchedulerService>(out _))
                    Services.Register(originalScheduler);
            }
            overlay._Process(0.3d);
            Assert(int.TryParse(schedulerActive.Text, out _),
                "SchedulerService 恢复后页面没有重新读取快照");

            SelectNavigationItem(navigation, runtime.GetFirstChild().GetNext().GetNext());
            Assert(title.Text == "Audio" &&
                audioDashboard.Visible &&
                !debuggerLabel.Visible &&
                audioBgmState.Text.Length > 0 &&
                audioBgmResource.Text.Length > 0 &&
                audioSfx.Text.Contains("/", StringComparison.Ordinal) &&
                IsPercentage(audioMasterVolume.Text) &&
                IsPercentage(audioBgmVolume.Text) &&
                IsPercentage(audioSfxVolume.Text),
                "Audio 仪表盘没有完整渲染");

            IAudioService originalAudio = Services.Get<IAudioService>();
            Assert(Services.Unregister(originalAudio), "AudioService 测试注销失败");
            try
            {
                SelectNavigationItem(navigation, overview);
                Assert(overviewAudio.Text == "未注册",
                    "Overview 没有显示 AudioService 未注册");
                SelectNavigationItem(
                    navigation,
                    runtime.GetFirstChild().GetNext().GetNext());
                Assert(audioBgmState.Text == "未注册" &&
                    audioBgmResource.Text == "—" &&
                    audioSfx.Text == "—" &&
                    audioMasterVolume.Text == "—" &&
                    audioBgmVolume.Text == "—" &&
                    audioSfxVolume.Text == "—",
                    "AudioService 未注册状态没有完整降级");

                var throwingAudio = new ThrowingAudioService();
                Services.Register<IAudioService>(throwingAudio);
                overlay._Process(0.3d);
                Assert(!audioDashboard.Visible &&
                    debuggerLabel.Visible &&
                    debuggerLabel.Text.Contains("页面读取失败：Audio", StringComparison.Ordinal) &&
                    debuggerLabel.Text.Contains(
                        nameof(ThrowingAudioService),
                        StringComparison.Ordinal),
                    "Audio 页面读取异常没有被隔离并显示");
                Assert(Services.Unregister<IAudioService>(throwingAudio),
                    "AudioService 异常测试实现注销失败");
            }
            finally
            {
                if (!Services.TryGet<IAudioService>(out _))
                    Services.Register(originalAudio);
            }
            overlay._Process(0.3d);
            Assert(audioDashboard.Visible && !debuggerLabel.Visible &&
                IsPercentage(audioMasterVolume.Text),
                "AudioService 恢复后页面没有退出异常降级状态");

            Assert(framework.GetChildCount() == 2, "框架二级页面错误");
            SelectNavigationItem(navigation, framework.GetFirstChild());
            Assert(title.Text == "Services" &&
                servicesDashboard.Visible &&
                !debuggerLabel.Visible &&
                int.TryParse(servicesContracts.Text, out int serviceContractCount) &&
                serviceContractCount > 0 &&
                int.TryParse(servicesImplementations.Text, out int implementationCount) &&
                implementationCount > 0 &&
                servicesTree.GetRoot()?.GetChildCount() == serviceContractCount,
                "Services 结构化快照没有完整渲染");
            TreeItem firstServiceItem = servicesTree.GetRoot()!.GetFirstChild();
            firstServiceItem.Select(0);
            servicesTree.EmitSignal(Tree.SignalName.ItemSelected);
            Assert(servicesSelectionDetail.Text.Contains(" → ", StringComparison.Ordinal) &&
                servicesSelectionDetail.Text.Contains(
                    firstServiceItem.GetText(0),
                    StringComparison.Ordinal),
                "Services 选中项没有显示完整注册关系");
            servicesSearch.Text = firstServiceItem.GetText(1);
            servicesSearch.EmitSignal(LineEdit.SignalName.TextChanged, servicesSearch.Text);
            Assert(servicesTree.GetRoot()?.GetChildCount() >= 1 &&
                servicesMatchStatus.Text.StartsWith("找到 ", StringComparison.Ordinal),
                "Services 搜索没有匹配实现类型");
            servicesSearch.Text = "missing-service-type";
            servicesSearch.EmitSignal(LineEdit.SignalName.TextChanged, servicesSearch.Text);
            Assert(servicesTree.GetRoot()?.GetChildCount() == 0 &&
                servicesMatchStatus.Text.StartsWith("找到 0 / ", StringComparison.Ordinal),
                "Services 搜索空结果状态错误");
            servicesSearch.Text = string.Empty;
            servicesSearch.EmitSignal(LineEdit.SignalName.TextChanged, servicesSearch.Text);
            SelectNavigationItem(navigation, framework.GetFirstChild().GetNext());
            Assert(title.Text == "Events" &&
                eventsDashboard.Visible &&
                !debuggerLabel.Visible &&
                int.TryParse(eventsTypes.Text, out int eventTypeCount) &&
                eventTypeCount > 0 &&
                int.TryParse(eventsListeners.Text, out int eventListenerCount) &&
                eventListenerCount > 0 &&
                eventsTree.GetRoot()?.GetChildCount() == eventTypeCount,
                "Events 结构化快照没有完整渲染");
            TreeItem firstEventItem = eventsTree.GetRoot()!.GetFirstChild();
            firstEventItem.Select(0);
            eventsTree.EmitSignal(Tree.SignalName.ItemSelected);
            Assert(eventsSelectionDetail.Text.Contains(firstEventItem.GetText(0), StringComparison.Ordinal),
                "Events 选中项没有显示完整类型名");
            eventsSearch.Text = firstEventItem.GetText(0);
            eventsSearch.EmitSignal(LineEdit.SignalName.TextChanged, eventsSearch.Text);
            Assert(eventsTree.GetRoot()?.GetChildCount() >= 1 &&
                eventsMatchStatus.Text.StartsWith("找到 ", StringComparison.Ordinal),
                "Events 搜索没有匹配事件类型");
            eventsSearch.Text = "missing-event-type";
            eventsSearch.EmitSignal(LineEdit.SignalName.TextChanged, eventsSearch.Text);
            Assert(eventsTree.GetRoot()?.GetChildCount() == 0 &&
                eventsMatchStatus.Text.StartsWith("找到 0 / ", StringComparison.Ordinal),
                "Events 搜索空结果状态错误");
            eventsSearch.Text = string.Empty;
            eventsSearch.EmitSignal(LineEdit.SignalName.TextChanged, eventsSearch.Text);

            Assert(console.GetChildCount() == 0, "控制台仍保留旧的单选过滤子页面");
            SelectNavigationItem(navigation, console);
            search.Text = "missing-empty-console-query";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            Assert(debuggerLabel.Text.Length == 0 &&
                pageStatus.Text == "日志 0" &&
                warningFilter.Text == "Warning (0)" &&
                errorFilter.Text == "Error (0)",
                "控制台空筛选结果状态或 ErrorHub 初始计数错误");
            search.Text = string.Empty;
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);

            ErrorHub.Warn("Debugger warning filter", "DebuggerRegression");
            ErrorHub.Report(
                ErrorLevel.Error,
                "Debugger error filter",
                "DebuggerRegression");
            overlay._Process(0.3d);
            Assert(warningFilter.Text == "Warning (1)" &&
                errorFilter.Text == "Error (1)" &&
                toggle.Text.StartsWith("FPS: ", StringComparison.Ordinal) &&
                !toggle.Text.Contains('W') &&
                !toggle.Text.Contains('E') &&
                toggle.GetThemeColor("font_color") == new Color(1f, 0.42f, 0.38f) &&
                toggle.GetThemeColor("font_outline_color") == new Color(1f, 0.42f, 0.38f) &&
                debuggerLabel.Text.Contains("Debugger warning filter", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("Debugger error filter", StringComparison.Ordinal),
                "ErrorHub Warning/Error 摘要或 FPS 紧凑文本错误");
            warningFilter.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(warningFilter.ButtonPressed && !allFilter.ButtonPressed &&
                debuggerLabel.Text.Contains("Debugger warning filter", StringComparison.Ordinal) &&
                !debuggerLabel.Text.Contains("Debugger error filter", StringComparison.Ordinal),
                "控制台 Warning 独占筛选失败");
            errorFilter.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(warningFilter.ButtonPressed && errorFilter.ButtonPressed &&
                debuggerLabel.Text.Contains("Debugger warning filter", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("Debugger error filter", StringComparison.Ordinal),
                "控制台 Warning + Error 组合筛选失败");
            warningFilter.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(!warningFilter.ButtonPressed && errorFilter.ButtonPressed &&
                !debuggerLabel.Text.Contains("Debugger warning filter", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("Debugger error filter", StringComparison.Ordinal),
                "控制台取消 Warning 筛选失败");
            errorFilter.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(allFilter.ButtonPressed && !errorFilter.ButtonPressed,
                "控制台取消最后一个 Error 筛选后没有恢复 All");

            for (int index = 0; index < LogHub.DebugHistoryCapacity - 18; index++)
                LogHub.Debug($"Debugger preload sample {index}", "DebuggerRegression");
            LogHub.Debug("Debugger debug filter", "DebuggerRegression");
            LogHub.Info("Debugger search needle", "DebuggerRegression");
            for (int index = 0; index < 8; index++)
            {
                LogHub.Debug($"Debugger debug sample {index}", "DebuggerRegression");
                LogHub.Info($"Debugger info sample {index}", "DebuggerRegression");
            }
            Stopwatch firstConsoleRender = Stopwatch.StartNew();
            SelectNavigationItem(navigation, console);
            firstConsoleRender.Stop();
            Assert(firstConsoleRender.Elapsed < TimeSpan.FromMilliseconds(500),
                $"控制台首次渲染 1000 条历史耗时过长：{firstConsoleRender.Elapsed.TotalMilliseconds:0.0} ms");
            for (int frame = 0; frame < 3; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(title.Text == "控制台", "控制台页面标题错误");
            Assert(consoleToolbar.Visible && consoleFilters.Visible && consolePagination.Visible,
                "控制台工具栏、筛选栏或分页栏没有显示");
            Assert(allFilter.ButtonPressed &&
                allFilter.Text.StartsWith("All (", StringComparison.Ordinal) &&
                allFilter.Text.EndsWith(')') &&
                debugFilter.Text.StartsWith("Debug (", StringComparison.Ordinal) &&
                debugFilter.Text.EndsWith(')') &&
                infoFilter.Text.StartsWith("Info (", StringComparison.Ordinal) &&
                infoFilter.Text.EndsWith(')') &&
                debuggerLabel.Text.Contains("Debugger debug sample 7", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("Debugger info sample 7", StringComparison.Ordinal) &&
                !debuggerLabel.Text.Contains("【最近日志】", StringComparison.Ordinal) &&
                olderPage.Text == "上一页" &&
                newerPage.Text == "下一页" &&
                latestPage.Text == "最新日志",
                "控制台默认 All 状态或计数标签错误");

            debugFilter.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(debugFilter.ButtonPressed && !allFilter.ButtonPressed &&
                debuggerLabel.Text.Contains("Debugger debug filter", StringComparison.Ordinal) &&
                !debuggerLabel.Text.Contains("Debugger search needle", StringComparison.Ordinal),
                "控制台从 All 切换到单独 Debug 失败");
            infoFilter.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(debugFilter.ButtonPressed && infoFilter.ButtonPressed &&
                debuggerLabel.Text.Contains("Debugger debug filter", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("Debugger search needle", StringComparison.Ordinal),
                "控制台 Debug + Info 多选失败");
            debugFilter.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(!debugFilter.ButtonPressed && infoFilter.ButtonPressed &&
                !debuggerLabel.Text.Contains("Debugger debug filter", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("Debugger search needle", StringComparison.Ordinal),
                "控制台取消 Debug 筛选失败");
            infoFilter.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(allFilter.ButtonPressed && !infoFilter.ButtonPressed,
                "控制台取消最后一个等级后没有恢复 All");

            int unchangedRenderCount = overlay.ConsoleRenderCount;
            overlay._Process(0.3d);
            Assert(overlay.ConsoleRenderCount == unchangedRenderCount,
                "控制台内容未变化时仍重复构建日志文本");

            search.Text = "needle";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            Assert(debuggerLabel.Text.Contains("Debugger search needle", StringComparison.Ordinal),
                "控制台搜索没有保留匹配日志");
            search.Text = "missing-query";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            Assert(!debuggerLabel.Text.Contains("Debugger search needle", StringComparison.Ordinal),
                "控制台搜索没有过滤不匹配日志");

            search.Text = string.Empty;
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            pause.EmitSignal(BaseButton.SignalName.Pressed);
            string pausedText = debuggerLabel.Text;
            int pausedScrollCount = overlay.ConsoleScrollToBottomCount;
            LogHub.Info("Debugger paused refresh", "DebuggerRegression");
            overlay._Process(0.3d);
            Assert(pause.Text == "继续" &&
                debuggerLabel.Text == pausedText &&
                overlay.ConsoleScrollToBottomCount == pausedScrollCount,
                "控制台暂停后仍自动刷新");
            pause.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(pause.Text == "暂停" &&
                debuggerLabel.Text.Contains("Debugger paused refresh", StringComparison.Ordinal),
                "控制台继续后没有刷新");

            int stressRenderCount = overlay.ConsoleRenderCount;
            for (int index = 0; index < LogHub.DebugHistoryCapacity; index++)
                LogHub.Debug($"Debugger stress sample {index}", "DebuggerRegression");
            overlay._Process(0.3d);
            Assert(overlay.ConsoleRenderCount == stressRenderCount + 1 &&
                debuggerLabel.Text.Contains("Debugger stress sample 999", StringComparison.Ordinal) &&
                !debuggerLabel.Text.Contains("Debugger stress sample 0", StringComparison.Ordinal),
                "控制台高日志量最新页或有界历史显示错误");
            Assert(pageStatus.Text.Contains("901–1000", StringComparison.Ordinal) &&
                !olderPage.Disabled && newerPage.Disabled && latestPage.Disabled,
                "控制台最新分页状态错误");

            search.Text = "Debugger stress sample 0";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            Assert(debuggerLabel.Text.Contains("Debugger stress sample 0", StringComparison.Ordinal) &&
                pageStatus.Text.Contains("日志 1", StringComparison.Ordinal),
                "控制台未能从完整 1000 条历史中搜索最早日志");

            search.Text = "Debugger stress sample";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            for (int index = 0; index < 9; index++)
                olderPage.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(debuggerLabel.Text.Contains("Debugger stress sample 0", StringComparison.Ordinal) &&
                pageStatus.Text.Contains("1–100", StringComparison.Ordinal) &&
                olderPage.Disabled && !newerPage.Disabled && !latestPage.Disabled,
                "控制台无法翻到最早一页日志");
            int latestScrollCount = overlay.ConsoleScrollToBottomCount;
            latestPage.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(debuggerLabel.Text.Contains("Debugger stress sample 999", StringComparison.Ordinal) &&
                pageStatus.Text.Contains("901–1000", StringComparison.Ordinal) &&
                newerPage.Disabled && latestPage.Disabled &&
                overlay.ConsoleScrollToBottomCount == latestScrollCount + 1,
                "控制台最新日志按钮没有返回最后一页");
            latestScrollCount = overlay.ConsoleScrollToBottomCount;
            latestPage.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(overlay.ConsoleScrollToBottomCount == latestScrollCount + 1,
                "控制台已在最新页时最新日志按钮没有重新滚到底部");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            int scrollEvaluationCount = overlay.ConsoleScrollEvaluationCount;
            consoleScrollBar.Value = consoleScrollBar.MinValue;
            for (int index = 0; index < 64; index++)
            {
                consoleScrollBar.EmitSignal(
                    Godot.Range.SignalName.ValueChanged,
                    consoleScrollBar.Value);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(!latestPage.Disabled &&
                overlay.ConsoleScrollEvaluationCount == scrollEvaluationCount + 1,
                "控制台滚动信号风暴没有合并为一次离底状态检查");
            consoleScrollBar.Value = consoleScrollBar.MaxValue;
            consoleScrollBar.EmitSignal(Godot.Range.SignalName.ValueChanged, consoleScrollBar.Value);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(latestPage.Disabled,
                "控制台手动滚到底部后没有恢复最新日志跟随");
            consoleScrollBar.Value = consoleScrollBar.MinValue;
            consoleScrollBar.EmitSignal(Godot.Range.SignalName.ValueChanged, consoleScrollBar.Value);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(!latestPage.Disabled,
                "控制台再次滚离底部后最新日志按钮没有启用");
            latestScrollCount = overlay.ConsoleScrollToBottomCount;
            LogHub.Debug("Debugger follow suspended", "DebuggerRegression");
            overlay._Process(0.3d);
            Assert(overlay.ConsoleScrollToBottomCount == latestScrollCount,
                "控制台滚动离开底部后仍自动跟随最新日志");
            latestPage.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(latestPage.Disabled &&
                overlay.ConsoleScrollToBottomCount == latestScrollCount + 1,
                "控制台手动返回最新日志后没有恢复自动跟随");
            copy.EmitSignal(BaseButton.SignalName.Pressed);

            panel.Position = new Vector2(80f, 64f);
            panel.Size = new Vector2(520f, 340f);
            reset.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(panel.Position == new Vector2(12f, 12f) &&
                panel.Size.X >= 480f &&
                panel.Size.Y >= 300f,
                "Debugger 默认布局没有恢复");

            toggle.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(!body.Visible && !title.Visible && !reset.Visible && !resizeRow.Visible,
                "Debugger 点击后未折叠");
            Assert(toggle.SizeFlagsHorizontal == Control.SizeFlags.ShrinkBegin &&
                panel.Size == panel.GetCombinedMinimumSize(),
                "Debugger 再次折叠后入口内背景宽度错误");

            GD.Print("[DebuggerOverlayRegression] PASS: Debug 节点与快照页面");
#else
            Assert(GetNodeOrNull<Node>("/root/GoDoRuntime/GoDoDebugger") is null,
                "Release 构建仍创建了 Debugger 节点");
            GD.Print("[DebuggerOverlayRegression] PASS: Release 未创建 Debugger");
#endif
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[DebuggerOverlayRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

#if DEBUG
    private static void SelectNavigationItem(Tree navigation, TreeItem item)
    {
        item.Select(0);
        navigation.EmitSignal(Tree.SignalName.ItemSelected);
    }

    private static bool IsPercentage(string text)
    {
        return text.EndsWith('%') &&
            int.TryParse(text.AsSpan(0, text.Length - 1), out int value) &&
            value >= 0 &&
            value <= 100;
    }

    private sealed class UnsupportedInputService : IInputService
    {
        public bool IsReady => false;
        public InputFrame Frame => default;
        public InputDeviceKind ActiveDevice => InputDeviceKind.Unknown;
        public InputBackendCapabilities Capabilities => InputBackendCapabilities.None;

        public bool TryGetRebinding(out IInputRebinding? rebinding)
        {
            rebinding = null;
            return false;
        }

        public bool TryGetRebindingPersistence(out IInputRebindingPersistence? persistence)
        {
            persistence = null;
            return false;
        }

        public bool TryGetPromptQuery(out IInputPromptQuery? promptQuery)
        {
            promptQuery = null;
            return false;
        }

        public void SetBaseContext(InputContextId context)
        {
        }

        public void PushContext(
            InputContextId context,
            InputContextMode mode = InputContextMode.Exclusive)
        {
        }

        public void PopContext(InputContextId expectedContext)
        {
        }

        public bool IsContextActive(InputContextId context) => false;
    }

    private sealed class UnsupportedSchedulerService : ISchedulerService
    {
        public ScheduleHandle Schedule(
            double delaySeconds,
            Action callback,
            ScheduleOptions options = default) => default;

        public ScheduleHandle ScheduleRepeating(
            double intervalSeconds,
            Action callback,
            ScheduleOptions options = default) => default;

        public ScheduleHandle ScheduleRepeating(
            double initialDelaySeconds,
            double intervalSeconds,
            Action callback,
            ScheduleOptions options = default) => default;

        public Task DelayAsync(
            double delaySeconds,
            ScheduleOptions options = default,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool Cancel(ScheduleHandle handle) => false;
        public bool Pause(ScheduleHandle handle) => false;
        public bool Resume(ScheduleHandle handle) => false;
        public bool IsScheduled(ScheduleHandle handle) => false;

        public bool TryGetRemainingSeconds(
            ScheduleHandle handle,
            out double remainingSeconds)
        {
            remainingSeconds = 0d;
            return false;
        }
    }

    private sealed class UnsupportedDataTableService : IDataTableService
    {
        public Task LoadAsync(
            DataTableSetDefinition definition,
            string runtimeDirectory,
            Action<DataTableLoadProgress>? progress = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool IsLoaded(string dataSetId) => false;

        public TTable GetTable<TTable>(string dataSetId, string tableId)
            where TTable : class =>
            throw new InvalidOperationException(nameof(UnsupportedDataTableService));

        public bool Unload(string dataSetId) => false;
    }

    private sealed class UnsupportedUiService : IUiService
    {
        public Control Open(ResourceKey key, UiLayer layer) =>
            throw new InvalidOperationException(nameof(UnsupportedUiService));

        public void Close(Control view)
        {
        }

        public bool TryGoBack() => false;
    }

    private sealed class DebuggerProcedure : IProcedure
    {
        private readonly bool _failEnter;

        public string Name { get; }

        public DebuggerProcedure(string name, bool failEnter = false)
        {
            Name = name;
            _failEnter = failEnter;
        }

        public Task EnterAsync(ProcedureContext context) =>
            _failEnter
                ? Task.FromException(new InvalidOperationException("Debugger Procedure 进入失败"))
                : Task.CompletedTask;

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }

    private sealed class ThrowingAudioService : IAudioService
    {
        public ResourceKey? CurrentBgm =>
            throw new InvalidOperationException(nameof(ThrowingAudioService));
        public bool IsBgmPlaying => false;
        public bool IsBgmLoading => false;
        public int ActiveSfxCount => 0;
        public int MaxSfxVoices => 0;

        public Task PlayBgmAsync(ResourceKey key, bool restart = false) =>
            Task.CompletedTask;

        public void PauseBgm()
        {
        }

        public void ResumeBgm()
        {
        }

        public void StopBgm()
        {
        }

        public Task<bool> PlaySfxAsync(ResourceKey key) =>
            Task.FromResult(false);

        public void StopAllSfx()
        {
        }

        public float GetVolume(AudioGroup group) => 0f;

        public void SetVolume(AudioGroup group, float linearVolume)
        {
        }
    }
#endif

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
