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

        ConfigureResourceTree(_resourcesActiveTree, "资源 Key", 6);
        _resourcesActiveTree.SetColumnTitle(0, "资源 Key");
        _resourcesActiveTree.SetColumnTitle(1, "类型");
        _resourcesActiveTree.SetColumnTitle(2, "状态");
        _resourcesActiveTree.SetColumnTitle(3, "进度");
        _resourcesActiveTree.SetColumnTitle(4, "请求");
        _resourcesActiveTree.SetColumnTitle(5, "存活");

        ConfigureResourceTree(_resourcesHistoryTree, "资源 Key", 5);
        _resourcesHistoryTree.SetColumnTitle(0, "资源 Key");
        _resourcesHistoryTree.SetColumnTitle(1, "类型");
        _resourcesHistoryTree.SetColumnTitle(2, "方式");
        _resourcesHistoryTree.SetColumnTitle(3, "状态");
        _resourcesHistoryTree.SetColumnTitle(4, "请求");
    }

    private static void ConfigureResourceTree(Tree tree, string firstColumnTitle, int columnCount)
    {
        tree.SetColumnTitle(0, firstColumnTitle);
        tree.SetColumnTitleAlignment(0, HorizontalAlignment.Left);
        tree.SetColumnExpand(0, true);
        for (int column = 1; column < columnCount; column++)
        {
            tree.SetColumnTitleAlignment(column, HorizontalAlignment.Center);
            tree.SetColumnExpand(column, false);
        }
        tree.SetColumnCustomMinimumWidth(1, 74);
        tree.SetColumnCustomMinimumWidth(2, 64);
        tree.SetColumnCustomMinimumWidth(3, 64);
        tree.SetColumnCustomMinimumWidth(4, 54);
        if (columnCount > 5)
            tree.SetColumnCustomMinimumWidth(5, 64);
    }

    private T GetResourcesNode<T>(string path) where T : Node
    {
        T? node = _resourcesDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerResources 场景缺少节点：{path}");
    }

    private void CachePoolNodes()
    {
        _poolRegisteredValue = GetPoolNode<Label>("Summary/RegisteredCard/Content/Value");
        _poolIdleValue = GetPoolNode<Label>("Summary/IdleCard/Content/Value");
        _poolActiveValue = GetPoolNode<Label>("Summary/ActiveCard/Content/Value");
        _poolStatus = GetPoolNode<Label>("Status");
        _poolTree = GetPoolNode<Tree>("PoolList");
        _poolActiveRentalsStatus = GetPoolNode<Label>("ActiveRentalsStatus");
        _poolActiveRentalsTree = GetPoolNode<Tree>("ActiveRentals");
        _poolTree.SetColumnTitle(0, "节点类型");
        _poolTree.SetColumnTitle(1, "空闲");
        _poolTree.SetColumnTitle(2, "活动");
        _poolTree.SetColumnTitle(3, "空闲容量");
        _poolTree.SetColumnExpand(0, true);
        for (int column = 1; column < 4; column++)
        {
            _poolTree.SetColumnTitleAlignment(column, HorizontalAlignment.Center);
            _poolTree.SetColumnExpand(column, false);
            _poolTree.SetColumnCustomMinimumWidth(column, 68);
        }

        _poolActiveRentalsTree.SetColumnTitle(0, "Pool");
        _poolActiveRentalsTree.SetColumnTitle(1, "节点");
        _poolActiveRentalsTree.SetColumnTitle(2, "当前父节点");
        _poolActiveRentalsTree.SetColumnTitle(3, "状态");
        _poolActiveRentalsTree.SetColumnTitle(4, "租借");
        _poolActiveRentalsTree.SetColumnExpand(0, true);
        _poolActiveRentalsTree.SetColumnExpand(1, true);
        _poolActiveRentalsTree.SetColumnExpand(2, true);
        _poolActiveRentalsTree.SetColumnExpand(3, false);
        _poolActiveRentalsTree.SetColumnExpand(4, false);
        _poolActiveRentalsTree.SetColumnCustomMinimumWidth(3, 72);
        _poolActiveRentalsTree.SetColumnCustomMinimumWidth(4, 72);
        for (int column = 3; column < 5; column++)
            _poolActiveRentalsTree.SetColumnTitleAlignment(column, HorizontalAlignment.Center);
    }

    private T GetPoolNode<T>(string path) where T : Node
    {
        T? node = _poolDashboard!.GetNodeOrNull<T>(path);
        return IsInstanceValid(node)
            ? node
            : throw new InvalidOperationException($"DebuggerPool 场景缺少节点：{path}");
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
        _uiStackTree.SetColumnTitle(5, "来源");
        _uiStackTree.SetColumnTitle(6, "存活");
        _uiStackTree.SetColumnTitleAlignment(0, HorizontalAlignment.Center);
        _uiStackTree.SetColumnTitleAlignment(1, HorizontalAlignment.Center);
        _uiStackTree.SetColumnTitleAlignment(2, HorizontalAlignment.Left);
        _uiStackTree.SetColumnTitleAlignment(3, HorizontalAlignment.Left);
        _uiStackTree.SetColumnTitleAlignment(4, HorizontalAlignment.Center);
        _uiStackTree.SetColumnTitleAlignment(5, HorizontalAlignment.Left);
        _uiStackTree.SetColumnTitleAlignment(6, HorizontalAlignment.Right);
        _uiStackTree.SetColumnExpand(0, false);
        _uiStackTree.SetColumnExpand(1, false);
        _uiStackTree.SetColumnExpand(2, true);
        _uiStackTree.SetColumnExpand(3, true);
        _uiStackTree.SetColumnExpand(4, false);
        _uiStackTree.SetColumnExpand(5, true);
        _uiStackTree.SetColumnExpand(6, false);
        _uiStackTree.SetColumnCustomMinimumWidth(0, 58);
        _uiStackTree.SetColumnCustomMinimumWidth(1, 54);
        _uiStackTree.SetColumnCustomMinimumWidth(4, 88);
        _uiStackTree.SetColumnCustomMinimumWidth(6, 72);
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
        _procedureCurrentTitle = GetProcedureNode<Label>("Summary/CurrentCard/Content/Title");
        _procedureStateTitle = GetProcedureNode<Label>("Summary/StateCard/Content/Title");
        _procedurePendingTitle = GetProcedureNode<Label>("Summary/PendingCard/Content/Title");
        _procedureResultTitle = GetProcedureNode<Label>("Summary/ResultCard/Content/Title");
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
        _sceneStateValue.Text = GetScenePhaseText(snapshot.CurrentPhase);
        AddSceneDetail(root, "正在加载", snapshot.CurrentChangeKey?.Value ?? "—");
        AddSceneDetail(root, "当前阶段", GetScenePhaseText(snapshot.CurrentPhase));
        AddSceneDetail(root, "当前耗时", snapshot.CurrentChangeKey.HasValue
            ? FormatAgeMilliseconds(snapshot.CurrentDurationMilliseconds)
            : "—");
        AddSceneDetail(root, "最近切换", snapshot.LastChangeKey?.Value ?? "—");
        AddSceneDetail(root, "最近阶段", snapshot.LastChangeKey.HasValue
            ? GetScenePhaseText(snapshot.LastPhase)
            : "—");
        AddSceneDetail(root, "最近结果", GetSceneResultText(snapshot.LastResult));
        AddSceneDetail(root, "最近耗时", snapshot.LastChangeKey.HasValue
            ? $"{snapshot.LastDurationMilliseconds.ToString(CultureInfo.InvariantCulture)} ms"
            : "—");
        AddSceneDetail(root, "最近详情", snapshot.LastDetail ?? "—");
    }

    private static string GetScenePhaseText(SceneDebugPhase phase) => phase switch
    {
        SceneDebugPhase.Loading => "加载中",
        SceneDebugPhase.Instantiating => "实例化中",
        SceneDebugPhase.Committing => "提交中",
        _ => "空闲",
    };

    private static string GetSceneResultText(SceneDebugResult result) => result switch
    {
        SceneDebugResult.Succeeded => "成功",
        SceneDebugResult.CallerCanceled => "调用方取消",
        SceneDebugResult.LifecycleCanceled => "生命周期取消",
        SceneDebugResult.Failed => "失败",
        _ => "—",
    };

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
            item.SetText(5, FormatAgeMilliseconds(entry.AgeMilliseconds));
            item.SetTooltipText(0, entry.Key.Value);
            for (int column = 1; column < 6; column++)
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

    private void RefreshPoolPage()
    {
        NodePoolDebugEntry[] entries = NodePoolDebugRegistry.GetSnapshot();
        NodePoolDebugActiveEntry[] activeEntries = NodePoolDebugRegistry.GetActiveSnapshot();
        if (!IsInstanceValid(_poolRegisteredValue) ||
            !IsInstanceValid(_poolIdleValue) ||
            !IsInstanceValid(_poolActiveValue) ||
            !IsInstanceValid(_poolStatus) ||
            !IsInstanceValid(_poolTree) ||
            !IsInstanceValid(_poolActiveRentalsStatus) ||
            !IsInstanceValid(_poolActiveRentalsTree))
            return;

        int idleCount = 0;
        int activeCount = 0;
        _poolTree.Clear();
        TreeItem root = _poolTree.CreateItem();
        for (int index = 0; index < entries.Length; index++)
        {
            NodePoolDebugEntry entry = entries[index];
            idleCount += entry.IdleCount;
            activeCount += entry.ActiveCount;
            TreeItem item = _poolTree.CreateItem(root);
            item.SetText(0, entry.NodeTypeName);
            item.SetText(1, entry.IdleCount.ToString(CultureInfo.InvariantCulture));
            item.SetText(2, entry.ActiveCount.ToString(CultureInfo.InvariantCulture));
            item.SetText(3, entry.IdleCapacity.ToString(CultureInfo.InvariantCulture));
            item.SetTooltipText(0, entry.NodeTypeName);
            for (int column = 1; column < 4; column++)
                item.SetTextAlignment(column, HorizontalAlignment.Center);
        }

        _poolRegisteredValue.Text = entries.Length.ToString(CultureInfo.InvariantCulture);
        _poolIdleValue.Text = idleCount.ToString(CultureInfo.InvariantCulture);
        _poolActiveValue.Text = activeCount.ToString(CultureInfo.InvariantCulture);
        _poolStatus.Text = entries.Length == 0
            ? "当前没有已登记的 NodePool。"
            : "仅显示仍存活的 Debug 注册；Dispose 后立即移除。";

        _poolActiveRentalsStatus.Text = activeCount <= NodePoolDebugRegistry.MaxActiveEntries
            ? $"活动租借 {activeEntries.Length} / 上限 {NodePoolDebugRegistry.MaxActiveEntries}"
            : $"活动租借 {activeCount}，显示前 {activeEntries.Length}";
        _poolActiveRentalsTree.Clear();
        TreeItem activeRoot = _poolActiveRentalsTree.CreateItem();
        for (int index = 0; index < activeEntries.Length; index++)
        {
            NodePoolDebugActiveEntry entry = activeEntries[index];
            TreeItem item = _poolActiveRentalsTree.CreateItem(activeRoot);
            item.SetText(0, entry.NodeTypeName);
            item.SetText(1, $"{entry.NodeName} #{entry.NodeInstanceId}");
            item.SetText(2, entry.ParentInstanceId == 0
                ? "—"
                : $"{entry.ParentName} #{entry.ParentInstanceId}");
            item.SetText(3, FormatPoolActiveStatus(entry.Status));
            item.SetText(4, FormatPoolAge(entry.Age));
            item.SetTooltipText(0, entry.ScenePath);
            item.SetTooltipText(1, $"{entry.NodeName} #{entry.NodeInstanceId}");
            item.SetTooltipText(2,
                string.IsNullOrEmpty(entry.ParentPath) ? "无当前父节点" : entry.ParentPath);
            item.SetTextAlignment(3, HorizontalAlignment.Center);
            item.SetTextAlignment(4, HorizontalAlignment.Center);
        }
    }

    private static string FormatPoolActiveStatus(NodePoolDebugActiveStatus status) => status switch
    {
        NodePoolDebugActiveStatus.Active => "活动",
        NodePoolDebugActiveStatus.Detached => "已脱离",
        NodePoolDebugActiveStatus.QueuedForDeletion => "等待删除",
        NodePoolDebugActiveStatus.Invalid => "已失效",
        _ => status.ToString(),
    };

    private static string FormatPoolAge(TimeSpan age)
    {
        if (age.TotalSeconds >= 1d)
            return $"{age.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s";

        return $"{Math.Max(0d, Math.Round(age.TotalMilliseconds)).ToString("0", CultureInfo.InvariantCulture)} ms";
    }

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
        {
            RefreshDataTableLoadingAges();
            return;
        }

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
        ulong snapshotTicks = Time.GetTicksMsec();
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
            if (entry.State == DataTableDebugState.Loading)
            {
                dataSetItem.SetMetadata(0, (long)entry.AgeMilliseconds);
                dataSetItem.SetMetadata(1, (long)snapshotTicks);
                dataSetItem.SetMetadata(4, dataSetDetail);
                dataSetItem.SetText(
                    4,
                    $"{dataSetDetail} · 存活 {FormatAgeMilliseconds(entry.AgeMilliseconds)}");
            }
            else
            {
                dataSetItem.SetText(4, dataSetDetail);
            }
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

    private void RefreshDataTableLoadingAges()
    {
        TreeItem? item = _dataTableDataSetTree!.GetRoot()?.GetFirstChild();
        if (item is null)
            return;

        ulong currentTicks = Time.GetTicksMsec();
        while (item is not null)
        {
            Variant ageMetadata = item.GetMetadata(0);
            Variant ticksMetadata = item.GetMetadata(1);
            Variant detailMetadata = item.GetMetadata(4);
            if (ageMetadata.VariantType == Variant.Type.Int &&
                ticksMetadata.VariantType == Variant.Type.Int &&
                detailMetadata.VariantType == Variant.Type.String)
            {
                ulong baselineAge = (ulong)Math.Max(0L, ageMetadata.AsInt64());
                ulong baselineTicks = (ulong)Math.Max(0L, ticksMetadata.AsInt64());
                ulong age = baselineAge + currentTicks - baselineTicks;
                item.SetText(
                    4,
                    $"{detailMetadata.AsString()} · 存活 {FormatAgeMilliseconds(age)}");
            }

            item = item.GetNext();
        }
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
        int openingRequestCount = snapshot.TotalOpeningRequestCount;
        UiDebugEntry? current = null;

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
        int totalRowCount = snapshot.TotalOpeningRequestCount + snapshot.Entries.Length;
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
            item.SetText(4, GetUiOpeningPhaseText(opening.Phase));
            item.SetText(5, opening.SourceDisplayName);
            item.SetText(6, FormatAgeMilliseconds(opening.AgeMilliseconds));
            item.SetTooltipText(
                2,
                opening.Id.IsValid
                    ? opening.Id.Value
                    : "通过 ResourceKey 直接打开");
            item.SetTooltipText(3, opening.Key.Value);
            item.SetTooltipText(4, $"异步打开请求尚未完成：{GetUiOpeningPhaseText(opening.Phase)}");
            item.SetTooltipText(5, opening.SourceFullName);
            item.SetTextAlignment(0, HorizontalAlignment.Center);
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTextAlignment(4, HorizontalAlignment.Center);
            item.SetTextAlignment(6, HorizontalAlignment.Right);
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
            item.SetText(5, "—");
            item.SetText(6, "—");
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

        if (snapshot.LastResult != UiDebugOpenResult.None)
        {
            TreeItem item = _uiStackTree.CreateItem(root);
            string target = snapshot.LastId.IsValid ? snapshot.LastId.Value : "Direct";
            string result = GetUiOpenResultText(snapshot.LastResult);
            string detail = $"{GetUiOpeningPhaseText(snapshot.LastPhase)} · " +
                $"{snapshot.LastDurationMilliseconds.ToString(CultureInfo.InvariantCulture)} ms";
            if (!string.IsNullOrEmpty(snapshot.LastDetail))
                detail += $" · {snapshot.LastDetail}";
            item.SetText(0, "诊断");
            item.SetText(1, snapshot.LastLayer.ToString());
            item.SetText(2, target);
            item.SetText(3, snapshot.LastKey.Value);
            item.SetText(4, result);
            item.SetText(5, "—");
            item.SetText(
                6,
                $"{snapshot.LastDurationMilliseconds.ToString(CultureInfo.InvariantCulture)} ms");
            item.SetTooltipText(4, detail);
            item.SetTextAlignment(0, HorizontalAlignment.Center);
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTextAlignment(4, HorizontalAlignment.Center);
            item.SetCustomColor(
                4,
                snapshot.LastResult == UiDebugOpenResult.Succeeded
                    ? new Color(0.45f, 0.88f, 0.62f)
                    : new Color(1f, 0.56f, 0.36f));
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

    private static string GetUiOpeningPhaseText(UiDebugOpenPhase phase) => phase switch
    {
        UiDebugOpenPhase.Preparing => "准备中",
        UiDebugOpenPhase.Committing => "提交中",
        _ => "加载中",
    };

    private static string FormatAgeMilliseconds(ulong ageMilliseconds) =>
        ageMilliseconds < 1000
            ? $"{ageMilliseconds.ToString(CultureInfo.InvariantCulture)} ms"
            : $"{(ageMilliseconds / 1000d).ToString("0.0", CultureInfo.InvariantCulture)} s";

    private static string GetUiOpenResultText(UiDebugOpenResult result) => result switch
    {
        UiDebugOpenResult.Succeeded => "成功",
        UiDebugOpenResult.CallerCanceled => "调用方取消",
        UiDebugOpenResult.ServiceCanceled => "服务取消",
        UiDebugOpenResult.LifecycleCanceled => "生命周期取消",
        UiDebugOpenResult.Failed => "失败",
        _ => "—",
    };

    private void RefreshProcedurePage()
    {
        SetProcedureCardTitles("当前流程", "切换状态", "待处理请求", "失败记录");
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
        _procedureResultValue.Text = GetProcedureResultText(snapshot.LastResult);
        _procedureDetailsTree.Clear();
        TreeItem root = _procedureDetailsTree.CreateItem();
        AddProcedureDetail(root, "上一个流程", snapshot.PreviousName ?? "—");
        AddProcedureDetail(root, "切换目标", snapshot.TargetName ?? "—");
        AddProcedureDetail(root, "当前耗时", snapshot.Phase == ProcedureDebugPhase.Idle
            ? "—"
            : FormatAgeMilliseconds(snapshot.CurrentDurationMilliseconds));
        AddProcedureDetail(root, "激活 Context", snapshot.HasActiveContext ? "有效" : "无");
        AddProcedureDetail(root, "待清理项", snapshot.CleanupCount.ToString());
        AddProcedureDetail(root, "最近成功", snapshot.LastSucceededName ?? "—");
        AddProcedureDetail(root, "最近阶段", GetProcedurePhaseText(snapshot.LastPhase));
        AddProcedureDetail(root, "最近结果", GetProcedureResultText(snapshot.LastResult));
        AddProcedureDetail(
            root,
            "最近耗时",
            snapshot.LastResult == ProcedureDebugResult.None
                ? "—"
                : $"{snapshot.LastDurationMilliseconds} ms");
        AddProcedureDetail(root, "最近详情", snapshot.LastFailure ?? "—");
        AddProcedureDetail(root, "最近拒绝请求", snapshot.LastRejectedRequestName ?? "—");
        AddProcedureDetail(root, "请求拒绝原因", snapshot.LastRequestRejection ?? "—");
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

    private void RefreshExecutionFlowPage()
    {
        SetProcedureCardTitles("Procedure", "Scene", "UI", "健康状态");
        if (!Services.TryGet<IProcedureService>(out IProcedureService? procedureContract) ||
            procedureContract is not ProcedureService procedureService ||
            !Services.TryGet<ISceneService>(out ISceneService? sceneContract) ||
            sceneContract is not SceneService sceneService ||
            !Services.TryGet<IUiService>(out IUiService? uiContract) ||
            uiContract is not UiService uiService)
        {
            SetProcedureUnavailable("不可用", "运行链路诊断需要内置 ProcedureService、SceneService 与 UiService");
            return;
        }

        ProcedureDebugSnapshot procedure = procedureService.GetDebugSnapshot();
        SceneDebugSnapshot scene = sceneService.GetDebugSnapshot();
        UiDebugSnapshot ui = uiService.GetDebugSnapshot();
        int openUiCount = 0;
        int openingUiCount = ui.TotalOpeningRequestCount;
        for (int index = 0; index < ui.Entries.Length; index++)
        {
            if (!ui.Entries[index].IsCached)
                openUiCount++;
        }

        bool isChanging = procedureContract.IsChanging ||
            sceneContract.IsChanging ||
            openingUiCount > 0;
        bool hasRecentFailure =
            procedure.LastResult is ProcedureDebugResult.Rejected or ProcedureDebugResult.Failed ||
            scene.LastResult == SceneDebugResult.Failed ||
            ui.LastResult == UiDebugOpenResult.Failed;
        bool hasRecentCancellation =
            procedure.LastResult == ProcedureDebugResult.LifecycleCanceled ||
            scene.LastResult is SceneDebugResult.CallerCanceled or SceneDebugResult.LifecycleCanceled ||
            ui.LastResult is UiDebugOpenResult.CallerCanceled or
                UiDebugOpenResult.ServiceCanceled or UiDebugOpenResult.LifecycleCanceled;

        _procedureCurrentValue!.Text = procedure.CurrentName ??
            (procedureContract.IsChanging ? procedure.TargetName : null) ??
            "空闲";
        Node? currentScene = GetTree().CurrentScene;
        _procedureStateValue!.Text = scene.CurrentChangeKey?.Value ??
            (IsInstanceValid(currentScene) ? currentScene!.Name : "空闲");
        _procedurePendingValue!.Text = openingUiCount == 0
            ? openUiCount.ToString(CultureInfo.InvariantCulture)
            : $"{openUiCount.ToString(CultureInfo.InvariantCulture)} + {openingUiCount.ToString(CultureInfo.InvariantCulture)}";
        _procedureResultValue!.Text = isChanging
            ? "切换中"
            : hasRecentFailure
                ? "最近失败"
                : hasRecentCancellation
                    ? "最近取消"
                    : "正常";

        _procedureDetailsTree!.Clear();
        TreeItem root = _procedureDetailsTree.CreateItem();
        AddProcedureDetail(root, "Procedure 状态", GetProcedurePhaseText(procedure.Phase));
        AddProcedureDetail(root, "Procedure 目标", procedure.TargetName ?? "—");
        AddProcedureDetail(root, "Procedure 待处理", procedure.PendingName ?? "—");
        AddProcedureDetail(root, "Procedure 最近结果", GetProcedureResultText(procedure.LastResult));
        AddProcedureDetail(root, "Procedure 详情", procedure.LastFailure ?? "—");
        AddProcedureDetail(
            root,
            "Procedure 最近拒绝",
            procedure.LastRejectedRequestName is null
                ? "—"
                : $"{procedure.LastRejectedRequestName} · {procedure.LastRequestRejection}");
        AddProcedureDetail(root, "Scene 阶段", GetScenePhaseText(scene.CurrentPhase));
        AddProcedureDetail(root, "Scene 目标", scene.CurrentChangeKey?.Value ?? "—");
        AddProcedureDetail(root, "Scene 进度", $"{sceneContract.Progress * 100f:0}%");
        AddProcedureDetail(root, "Scene 最近结果", GetSceneResultText(scene.LastResult));
        AddProcedureDetail(root, "Scene 详情", scene.LastDetail ?? "—");
        AddProcedureDetail(root, "UI 已打开", openUiCount.ToString(CultureInfo.InvariantCulture));
        AddProcedureDetail(root, "UI 打开中", openingUiCount.ToString(CultureInfo.InvariantCulture));
        AddProcedureDetail(root, "UI 最近结果", GetUiOpenResultText(ui.LastResult));
        AddProcedureDetail(root, "UI 详情", ui.LastDetail ?? "—");
    }

    private void SetProcedureCardTitles(
        string current,
        string state,
        string pending,
        string result)
    {
        _procedureCurrentTitle!.Text = current;
        _procedureStateTitle!.Text = state;
        _procedurePendingTitle!.Text = pending;
        _procedureResultTitle!.Text = result;
    }

    private void AddProcedureDetail(TreeItem root, string name, string value)
    {
        TreeItem item = _procedureDetailsTree!.CreateItem(root);
        item.SetText(0, name);
        item.SetText(1, value);
        item.SetTooltipText(1, value);
    }

    private static string GetProcedurePhaseText(ProcedureDebugPhase phase) => phase switch
    {
        ProcedureDebugPhase.Exiting => "退出",
        ProcedureDebugPhase.Entering => "进入",
        _ => "空闲",
    };

    private static string GetProcedureResultText(ProcedureDebugResult result) => result switch
    {
        ProcedureDebugResult.Succeeded => "成功",
        ProcedureDebugResult.Rejected => "已拒绝",
        ProcedureDebugResult.LifecycleCanceled => "生命周期取消",
        ProcedureDebugResult.Failed => "失败",
        _ => "—",
    };


}
#endif
