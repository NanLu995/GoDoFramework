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

internal interface INodePoolDebugSource
{
    NodePoolDebugEntry GetDebugEntry();
}

internal static class NodePoolDebugRegistry
{
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
}
#endif
