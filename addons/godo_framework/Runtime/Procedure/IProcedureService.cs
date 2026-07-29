using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的顶层游戏流程切换服务。</summary>
public interface IProcedureService
{
    /// <summary>当前已成功进入的流程；无流程或进入失败后为 null。</summary>
    IProcedure? Current { get; }

    /// <summary>当前是否正在切换流程。</summary>
    bool IsChanging { get; }

    /// <summary>
    /// 退出当前流程并进入目标流程。
    /// <para>切换失败或服务关闭时抛出 <see cref="ProcedureChangeException"/>；服务关闭导致的失败以 <see cref="System.OperationCanceledException"/> 作为内部异常。</para>
    /// </summary>
    Task ChangeAsync(IProcedure next);

    /// <summary>
    /// 在验证 Godot 主线程后创建并进入无参构造的目标流程。
    /// <para>失败语义与 <see cref="ChangeAsync(IProcedure)"/> 相同。</para>
    /// </summary>
    Task ChangeAsync<TProcedure>() where TProcedure : IProcedure, new();
}
