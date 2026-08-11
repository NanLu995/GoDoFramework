using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的一次性、重复和异步主线程时间调度服务。</summary>
/// <remarks>
/// 通过 <see cref="Services.Get{T}"/> 获取实现。除取消令牌可从后台线程触发外，
/// 所有成员都必须在 GoDoRuntime 所在的 Godot 主线程调用。
/// </remarks>
public interface ISchedulerService
{
    /// <summary>在指定延迟后执行一次回调。</summary>
    /// <param name="delaySeconds">任务自身时钟中的延迟秒数；必须有限且不小于 0。</param>
    /// <param name="callback">到期后在所选 Godot 主线程阶段执行的回调。</param>
    /// <param name="options">时钟、派发阶段和可选场景 Owner。</param>
    /// <returns>用于查询、暂停或取消任务的不透明句柄。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 延迟或选项值无效，或者计算出的到期时间超出有限 <see cref="double"/> 范围。
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为 null。</exception>
    /// <exception cref="ArgumentException">Owner 已失效。</exception>
    /// <exception cref="InvalidOperationException">
    /// 调用线程或服务生命周期无效、Owner 尚未进入场景树，或者内部句柄空间已耗尽。
    /// </exception>
    /// <exception cref="ObjectDisposedException">Scheduler 已永久关闭。</exception>
    ScheduleHandle Schedule(
        double delaySeconds,
        Action callback,
        ScheduleOptions options = default);

    /// <summary>每隔指定时间执行回调；首次执行也等待一个完整间隔。</summary>
    /// <param name="intervalSeconds">首次和后续执行之间的间隔秒数；必须有限且大于 0。</param>
    /// <param name="callback">每次到期时在所选 Godot 主线程阶段执行的回调。</param>
    /// <param name="options">时钟、派发阶段和可选场景 Owner。</param>
    /// <returns>用于查询、暂停或取消重复任务的不透明句柄。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 间隔或选项值无效，或者计算出的到期时间超出有限 <see cref="double"/> 范围。
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为 null。</exception>
    /// <exception cref="ArgumentException">Owner 已失效。</exception>
    /// <exception cref="InvalidOperationException">
    /// 调用线程或服务生命周期无效、Owner 尚未进入场景树，或者内部句柄空间已耗尽。
    /// </exception>
    /// <exception cref="ObjectDisposedException">Scheduler 已永久关闭。</exception>
    ScheduleHandle ScheduleRepeating(
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default);

    /// <summary>等待初始延迟后，每隔指定时间执行回调。</summary>
    /// <param name="initialDelaySeconds">首次执行前的延迟秒数；必须有限且不小于 0。</param>
    /// <param name="intervalSeconds">后续执行之间的间隔秒数；必须有限且大于 0。</param>
    /// <param name="callback">每次到期时在所选 Godot 主线程阶段执行的回调。</param>
    /// <param name="options">时钟、派发阶段和可选场景 Owner。</param>
    /// <returns>用于查询、暂停或取消重复任务的不透明句柄。</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 延迟、间隔或选项值无效，或者计算出的到期时间超出有限 <see cref="double"/> 范围。
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为 null。</exception>
    /// <exception cref="ArgumentException">Owner 已失效。</exception>
    /// <exception cref="InvalidOperationException">
    /// 调用线程或服务生命周期无效、Owner 尚未进入场景树，或者内部句柄空间已耗尽。
    /// </exception>
    /// <exception cref="ObjectDisposedException">Scheduler 已永久关闭。</exception>
    ScheduleHandle ScheduleRepeating(
        double initialDelaySeconds,
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default);

    /// <summary>异步等待指定时间；取消和框架退出以取消状态结束。</summary>
    /// <param name="delaySeconds">任务自身时钟中的等待秒数；必须有限且不小于 0。</param>
    /// <param name="options">时钟、派发阶段和可选场景 Owner。</param>
    /// <param name="cancellationToken">
    /// 可选取消令牌；可从任意线程触发，实际取消在下一次 Scheduler 主线程更新中生效。
    /// </param>
    /// <returns>
    /// 到期时完成的任务；令牌取消、Owner 退出或 Scheduler 关闭时以取消状态完成。
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 延迟或选项值无效，或者计算出的到期时间超出有限 <see cref="double"/> 范围。
    /// </exception>
    /// <exception cref="ArgumentException">Owner 已失效。</exception>
    /// <exception cref="InvalidOperationException">
    /// 调用线程或服务生命周期无效、Owner 尚未进入场景树，或者内部句柄空间已耗尽。
    /// </exception>
    /// <exception cref="ObjectDisposedException">Scheduler 已永久关闭。</exception>
    Task DelayAsync(
        double delaySeconds,
        ScheduleOptions options = default,
        CancellationToken cancellationToken = default);

    /// <summary>取消活动任务；句柄无效或任务已结束时返回 false。</summary>
    /// <param name="handle">要取消的任务句柄。</param>
    /// <returns>成功取消活动或独立暂停任务时为 true；否则为 false。</returns>
    /// <exception cref="InvalidOperationException">调用线程或服务生命周期无效。</exception>
    bool Cancel(ScheduleHandle handle);

    /// <summary>独立暂停活动任务并保存剩余时间；无法暂停时返回 false。</summary>
    /// <param name="handle">要独立暂停的任务句柄。</param>
    /// <returns>
    /// 成功暂停活动任务时为 true；句柄无效、任务已结束、已经暂停或当前状态不允许暂停时为 false。
    /// </returns>
    /// <exception cref="InvalidOperationException">调用线程或服务生命周期无效。</exception>
    bool Pause(ScheduleHandle handle);

    /// <summary>恢复独立暂停的任务；无法恢复时返回 false。</summary>
    /// <param name="handle">要恢复的任务句柄。</param>
    /// <returns>成功恢复独立暂停任务时为 true；否则为 false。</returns>
    /// <exception cref="ArgumentOutOfRangeException">恢复后计算出的到期时间超出有限 double 范围。</exception>
    /// <exception cref="InvalidOperationException">调用线程或服务生命周期无效。</exception>
    bool Resume(ScheduleHandle handle);

    /// <summary>任务仍处于活动或独立暂停状态时返回 true。</summary>
    /// <param name="handle">要查询的任务句柄。</param>
    /// <returns>句柄对应任务仍由 Scheduler 管理时为 true；否则为 false。</returns>
    /// <exception cref="InvalidOperationException">调用线程或服务生命周期无效。</exception>
    bool IsScheduled(ScheduleHandle handle);

    /// <summary>尝试取得任务自身时钟中的剩余秒数。</summary>
    /// <param name="handle">要查询的任务句柄。</param>
    /// <param name="remainingSeconds">
    /// 成功时为不小于 0 的剩余秒数；失败时为 0。执行中的任务也返回 0。
    /// </param>
    /// <returns>句柄对应任务仍由 Scheduler 管理时为 true；否则为 false。</returns>
    /// <exception cref="InvalidOperationException">调用线程或服务生命周期无效。</exception>
    bool TryGetRemainingSeconds(ScheduleHandle handle, out double remainingSeconds);
}
