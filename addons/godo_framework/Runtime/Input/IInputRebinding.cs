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
    /// <param name="context">已注册的非默认 Context ID。</param>
    /// <returns>按后端配置稳定排序的绑定信息；没有公开槽位时为空列表。</returns>
    /// <exception cref="InputOperationException">Context 未注册或后端已经关闭。</exception>
    IReadOnlyList<InputBindingInfo> GetBindings(InputContextId context);

    /// <summary>查询单个可重绑定槽位。</summary>
    /// <param name="binding">已公开的稳定绑定槽位 ID。</param>
    /// <returns>槽位当前绑定、默认绑定和显示信息的快照。</returns>
    /// <exception cref="InputOperationException">Binding 未注册或后端已经关闭。</exception>
    InputBindingInfo GetBinding(InputBindingId binding);

    /// <summary>开始捕获与槽位值类型兼容的输入。</summary>
    /// <param name="binding">要捕获新输入的已公开绑定槽位。</param>
    /// <returns>捕获成功时返回仅供当前后端实例使用的候选；主动取消时返回 <see langword="null"/>。</returns>
    /// <exception cref="InputOperationException">Binding 未注册、已有捕获正在执行、后端关闭或捕获失败。</exception>
    Task<InputBindingCandidate?> CaptureAsync(InputBindingId binding);

    /// <summary>取消当前捕获；未捕获时不执行操作。</summary>
    void CancelCapture();

    /// <summary>查询应用候选输入后会产生的其他槽位冲突。</summary>
    /// <param name="binding">候选将要应用到的已公开槽位。</param>
    /// <param name="candidate">由当前后端实例捕获的会话内候选。</param>
    /// <returns>会与候选输入冲突的其他绑定信息；没有冲突时为空列表。</returns>
    /// <exception cref="InputOperationException">Binding、候选或后端生命周期无效。</exception>
    IReadOnlyList<InputBindingInfo> FindConflicts(
        InputBindingId binding,
        InputBindingCandidate candidate);

    /// <summary>应用候选输入；不会自动修改冲突槽位。</summary>
    /// <param name="binding">要修改的已公开绑定槽位。</param>
    /// <param name="candidate">由当前后端实例捕获的会话内候选。</param>
    /// <exception cref="InputOperationException">Binding、候选或后端生命周期无效，或应用/回滚失败。</exception>
    void Apply(InputBindingId binding, InputBindingCandidate candidate);

    /// <summary>恢复指定槽位的默认输入；不会自动修改冲突槽位。</summary>
    /// <param name="binding">要恢复的已公开绑定槽位。</param>
    /// <exception cref="InputOperationException">Binding 未注册、后端关闭，或恢复/回滚失败。</exception>
    void RestoreDefault(InputBindingId binding);
}

/// <summary>由支持重绑定的低层输入后端实现，用于向 InputService 暴露可选能力。</summary>
public interface IInputRebindingBackend
{
    /// <summary>与当前后端共享生命周期的重绑定实现。</summary>
    IInputRebinding Rebinding { get; }
}
