#if DEBUG
namespace GoDo;

internal readonly struct SceneDebugSnapshot
{
    public ResourceKey? CurrentChangeKey { get; }
    public ResourceKey? LastChangeKey { get; }
    public bool LastChangeSucceeded { get; }

    public SceneDebugSnapshot(
        ResourceKey? currentChangeKey,
        ResourceKey? lastChangeKey,
        bool lastChangeSucceeded)
    {
        CurrentChangeKey = currentChangeKey;
        LastChangeKey = lastChangeKey;
        LastChangeSucceeded = lastChangeSucceeded;
    }
}
#endif
