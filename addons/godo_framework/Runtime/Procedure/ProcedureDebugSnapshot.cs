#if DEBUG
#nullable enable

namespace GoDo;

internal enum ProcedureDebugPhase
{
    Idle,
    Exiting,
    Entering,
}

internal readonly struct ProcedureDebugSnapshot
{
    public string? CurrentName { get; }
    public string? PreviousName { get; }
    public string? TargetName { get; }
    public string? PendingName { get; }
    public ProcedureDebugPhase Phase { get; }
    public string? LastSucceededName { get; }
    public string? LastFailure { get; }

    public ProcedureDebugSnapshot(
        string? currentName,
        string? previousName,
        string? targetName,
        string? pendingName,
        ProcedureDebugPhase phase,
        string? lastSucceededName,
        string? lastFailure)
    {
        CurrentName = currentName;
        PreviousName = previousName;
        TargetName = targetName;
        PendingName = pendingName;
        Phase = phase;
        LastSucceededName = lastSucceededName;
        LastFailure = lastFailure;
    }
}
#endif
