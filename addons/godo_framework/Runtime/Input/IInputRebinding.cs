using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>由支持改键的输入后端提供的非热路径运行时重绑定能力。</summary>
public interface IInputRebinding
{
    /// <summary>当前是否正在等待玩家输入。</summary>
    bool IsCapturing { get; }

    /// <summary>查询指定 Context 中已显式公开的可重绑定槽位。</summary>
    IReadOnlyList<InputBindingInfo> GetBindings(InputContextId context);

    /// <summary>查询单个可重绑定槽位。</summary>
    InputBindingInfo GetBinding(InputBindingId binding);

    /// <summary>开始捕获与槽位值类型兼容的输入；主动取消时返回 null。</summary>
    Task<InputBindingCandidate?> CaptureAsync(InputBindingId binding);

    /// <summary>取消当前捕获；未捕获时不执行操作。</summary>
    void CancelCapture();

    /// <summary>查询应用候选输入后会产生的其他槽位冲突。</summary>
    IReadOnlyList<InputBindingInfo> FindConflicts(
        InputBindingId binding,
        InputBindingCandidate candidate);

    /// <summary>应用候选输入；不会自动修改冲突槽位。</summary>
    void Apply(InputBindingId binding, InputBindingCandidate candidate);

    /// <summary>恢复指定槽位的默认输入；不会自动修改冲突槽位。</summary>
    void RestoreDefault(InputBindingId binding);
}

/// <summary>由支持重绑定的低层输入后端实现，用于向 InputService 暴露可选能力。</summary>
public interface IInputRebindingBackend
{
    /// <summary>与当前后端共享生命周期的重绑定实现。</summary>
    IInputRebinding Rebinding { get; }
}
