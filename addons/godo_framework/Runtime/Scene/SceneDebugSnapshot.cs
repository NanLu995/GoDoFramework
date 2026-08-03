#if DEBUG
#nullable enable

namespace GoDo;

internal enum SceneDebugPhase
{
    Idle,
    Loading,
    Instantiating,
    Committing,
}

internal enum SceneDebugResult
{
    None,
    Succeeded,
    CallerCanceled,
    LifecycleCanceled,
    Failed,
}

internal readonly struct SceneDebugSnapshot
{
    public ResourceKey? CurrentChangeKey { get; }
    public SceneDebugPhase CurrentPhase { get; }
    public ResourceKey? LastChangeKey { get; }
    public SceneDebugPhase LastPhase { get; }
    public SceneDebugResult LastResult { get; }
    public string? LastDetail { get; }
    public ulong LastDurationMilliseconds { get; }

    public SceneDebugSnapshot(
        ResourceKey? currentChangeKey,
        SceneDebugPhase currentPhase,
        ResourceKey? lastChangeKey,
        SceneDebugPhase lastPhase,
        SceneDebugResult lastResult,
        string? lastDetail,
        ulong lastDurationMilliseconds)
    {
        CurrentChangeKey = currentChangeKey;
        CurrentPhase = currentPhase;
        LastChangeKey = lastChangeKey;
        LastPhase = lastPhase;
        LastResult = lastResult;
        LastDetail = lastDetail;
        LastDurationMilliseconds = lastDurationMilliseconds;
    }
}
#endif
