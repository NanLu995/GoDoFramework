#if DEBUG
namespace GoDo;

internal readonly struct UiDebugSnapshot
{
    public UiDebugEntry[] Entries { get; }
    public UiDebugOpeningEntry[] Openings { get; }

    public UiDebugSnapshot(UiDebugEntry[] entries, UiDebugOpeningEntry[] openings)
    {
        Entries = entries;
        Openings = openings;
    }
}

internal readonly struct UiDebugOpeningEntry
{
    public UiId Id { get; }
    public UiLayer Layer { get; }
    public ResourceKey Key { get; }
    public int RequestCount { get; }

    public UiDebugOpeningEntry(
        UiId id,
        UiLayer layer,
        ResourceKey key,
        int requestCount)
    {
        Id = id;
        Layer = layer;
        Key = key;
        RequestCount = requestCount;
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
