using System;

namespace GoDo;

/// <summary>输入 Action 输出值的固定类型。</summary>
public enum InputActionValueType
{
    /// <summary>开关或按钮状态。</summary>
    Bool,

    /// <summary>单轴浮点值。</summary>
    Axis1D,

    /// <summary>二维轴值。</summary>
    Axis2D,

    /// <summary>三维轴值。</summary>
    Axis3D,
}

/// <summary>输入 Action 在最近一次采样提交后的稳定阶段。</summary>
public enum InputActionStatus
{
    /// <summary>Action 当前未在求值或已终止。</summary>
    Idle,

    /// <summary>Action 已开始求值，但尚未执行。</summary>
    Ongoing,

    /// <summary>Action 当前处于已执行状态。</summary>
    Performed,
}

/// <summary>两次 InputService 采样之间观察到的离散 Action 转换。</summary>
[Flags]
public enum InputActionTransitions
{
    /// <summary>采样窗口内没有离散转换。</summary>
    None = 0,

    /// <summary>Action 开始求值。</summary>
    Started = 1 << 0,

    /// <summary>Action 在本采样窗口内至少执行一次。</summary>
    Performed = 1 << 1,

    /// <summary>Action 正常完成。</summary>
    Completed = 1 << 2,

    /// <summary>Action 在完成执行前取消。</summary>
    Cancelled = 1 << 3,
}

/// <summary>Context 入栈后与更低层 Context 的组合方式。</summary>
public enum InputContextMode
{
    /// <summary>与更低层有效 Context 同时启用。</summary>
    Overlay,

    /// <summary>屏蔽所有更低层 Context。</summary>
    Exclusive,
}

/// <summary>输入路由处理器对当前 Action 的传播决定。</summary>
public enum InputRouteResult
{
    /// <summary>继续向当前 Scope 的后续 Binding 或更低 Scope 传播。</summary>
    Pass,

    /// <summary>消费当前 Action，不再向后续 Binding 或更低 Scope 传播。</summary>
    Handled,
}

/// <summary>最近产生有效输入的设备类别。</summary>
public enum InputDeviceKind
{
    /// <summary>后端未提供或尚未观察到有效输入。</summary>
    Unknown,

    /// <summary>键盘或鼠标。</summary>
    KeyboardMouse,

    /// <summary>游戏手柄。</summary>
    Gamepad,

    /// <summary>触摸输入。</summary>
    Touch,
}

/// <summary>输入后端可选能力。</summary>
[Flags]
public enum InputBackendCapabilities
{
    /// <summary>没有可选能力。</summary>
    None = 0,

    /// <summary>支持运行时重绑定。</summary>
    Rebinding = 1 << 0,

    /// <summary>支持可靠的活动设备跟踪。</summary>
    DeviceTracking = 1 << 1,

    /// <summary>支持游戏手柄震动。</summary>
    Rumble = 1 << 2,

    /// <summary>支持可靠保存和加载运行时绑定。</summary>
    RebindingPersistence = 1 << 3,

    /// <summary>支持按 Context、Action 与设备查询当前文本提示。</summary>
    PromptQuery = 1 << 4,
}
