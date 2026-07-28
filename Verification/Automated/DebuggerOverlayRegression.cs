using System;
using System.Diagnostics;
using Godot;
using GoDo;

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
            Label schedulerNext =
                schedulerDashboard.GetNode<Label>("Content/StatusGrid/NextCard/Content/Value");
            Label schedulerProcessDispatch =
                schedulerDashboard.GetNode<Label>("Content/PhaseGrid/ProcessCard/Content/Dispatch/Value");
            Label schedulerPhysicsDispatch =
                schedulerDashboard.GetNode<Label>("Content/PhaseGrid/PhysicsCard/Content/Dispatch/Value");
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
                inputContextsTree.FocusMode == Control.FocusModeEnum.None &&
                inputActionsTree.FocusMode == Control.FocusModeEnum.None &&
                schedulerDashboard.FocusMode == Control.FocusModeEnum.None &&
                audioDashboard.FocusMode == Control.FocusModeEnum.None &&
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
            Assert(!body.Visible && !title.Visible && !reset.Visible && !resizeRow.Visible,
                "Debugger 默认未折叠");
            Assert(toggle.SizeFlagsHorizontal == Control.SizeFlags.ExpandFill,
                "Debugger 折叠入口内背景没有填满固定宽度");
            toggle.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(body.Visible && navigation.Visible && title.Visible && reset.Visible &&
                resizeRow.Visible && resizeGrip.Visible,
                "Debugger 点击后未展开");
            Assert(toggle.SizeFlagsHorizontal == Control.SizeFlags.ShrinkBegin,
                "Debugger 展开后 FPS 状态没有恢复紧凑宽度");
            Assert(resizeGrip.Text == "拖动调整大小 ↘" &&
                resizeGrip.CustomMinimumSize.X >= 120f,
                "Debugger 整体缩放入口不够明显");

            TreeItem root = navigation.GetRoot();
            Assert(root.GetChildCount() == 4, "Debugger 一级分类数量错误");
            TreeItem overview = root.GetFirstChild();
            TreeItem runtime = overview.GetNext();
            TreeItem framework = runtime.GetNext();
            TreeItem console = framework.GetNext();
            Assert(overview.GetText(0) == "概览" &&
                runtime.GetText(0) == "运行时" &&
                framework.GetText(0) == "框架" &&
                console.GetText(0) == "控制台",
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

            Assert(runtime.GetChildCount() == 3, "运行时二级页面错误");
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
            SelectNavigationItem(navigation, runtime.GetFirstChild().GetNext());
            Assert(title.Text == "Scheduler", "Scheduler 页面切换失败");
            Assert(schedulerDashboard.Visible && !debuggerLabel.Visible &&
                int.TryParse(schedulerActive.Text, out _) &&
                schedulerNext.Text.Length > 0 &&
                int.TryParse(schedulerProcessDispatch.Text, out _) &&
                int.TryParse(schedulerPhysicsDispatch.Text, out _) &&
                int.TryParse(schedulerFailed.Text, out _),
                "Scheduler 仪表盘没有完整渲染");
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
            Assert(toggle.SizeFlagsHorizontal == Control.SizeFlags.ExpandFill,
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
#endif

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
