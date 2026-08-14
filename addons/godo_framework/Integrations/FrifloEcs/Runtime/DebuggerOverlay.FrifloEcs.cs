#if DEBUG
using System;
using System.Globalization;
using GoDo.Integrations.FrifloEcs;
using Godot;

#nullable enable

namespace GoDo;

public sealed partial class DebuggerOverlay : CanvasLayer
{
    private const string EcsDashboardNodePath = "Panel/Margin/VBox/Body/Page/EcsDashboard";
    private const int MaxDisplayedEcsWorlds = 32;
    private const int MaxDisplayedEcsSystems = 128;

    private readonly TreeItem?[] _ecsSystemItems = new TreeItem?[MaxDisplayedEcsSystems];
    private Control? _ecsDashboard;
    private Label? _ecsWorldsValue;
    private Label? _ecsRunningValue;
    private Label? _ecsEntitiesValue;
    private Label? _ecsArchetypesValue;
    private Label? _ecsWorldStatus;
    private Tree? _ecsWorldTree;
    private Label? _ecsSystemStatus;
    private Tree? _ecsSystemTree;

    partial void CacheIntegrationPages()
    {
        _ecsDashboard = GetNodeOrNull<Control>(EcsDashboardNodePath)
            ?? throw new InvalidOperationException($"DebuggerOverlay 场景缺少节点：{EcsDashboardNodePath}");
        _ecsWorldsValue = GetRequiredEcsNode<Label>("Summary/WorldsCard/Content/Value");
        _ecsRunningValue = GetRequiredEcsNode<Label>("Summary/RunningCard/Content/Value");
        _ecsEntitiesValue = GetRequiredEcsNode<Label>("Summary/EntitiesCard/Content/Value");
        _ecsArchetypesValue = GetRequiredEcsNode<Label>("Summary/ArchetypesCard/Content/Value");
        _ecsWorldStatus = GetRequiredEcsNode<Label>("WorldStatus");
        _ecsWorldTree = GetRequiredEcsNode<Tree>("WorldList");
        _ecsSystemStatus = GetRequiredEcsNode<Label>("SystemStatus");
        _ecsSystemTree = GetRequiredEcsNode<Tree>("SystemList");
        ConfigureEcsWorldTree();
        ConfigureEcsSystemTree();
    }

    partial void RegisterIntegrationPages()
    {
        RegisterPage("Runtime/ECS", "运行时", "ECS", RefreshEcsDashboard);
    }

    partial void ApplyIntegrationPageContentVisibility(DebuggerPage page, bool showReadFailure)
    {
        bool visible = string.Equals(page.Path, "Runtime/ECS", StringComparison.Ordinal) && !showReadFailure;
        if (IsInstanceValid(_ecsDashboard))
            _ecsDashboard.Visible = visible;
        _integrationPageContentVisible = visible;
    }

    private T GetRequiredEcsNode<T>(string relativePath) where T : Node
    {
        return _ecsDashboard!.GetNodeOrNull<T>(relativePath)
            ?? throw new InvalidOperationException($"ECS 调试页面缺少节点：{relativePath}");
    }

    private void ConfigureEcsWorldTree()
    {
        string[] titles = ["宿主", "阶段", "状态", "Entity", "Archetype", "容量"];
        for (int column = 0; column < titles.Length; column++)
        {
            _ecsWorldTree!.SetColumnTitle(column, titles[column]);
            _ecsWorldTree.SetColumnTitleAlignment(
                column,
                column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center);
            _ecsWorldTree.SetColumnExpand(column, column == 0);
        }

        _ecsWorldTree!.SetColumnCustomMinimumWidth(1, 68);
        _ecsWorldTree.SetColumnCustomMinimumWidth(2, 62);
        _ecsWorldTree.SetColumnCustomMinimumWidth(3, 62);
        _ecsWorldTree.SetColumnCustomMinimumWidth(4, 74);
        _ecsWorldTree.SetColumnCustomMinimumWidth(5, 62);
    }

    private void ConfigureEcsSystemTree()
    {
        string[] titles = ["System", "状态", "Entity", "Last ms", "Updates", "Last alloc"];
        for (int column = 0; column < titles.Length; column++)
        {
            _ecsSystemTree!.SetColumnTitle(column, titles[column]);
            _ecsSystemTree.SetColumnTitleAlignment(
                column,
                column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center);
            _ecsSystemTree.SetColumnExpand(column, column == 0);
        }

        _ecsSystemTree!.SetColumnCustomMinimumWidth(1, 62);
        _ecsSystemTree.SetColumnCustomMinimumWidth(2, 62);
        _ecsSystemTree.SetColumnCustomMinimumWidth(3, 72);
        _ecsSystemTree.SetColumnCustomMinimumWidth(4, 68);
        _ecsSystemTree.SetColumnCustomMinimumWidth(5, 86);
    }

    private void RefreshEcsDashboard()
    {
        EcsWorldDebugSnapshot snapshot = EcsWorldDebugRegistry.GetSnapshot(
            MaxDisplayedEcsWorlds,
            MaxDisplayedEcsSystems);
        _ecsWorldsValue!.Text = snapshot.RegisteredWorldCount.ToString(CultureInfo.InvariantCulture);
        _ecsRunningValue!.Text = snapshot.RunningWorldCount.ToString(CultureInfo.InvariantCulture);
        _ecsEntitiesValue!.Text = snapshot.TotalEntityCount.ToString(CultureInfo.InvariantCulture);
        _ecsArchetypesValue!.Text = snapshot.TotalArchetypeCount.ToString(CultureInfo.InvariantCulture);
        RefreshEcsWorlds(snapshot);
        RefreshEcsSystems(snapshot);
    }

    private void RefreshEcsWorlds(EcsWorldDebugSnapshot snapshot)
    {
        _ecsWorldTree!.Clear();
        TreeItem root = _ecsWorldTree.CreateItem();
        for (int index = 0; index < snapshot.Worlds.Length; index++)
        {
            EcsWorldDebugEntry world = snapshot.Worlds[index];
            TreeItem item = _ecsWorldTree.CreateItem(root);
            item.SetText(0, world.NodePath);
            item.SetTooltipText(0, world.NodePath);
            item.SetText(1, world.UpdatePhase == EcsUpdatePhase.Physics ? "Physics" : "Process");
            item.SetText(2, world.IsRunning ? "运行中" : "已暂停");
            item.SetText(3, world.EntityCount.ToString(CultureInfo.InvariantCulture));
            item.SetText(4, world.ArchetypeCount.ToString(CultureInfo.InvariantCulture));
            item.SetText(5, world.ArchetypeCapacity.ToString(CultureInfo.InvariantCulture));
            item.SetCustomColor(
                2,
                world.IsRunning ? new Color(0.45f, 0.88f, 0.62f) : new Color(0.96f, 0.75f, 0.32f));
        }

        _ecsWorldStatus!.Text = snapshot.RegisteredWorldCount == 0
            ? "当前没有已登记的 EcsWorldHost。"
            : snapshot.WorldsTruncated
                ? $"World {snapshot.Worlds.Length} / {snapshot.RegisteredWorldCount}（显示上限 {MaxDisplayedEcsWorlds}）"
                : $"World {snapshot.RegisteredWorldCount} · 运行中 {snapshot.RunningWorldCount}";
    }

    private void RefreshEcsSystems(EcsWorldDebugSnapshot snapshot)
    {
        _ecsSystemTree!.Clear();
        Array.Clear(_ecsSystemItems, 0, _ecsSystemItems.Length);
        TreeItem root = _ecsSystemTree.CreateItem();

        for (int worldIndex = 0; worldIndex < snapshot.Worlds.Length; worldIndex++)
        {
            EcsWorldDebugEntry world = snapshot.Worlds[worldIndex];
            TreeItem worldItem = _ecsSystemTree.CreateItem(root);
            worldItem.SetText(0, world.NodePath);
            worldItem.SetTooltipText(0, world.NodePath);
            worldItem.SetText(1, world.IsPerformanceMonitoringEnabled ? "Perf 开" : "Perf 关");
            worldItem.SetSelectable(0, false);

            int endIndex = world.SystemStartIndex + world.SystemCount;
            for (int systemIndex = world.SystemStartIndex; systemIndex < endIndex; systemIndex++)
            {
                EcsSystemDebugEntry system = snapshot.Systems[systemIndex];
                TreeItem parent = system.ParentIndex >= world.SystemStartIndex
                    ? _ecsSystemItems[system.ParentIndex] ?? worldItem
                    : worldItem;
                TreeItem item = _ecsSystemTree.CreateItem(parent);
                _ecsSystemItems[systemIndex] = item;
                item.SetText(0, string.IsNullOrWhiteSpace(system.Name) ? system.TypeName : system.Name);
                item.SetTooltipText(0, system.TypeName);
                item.SetText(1, system.IsEnabled ? "启用" : "禁用");
                item.SetText(2, system.HasEntityCount
                    ? system.EntityCount.ToString(CultureInfo.InvariantCulture)
                    : "—");
                item.SetText(3, system.HasPerformance
                    ? system.LastMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)
                    : "—");
                item.SetText(4, system.HasPerformance
                    ? system.UpdateCount.ToString(CultureInfo.InvariantCulture)
                    : "—");
                item.SetText(5, system.HasPerformance ? FormatBytes(system.LastAllocatedBytes) : "—");
                if (!system.IsEnabled)
                    item.SetCustomColor(1, new Color(0.96f, 0.75f, 0.32f));
            }
        }

        if (snapshot.RegisteredWorldCount == 0)
        {
            _ecsSystemStatus!.Text = "当前没有可显示的 ECS System。";
            return;
        }

        string truncation = snapshot.SystemsTruncated
            ? $" · 仅显示前 {MaxDisplayedEcsSystems} 个 System"
            : string.Empty;
        _ecsSystemStatus!.Text =
            $"System {snapshot.Systems.Length} · 性能列仅显示业务已启用的 Systems.SetMonitorPerf(true){truncation}";
    }
}
#endif
