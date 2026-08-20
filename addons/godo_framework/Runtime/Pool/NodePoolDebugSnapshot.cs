using System;
using System.Collections.Generic;

#nullable enable

#if DEBUG
namespace GoDo;

internal readonly struct NodePoolDebugEntry
{
    public string NodeTypeName { get; }
    public int IdleCount { get; }
    public int ActiveCount { get; }
    public int IdleCapacity { get; }

    public NodePoolDebugEntry(string nodeTypeName, int idleCount, int activeCount, int idleCapacity)
    {
        NodeTypeName = nodeTypeName;
        IdleCount = idleCount;
        ActiveCount = activeCount;
        IdleCapacity = idleCapacity;
    }
}

internal enum NodePoolDebugActiveStatus
{
    Active,
    Detached,
    QueuedForDeletion,
    Invalid,
}

internal readonly struct NodePoolDebugActiveEntry
{
    public string NodeTypeName { get; }
    public string ScenePath { get; }
    public string NodeName { get; }
    public ulong NodeInstanceId { get; }
    public string ParentName { get; }
    public string ParentPath { get; }
    public ulong ParentInstanceId { get; }
    public NodePoolDebugActiveStatus Status { get; }
    public TimeSpan Age { get; }

    public NodePoolDebugActiveEntry(
        string nodeTypeName,
        string scenePath,
        string nodeName,
        ulong nodeInstanceId,
        string parentName,
        string parentPath,
        ulong parentInstanceId,
        NodePoolDebugActiveStatus status,
        TimeSpan age)
    {
        NodeTypeName = nodeTypeName;
        ScenePath = scenePath;
        NodeName = nodeName;
        NodeInstanceId = nodeInstanceId;
        ParentName = parentName;
        ParentPath = parentPath;
        ParentInstanceId = parentInstanceId;
        Status = status;
        Age = age;
    }
}

internal interface INodePoolDebugSource
{
    NodePoolDebugEntry GetDebugEntry();
    void AppendDebugActiveEntries(List<NodePoolDebugActiveEntry> entries, int maximumCount);
}

internal static class NodePoolDebugRegistry
{
    internal const int MaxActiveEntries = 64;

    private static readonly List<WeakReference<INodePoolDebugSource>> Sources = new();

    internal static void Register(INodePoolDebugSource source)
    {
        Sources.Add(new WeakReference<INodePoolDebugSource>(source));
    }

    internal static void Unregister(INodePoolDebugSource source)
    {
        for (int index = Sources.Count - 1; index >= 0; index--)
        {
            if (!Sources[index].TryGetTarget(out INodePoolDebugSource? candidate) ||
                ReferenceEquals(candidate, source))
                Sources.RemoveAt(index);
        }
    }

    internal static NodePoolDebugEntry[] GetSnapshot()
    {
        var entries = new List<NodePoolDebugEntry>(Sources.Count);
        for (int index = Sources.Count - 1; index >= 0; index--)
        {
            if (!Sources[index].TryGetTarget(out INodePoolDebugSource? source))
            {
                Sources.RemoveAt(index);
                continue;
            }

            entries.Add(source.GetDebugEntry());
        }

        entries.Reverse();
        return entries.ToArray();
    }

    internal static NodePoolDebugActiveEntry[] GetActiveSnapshot()
    {
        var entries = new List<NodePoolDebugActiveEntry>(MaxActiveEntries);
        for (int index = Sources.Count - 1; index >= 0; index--)
        {
            if (!Sources[index].TryGetTarget(out _))
                Sources.RemoveAt(index);
        }

        for (int index = 0; index < Sources.Count && entries.Count < MaxActiveEntries; index++)
        {
            if (Sources[index].TryGetTarget(out INodePoolDebugSource? source))
                source.AppendDebugActiveEntries(entries, MaxActiveEntries);
        }

        entries.Sort(static (left, right) => right.Age.CompareTo(left.Age));
        return entries.ToArray();
    }
}
#endif
