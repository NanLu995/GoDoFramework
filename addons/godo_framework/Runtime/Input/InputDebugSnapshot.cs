using Godot;

namespace GoDo;

#if DEBUG
/// <summary>InputService 在 Debug 构建中按需生成的只读诊断快照。</summary>
internal readonly record struct InputDebugSnapshot(
    bool IsReady,
    string BackendName,
    InputDeviceKind ActiveDevice,
    InputBackendCapabilities Capabilities,
    bool HasSample,
    ulong Sequence,
    ulong ContextRevision,
    InputDebugContextEntry[] Contexts,
    InputDebugActionEntry[] Actions,
    InputRouterDebugSnapshot Router);

/// <summary>Context 栈中的一项 Debug-only 状态。</summary>
internal readonly record struct InputDebugContextEntry(
    InputContextId Context,
    InputContextMode Mode,
    InputService.ContextEntryKind Kind,
    ulong Token,
    bool IsValid,
    bool IsEffective);

/// <summary>Action 的 Debug-only 当前状态。</summary>
internal readonly record struct InputDebugActionEntry(
    InputActionId Action,
    InputActionValueType ValueType,
    Vector3 Value,
    bool Pressed,
    bool JustPressed,
    bool JustReleased,
    InputActionStatus Status,
    InputActionTransitions Transitions,
    float ElapsedSeconds,
    float ElapsedRatio,
    bool IsRetriggerGated);

/// <summary>InputActionRouter 的 Debug-only 当前状态。</summary>
internal readonly record struct InputRouterDebugSnapshot(
    bool IsRegistered,
    ulong RouteRevision,
    ulong LastDispatchSequence,
    InputRouterDebugScopeEntry[] Scopes);

/// <summary>按实际派发优先级排列的路由 Scope。</summary>
internal readonly record struct InputRouterDebugScopeEntry(
    int Order,
    string DebugName,
    int BindingCount);
#endif
