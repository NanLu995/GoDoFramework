using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>表示一个顶层游戏流程阶段。</summary>
public interface IProcedure
{
    /// <summary>流程名称，用于诊断和错误信息。</summary>
    string Name { get; }

    /// <summary>
    /// 进入当前流程阶段。
    /// <para>进入失败时，框架会释放通过 <paramref name="context"/> 登记的事件与清理项。</para>
    /// </summary>
    /// <param name="context">仅属于本次流程激活的服务、事件、取消令牌与清理上下文。</param>
    /// <returns>进入工作完成时结束的非空任务；异步延续必须返回 Godot 主线程。</returns>
    Task EnterAsync(ProcedureContext context);

    /// <summary>
    /// 退出当前流程阶段。
    /// <para>退出成功后，框架会释放通过 <paramref name="context"/> 登记的事件与清理项；退出失败时保持该激活有效。</para>
    /// </summary>
    /// <param name="context">当前仍然有效的流程激活上下文。</param>
    /// <returns>退出工作完成时结束的非空任务；异步延续必须返回 Godot 主线程。</returns>
    Task ExitAsync(ProcedureContext context);
}
