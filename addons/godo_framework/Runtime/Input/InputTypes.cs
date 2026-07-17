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

/// <summary>Context 入栈后与更低层 Context 的组合方式。</summary>
public enum InputContextMode
{
    /// <summary>与更低层有效 Context 同时启用。</summary>
    Overlay,

    /// <summary>屏蔽所有更低层 Context。</summary>
    Exclusive,
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
