#if DEBUG
#nullable enable

namespace GoDo;

internal enum ProcedureDebugPhase
{
    Idle,
    Exiting,
    Entering,
}

internal enum ProcedureDebugResult
{
    None,
    Succeeded,
    Rejected,
    LifecycleCanceled,
    Failed,
}

internal readonly struct ProcedureDebugSnapshot
{
    public string? CurrentName { get; }
    public string? PreviousName { get; }
    public string? TargetName { get; }
    public string? PendingName { get; }
    public ProcedureDebugPhase Phase { get; }
    public string? LastSucceededName { get; }
    public ProcedureDebugPhase LastPhase { get; }
    public ProcedureDebugResult LastResult { get; }
    public string? LastFailure { get; }
    public string? LastRejectedRequestName { get; }
    public string? LastRequestRejection { get; }
    public ulong LastDurationMilliseconds { get; }
    public bool HasActiveContext { get; }
    public int CleanupCount { get; }

    public ProcedureDebugSnapshot(
        string? currentName,
        string? previousName,
        string? targetName,
        string? pendingName,
        ProcedureDebugPhase phase,
        string? lastSucceededName,
        ProcedureDebugPhase lastPhase,
        ProcedureDebugResult lastResult,
        string? lastFailure,
        string? lastRejectedRequestName,
        string? lastRequestRejection,
        ulong lastDurationMilliseconds,
        bool hasActiveContext,
        int cleanupCount)
    {
        CurrentName = currentName;
        PreviousName = previousName;
        TargetName = targetName;
        PendingName = pendingName;
        Phase = phase;
        LastSucceededName = lastSucceededName;
        LastPhase = lastPhase;
        LastResult = lastResult;
        LastFailure = lastFailure;
        LastRejectedRequestName = lastRejectedRequestName;
        LastRequestRejection = lastRequestRejection;
        LastDurationMilliseconds = lastDurationMilliseconds;
        HasActiveContext = hasActiveContext;
        CleanupCount = cleanupCount;
    }
}
#endif
