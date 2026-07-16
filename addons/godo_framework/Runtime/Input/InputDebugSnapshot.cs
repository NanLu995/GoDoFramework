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
    InputDebugContextEntry[] Contexts,
    InputDebugActionEntry[] Actions);

/// <summary>Context 栈中的一项 Debug-only 状态。</summary>
internal readonly record struct InputDebugContextEntry(
    InputContextId Context,
    InputContextMode Mode,
    bool IsEffective);

/// <summary>Action 的 Debug-only 当前状态。</summary>
internal readonly record struct InputDebugActionEntry(
    InputActionId Action,
    InputActionValueType ValueType,
    Vector3 Value,
    bool Pressed,
    bool JustPressed,
    bool JustReleased);
#endif
