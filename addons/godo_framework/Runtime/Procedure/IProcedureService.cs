using System;
using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的顶层游戏流程切换服务。</summary>
public interface IProcedureService
{
    /// <summary>
    /// 当通过 <see cref="ProcedureContext.RequestChange(IProcedure)"/> 请求的流程切换失败时触发。
    /// <para>
    /// 通知发生在切换状态复位后，因此订阅者可以发起恢复流程；直接调用 <see cref="ChangeAsync(IProcedure)"/>
    /// 产生的失败仍仅通过返回的任务传播，不会触发本事件。服务关闭导致的预期取消也不会触发本事件。
    /// </para>
    /// <para>
    /// 订阅者异常会单独报告给 ErrorHub，不会阻断其他订阅者或覆盖原始切换失败。
    /// 长期订阅者应在自身生命周期结束时取消订阅；ProcedureService 关闭时会清空剩余订阅。
    /// </para>
    /// </summary>
    event Action<ProcedureChangeException>? RequestedChangeFailed;

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
