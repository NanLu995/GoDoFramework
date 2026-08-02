#if DEBUG
namespace GoDo;

internal readonly struct UiDebugSnapshot
{
    public UiDebugEntry[] Entries { get; }

    public UiDebugSnapshot(UiDebugEntry[] entries)
    {
        Entries = entries;
    }
}

internal readonly struct UiDebugEntry
{
    public UiLayer Layer { get; }
    public int Index { get; }
    public ResourceKey Key { get; }
    public string NodeName { get; }
    public bool IsVisible { get; }
    public bool IsValid { get; }

    public UiDebugEntry(
        UiLayer layer,
        int index,
        ResourceKey key,
        string nodeName,
        bool isVisible,
        bool isValid)
    {
        Layer = layer;
        Index = index;
        Key = key;
        NodeName = nodeName;
        IsVisible = isVisible;
        IsValid = isValid;
    }
}
#endif
