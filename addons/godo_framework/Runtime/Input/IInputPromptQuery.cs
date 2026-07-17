using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>由支持输入提示的后端提供的非热路径只读查询能力。</summary>
public interface IInputPromptQuery
{
    /// <summary>按 Context、Action 与具体设备查询当前提示，结果顺序由后端配置稳定定义。</summary>
    IReadOnlyList<InputPromptInfo> GetPrompts(
        InputContextId context,
        InputActionId action,
        InputDeviceKind device);
}

/// <summary>由支持提示查询的低层输入后端实现，用于向 InputService 暴露可选能力。</summary>
public interface IInputPromptBackend
{
    /// <summary>与当前后端共享生命周期的提示查询实现。</summary>
    IInputPromptQuery PromptQuery { get; }
}
