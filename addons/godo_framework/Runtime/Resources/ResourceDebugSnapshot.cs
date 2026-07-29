using System;

#if DEBUG
namespace GoDo;

internal readonly struct ResourceDebugSnapshot
{
    public ResourceDebugActiveEntry[] ActiveOperations { get; }
    public ResourceDebugHistoryEntry[] History { get; }
    public int SynchronousRequestCount { get; }
    public int AsynchronousRequestCount { get; }
    public int MergedRequestCount { get; }
    public int SucceededRequestCount { get; }
    public int FailedRequestCount { get; }

    public ResourceDebugSnapshot(
        ResourceDebugActiveEntry[] activeOperations,
        ResourceDebugHistoryEntry[] history,
        int synchronousRequestCount,
        int asynchronousRequestCount,
        int mergedRequestCount,
        int succeededRequestCount,
        int failedRequestCount)
    {
        ActiveOperations = activeOperations;
        History = history;
        SynchronousRequestCount = synchronousRequestCount;
        AsynchronousRequestCount = asynchronousRequestCount;
        MergedRequestCount = mergedRequestCount;
        SucceededRequestCount = succeededRequestCount;
        FailedRequestCount = failedRequestCount;
    }
}

internal readonly struct ResourceDebugActiveEntry
{
    public ResourceKey Key { get; }
    public Type ResourceType { get; }
    public ResourceLoadStatus Status { get; }
    public float Progress { get; }
    public int MergedRequestCount { get; }

    public ResourceDebugActiveEntry(
        ResourceKey key,
        Type resourceType,
        ResourceLoadStatus status,
        float progress,
        int mergedRequestCount)
    {
        Key = key;
        ResourceType = resourceType;
        Status = status;
        Progress = progress;
        MergedRequestCount = mergedRequestCount;
    }
}

internal readonly struct ResourceDebugHistoryEntry
{
    public ResourceKey Key { get; }
    public Type ResourceType { get; }
    public ResourceDebugLoadMode Mode { get; }
    public ResourceLoadStatus Status { get; }
    public int MergedRequestCount { get; }

    public ResourceDebugHistoryEntry(
        ResourceKey key,
        Type resourceType,
        ResourceDebugLoadMode mode,
        ResourceLoadStatus status,
        int mergedRequestCount)
    {
        Key = key;
        ResourceType = resourceType;
        Mode = mode;
        Status = status;
        MergedRequestCount = mergedRequestCount;
    }
}

internal enum ResourceDebugLoadMode
{
    Synchronous,
    Asynchronous,
}
#endif
