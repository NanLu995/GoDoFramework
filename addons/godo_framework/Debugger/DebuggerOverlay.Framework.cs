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
        _eventsListenerSourcesStatus = GetEventsNode<Label>("ListenerSourcesStatus");
        _eventsListenerSourcesTree = GetEventsNode<Tree>("ListenerSources");
        _eventsTree.SetColumnTitle(0, "事件");
        _eventsTree.SetColumnTitle(1, "监听器");
        _eventsTree.SetColumnExpand(0, true);
        _eventsTree.SetColumnExpand(1, false);
        _eventsTree.SetColumnCustomMinimumWidth(1, 72);
        _eventsListenerSourcesTree.SetColumnTitle(0, "监听方法");
        _eventsListenerSourcesTree.SetColumnTitle(1, "方式");
        _eventsListenerSourcesTree.SetColumnTitle(2, "Owner");
        _eventsListenerSourcesTree.SetColumnTitle(3, "优先级");
        _eventsListenerSourcesTree.SetColumnTitle(4, "已注册");
        _eventsListenerSourcesTree.SetColumnExpand(0, true);
        _eventsListenerSourcesTree.SetColumnExpand(1, false);
        _eventsListenerSourcesTree.SetColumnExpand(2, true);
        _eventsListenerSourcesTree.SetColumnExpand(3, false);
        _eventsListenerSourcesTree.SetColumnExpand(4, false);
        _eventsListenerSourcesTree.SetColumnCustomMinimumWidth(1, 72);
        _eventsListenerSourcesTree.SetColumnCustomMinimumWidth(3, 64);
        _eventsListenerSourcesTree.SetColumnCustomMinimumWidth(4, 72);
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
        _selectedEventTypeName = item?.GetMetadata(0).AsString() ?? string.Empty;
        _eventsSelectionDetail.Text = item is null
            ? "选择事件查看完整类型名"
            : _selectedEventTypeName;
        RefreshEventListenerSources();
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
            !IsInstanceValid(_eventsSelectionDetail) ||
            !IsInstanceValid(_eventsListenerSourcesStatus) ||
            !IsInstanceValid(_eventsListenerSourcesTree))
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
        {
            RefreshEventListenerSources();
            return;
        }

        _eventsSnapshotSignature = snapshotSignature;
        _eventsTree.Clear();
        TreeItem root = _eventsTree.CreateItem();
        bool restoredSelection = false;
        for (int index = 0; index < events.Length; index++)
        {
            EventChannel.EventDebugEntry entry = events[index];
            if (!MatchesEventSearch(entry.EventType))
                continue;

            string eventTypeName = entry.EventType.FullName ?? entry.EventType.Name;
            TreeItem item = _eventsTree.CreateItem(root);
            item.SetText(0, entry.EventType.Name);
            item.SetText(1, entry.ListenerCount.ToString(CultureInfo.InvariantCulture));
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTooltipText(0, eventTypeName);
            item.SetMetadata(0, eventTypeName);
            if (eventTypeName == _selectedEventTypeName)
            {
                item.Select(0);
                restoredSelection = true;
            }
        }

        if (!restoredSelection)
            _selectedEventTypeName = string.Empty;
        _eventsSelectionDetail.Text = restoredSelection
            ? _selectedEventTypeName
            : "选择事件查看完整类型名";
        RefreshEventListenerSources();
    }

    private void RefreshEventListenerSources()
    {
        if (!IsInstanceValid(_eventsListenerSourcesStatus) ||
            !IsInstanceValid(_eventsListenerSourcesTree))
        {
            return;
        }

        _eventsListenerSourcesTree.Clear();
        TreeItem root = _eventsListenerSourcesTree.CreateItem();
        if (string.IsNullOrEmpty(_selectedEventTypeName))
        {
            _eventsListenerSourcesStatus.Text = "选择事件查看监听来源";
            return;
        }

        EventChannel.EventDebugEntry[] events = EventChannel.GetDebugSnapshot();
        Type? selectedEventType = null;
        int totalListenerCount = 0;
        for (int index = 0; index < events.Length; index++)
        {
            Type eventType = events[index].EventType;
            if ((eventType.FullName ?? eventType.Name) != _selectedEventTypeName)
                continue;

            selectedEventType = eventType;
            totalListenerCount = events[index].ListenerCount;
            break;
        }

        if (selectedEventType is null)
        {
            _selectedEventTypeName = string.Empty;
            _eventsListenerSourcesStatus.Text = "选择事件查看监听来源";
            return;
        }

        EventChannel.EventDebugListenerEntry[] listeners =
            EventChannel.GetDebugListenerSnapshot(selectedEventType);
        _eventsListenerSourcesStatus.Text = totalListenerCount > EventChannel.MaxDebugListenerEntries
            ? $"监听来源 {totalListenerCount}，显示前 {EventChannel.MaxDebugListenerEntries}"
            : $"监听来源 {totalListenerCount} / 上限 {EventChannel.MaxDebugListenerEntries}";

        for (int index = 0; index < listeners.Length; index++)
        {
            EventChannel.EventDebugListenerEntry entry = listeners[index];
            string owner = entry.OwnerInstanceId == 0
                ? entry.OwnerName
                : $"{entry.OwnerName} #{entry.OwnerInstanceId}";
            TreeItem item = _eventsListenerSourcesTree.CreateItem(root);
            item.SetText(0, entry.HandlerDisplayName);
            item.SetText(1, entry.RegistrationKind.ToString());
            item.SetText(2, owner);
            item.SetText(3, entry.Priority.ToString(CultureInfo.InvariantCulture));
            item.SetText(4, FormatEventListenerAge(entry.Age));
            item.SetTooltipText(0, entry.HandlerFullName);
            item.SetTooltipText(2, string.IsNullOrEmpty(entry.OwnerPath) ? owner : entry.OwnerPath);
            item.SetTextAlignment(1, HorizontalAlignment.Center);
            item.SetTextAlignment(3, HorizontalAlignment.Center);
            item.SetTextAlignment(4, HorizontalAlignment.Right);
        }
    }

    private static string FormatEventListenerAge(TimeSpan age)
    {
        return age.TotalSeconds < 1d
            ? $"{Math.Max(0d, age.TotalMilliseconds).ToString("0", CultureInfo.InvariantCulture)} ms"
            : $"{age.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s";
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
