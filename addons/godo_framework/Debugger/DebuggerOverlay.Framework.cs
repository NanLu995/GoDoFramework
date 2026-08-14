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


}
#endif
