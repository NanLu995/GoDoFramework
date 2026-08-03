#if DEBUG
#nullable enable

namespace GoDo;

internal enum UiDebugOpenPhase
{
    Loading,
    Preparing,
    Committing,
}

internal enum UiDebugOpenResult
{
    None,
    Succeeded,
    CallerCanceled,
    ServiceCanceled,
    LifecycleCanceled,
    Failed,
}

internal enum UiDebugOpenCancellationOrigin
{
    None,
    Caller,
    Service,
    Lifecycle,
}

internal readonly struct UiDebugSnapshot
{
    public UiDebugEntry[] Entries { get; }
    public UiDebugOpeningEntry[] Openings { get; }
    public UiId LastId { get; }
    public UiLayer LastLayer { get; }
    public ResourceKey LastKey { get; }
    public UiDebugOpenPhase LastPhase { get; }
    public UiDebugOpenResult LastResult { get; }
    public string? LastDetail { get; }
    public ulong LastDurationMilliseconds { get; }

    public UiDebugSnapshot(
        UiDebugEntry[] entries,
        UiDebugOpeningEntry[] openings,
        UiId lastId,
        UiLayer lastLayer,
        ResourceKey lastKey,
        UiDebugOpenPhase lastPhase,
        UiDebugOpenResult lastResult,
        string? lastDetail,
        ulong lastDurationMilliseconds)
    {
        Entries = entries;
        Openings = openings;
        LastId = lastId;
        LastLayer = lastLayer;
        LastKey = lastKey;
        LastPhase = lastPhase;
        LastResult = lastResult;
        LastDetail = lastDetail;
        LastDurationMilliseconds = lastDurationMilliseconds;
    }
}

internal readonly struct UiDebugOpeningEntry
{
    public UiId Id { get; }
    public UiLayer Layer { get; }
    public ResourceKey Key { get; }
    public int RequestCount { get; }
    public UiDebugOpenPhase Phase { get; }

    public UiDebugOpeningEntry(
        UiId id,
        UiLayer layer,
        ResourceKey key,
        int requestCount,
        UiDebugOpenPhase phase)
    {
        Id = id;
        Layer = layer;
        Key = key;
        RequestCount = requestCount;
        Phase = phase;
    }
}

internal readonly struct UiDebugEntry
{
    public UiLayer Layer { get; }
    public int Index { get; }
    public ResourceKey Key { get; }
    public UiId Id { get; }
    public string NodeName { get; }
    public bool IsVisible { get; }
    public bool IsValid { get; }
    public bool IsCached { get; }
    public bool HasFocus { get; }
    public string FocusNodeName { get; }

    public UiDebugEntry(
        UiLayer layer,
        int index,
        ResourceKey key,
        UiId id,
        string nodeName,
        bool isVisible,
        bool isValid,
        bool isCached,
        bool hasFocus,
        string focusNodeName)
    {
        Layer = layer;
        Index = index;
        Key = key;
        Id = id;
        NodeName = nodeName;
        IsVisible = isVisible;
        IsValid = isValid;
        IsCached = isCached;
        HasFocus = hasFocus;
        FocusNodeName = focusNodeName;
    }
}
#endif
