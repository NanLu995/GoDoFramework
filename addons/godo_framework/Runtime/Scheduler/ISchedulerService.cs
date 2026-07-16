using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的一次性、重复和异步主线程时间调度服务。</summary>
public interface ISchedulerService
{
    /// <summary>在指定延迟后执行一次回调。</summary>
    ScheduleHandle Schedule(
        double delaySeconds,
        Action callback,
        ScheduleOptions options = default);

    /// <summary>每隔指定时间执行回调；首次执行也等待一个完整间隔。</summary>
    ScheduleHandle ScheduleRepeating(
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default);

    /// <summary>等待初始延迟后，每隔指定时间执行回调。</summary>
    ScheduleHandle ScheduleRepeating(
        double initialDelaySeconds,
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default);

    /// <summary>异步等待指定时间；取消和框架退出以取消状态结束。</summary>
    Task DelayAsync(
        double delaySeconds,
        ScheduleOptions options = default,
        CancellationToken cancellationToken = default);

    /// <summary>取消活动任务；句柄无效或任务已结束时返回 false。</summary>
    bool Cancel(ScheduleHandle handle);

    /// <summary>独立暂停活动任务并保存剩余时间；无法暂停时返回 false。</summary>
    bool Pause(ScheduleHandle handle);

    /// <summary>恢复独立暂停的任务；无法恢复时返回 false。</summary>
    bool Resume(ScheduleHandle handle);

    /// <summary>任务仍处于活动或独立暂停状态时返回 true。</summary>
    bool IsScheduled(ScheduleHandle handle);

    /// <summary>尝试取得任务自身时钟中的剩余秒数。</summary>
    bool TryGetRemainingSeconds(ScheduleHandle handle, out double remainingSeconds);
}
