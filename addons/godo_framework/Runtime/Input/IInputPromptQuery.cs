using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>由支持输入提示的后端提供的非热路径只读查询能力。</summary>
public interface IInputPromptQuery
{
    /// <summary>按 Context、Action 与具体设备查询当前提示，结果顺序由后端配置稳定定义。</summary>
    /// <param name="context">要查询的已注册 Context。</param>
    /// <param name="action">要查询的已注册语义 Action。</param>
    /// <param name="device">键鼠、手柄或触摸设备；不能为 Unknown。</param>
    /// <returns>匹配绑定的只读提示列表；指定设备没有匹配槽位时为空列表。</returns>
    /// <exception cref="System.ArgumentException">ID 为默认值。</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="device"/> 未定义或为 Unknown。</exception>
    /// <exception cref="InputOperationException">Context、Action 未注册，或后端已经关闭。</exception>
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
