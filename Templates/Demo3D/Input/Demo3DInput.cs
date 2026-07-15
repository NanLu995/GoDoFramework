using GoDo;

namespace Demo3D;

/// <summary>Demo3D 业务代码使用的稳定输入语义 ID。</summary>
internal static class Demo3DInput
{
    internal static readonly InputActionId Move = InputActionId.Create("gameplay.move");
    internal static readonly InputActionId Look = InputActionId.Create("gameplay.look");
    internal static readonly InputActionId Jump = InputActionId.Create("gameplay.jump");
    internal static readonly InputActionId ReleasePointer = InputActionId.Create("gameplay.release_pointer");

    internal static readonly InputContextId Gameplay = InputContextId.Create("gameplay");
    internal static readonly InputContextId Result = InputContextId.Create("result");
}
