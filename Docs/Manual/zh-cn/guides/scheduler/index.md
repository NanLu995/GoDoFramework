# 安排延迟、循环任务与异步等待

SchedulerService 在 Godot 主线程上统一处理一次性延迟、重复回调和可取消等待。它把“游戏慢动作时是否变慢”“暂停菜单打开时是否继续”“回调在 Process 还是 Physics 执行”变成明确选项，并能在场景 Owner 离开时自动取消任务。

它不会为每项任务创建 `Timer` Node。完全属于单个场景、适合在 Inspector 配置的简单计时，继续使用 Godot `Timer` 会更直观。

## 什么时候使用 Scheduler

适合：

- 技能冷却、倒计时、延迟关闭提示和周期性检查。
- 暂停时继续运行的 UI 或连接超时。
- Procedure 中可取消的异步等待。
- 场景退出时必须自动清理的任务。

不适合：

- 确定性 Tick、网络同步时钟或战斗回放。
- Tween、AnimationPlayer 或音频采样级定时。
- 后台线程工作、日历提醒、离线收益和跨存档计时。

## 1. 选择正确的时钟

| 时钟 | 受 `Engine.TimeScale` 影响 | SceneTree 暂停时推进 | 常见用途 |
|---|---:|---:|---|
| `GameTime` | 是 | 否 | 技能冷却、玩法倒计时 |
| `UnscaledGameTime` | 否 | 否 | 不受慢动作影响、但随游戏暂停的逻辑 |
| `RealTime` | 否 | 是 | 暂停菜单动画、连接超时、调试提示 |

默认选项是 `GameTime + Process`：

```csharp
ISchedulerService scheduler = Services.Get<ISchedulerService>();

scheduler.Schedule(
    0.5,
    () => GD.Print("半秒后执行"));
```

暂停期间仍需继续：

```csharp
scheduler.Schedule(
    3.0,
    CloseNotification,
    ScheduleOptions.RealTime);
```

`RealTime` 表示不受 TimeScale 和 SceneTree 暂停影响，不表示在后台线程运行；回调仍在 Godot 主线程执行。

## 2. 让任务跟随场景 Node

为场景内任务指定已经进入树的 Owner：

```csharp
var options = new ScheduleOptions(
    clock: ScheduleClock.GameTime,
    phase: SchedulePhase.Process,
    owner: this);

ScheduleHandle handle = scheduler.Schedule(
    1.0,
    ShowNextHint,
    options);
```

Owner 退出场景树时，关联任务和异步等待会自动取消。一个 Owner 的多项任务共享退出监听，任务全部结束后会解除不再需要的绑定。

Owner 必须有效并已进入场景树。不要传入刚 `new`、尚未挂入树的 Node，也不要使用即将 `QueueFree()` 的对象创建新任务。

Owner 为 `null` 的任务可以跨主场景切换。此时调用方必须保存句柄或 CancellationToken，并明确负责清理；不要让场景对象的闭包被全局任务长期持有。

## 3. 安排重复任务

每秒执行一次，第一次也等待一秒：

```csharp
ScheduleHandle handle = scheduler.ScheduleRepeating(
    1.0,
    UpdateCountdown,
    new ScheduleOptions(owner: this));
```

先等待 0.25 秒，之后每秒执行：

```csharp
ScheduleHandle handle = scheduler.ScheduleRepeating(
    initialDelaySeconds: 0.25,
    intervalSeconds: 1.0,
    callback: UpdateCountdown,
    options: new ScheduleOptions(owner: this));
```

如果卡帧错过多个周期，Scheduler 会把遗漏周期合并为一次回调，并把下次到期时间推进到未来，不会在同一帧连续补发大量旧回调。需要精确累计时，用当前权威状态重新计算结果，不要把“回调次数”当作绝对时间。

回调抛出异常时会由 ErrorHub 上报。一次性任务结束，重复任务则会被取消，避免每个周期重复制造错误。

## 4. 取消、暂停与查询任务

```csharp
if (scheduler.IsScheduled(handle) &&
    scheduler.TryGetRemainingSeconds(handle, out double remaining))
{
    GD.Print($"剩余 {remaining:F2} 秒");
}

scheduler.Pause(handle);
scheduler.Resume(handle);
scheduler.Cancel(handle);
```

`Pause` 保存任务自身时钟中的剩余时间；`Resume` 从该剩余时间继续。它只暂停这一项任务，不改变 SceneTree 或 TimeScale。

这些操作在句柄无效、任务已结束或状态不匹配时返回 `false`，不会抛出“任务不存在”异常。`default(ScheduleHandle)` 是无效句柄。

重复任务可以在自己的回调中取消或暂停自己：

```csharp
ScheduleHandle heartbeat = default;
heartbeat = scheduler.ScheduleRepeating(1.0, () =>
{
    if (!ShouldContinue())
        scheduler.Cancel(heartbeat);
}, new ScheduleOptions(owner: this));
```

## 5. 在 Procedure 中异步等待

```csharp
using var cancellation = new CancellationTokenSource();

await scheduler.DelayAsync(
    1.0,
    new ScheduleOptions(
        ScheduleClock.UnscaledGameTime,
        SchedulePhase.Process),
    cancellation.Token);
```

正常完成后的 continuation 回到主线程。Token 可以从后台线程触发，但实际取消会在下一次 Scheduler 主线程更新时生效。Owner 退出、框架关闭或 Token 取消都会让等待以取消状态结束，所以异步流程应正常处理 `OperationCanceledException`。

如果等待属于一个 Node，可同时使用 Owner 和 Procedure 的 CancellationToken：Owner 负责场景生命周期，Token 负责流程主动取消。

## 6. 选择 Process 或 Physics

默认 `Process` 适用于 UI、流程、音频控制和一般业务延迟。只有回调需要与物理更新边界对齐时才选择：

```csharp
var physicsOptions = new ScheduleOptions(
    ScheduleClock.GameTime,
    SchedulePhase.Physics,
    owner: this);
```

Physics 选项不会把 Scheduler 变成确定性模拟器。帧率变化、TimeScale 和浮点时间仍然存在；确定性战斗逻辑应由自己的固定 Tick 系统负责。

0 秒任务最早在下一次对应阶段更新时执行，不会在 `Schedule()` 调用栈内同步重入。这适合把工作推迟到下一帧边界，但不要依赖它替代 Godot 更明确的信号或 Deferred API。

## 7. 在 Debugger 中排查

Debug 构建展开 GoDo Debugger 的 **运行时 / Scheduler** 页面，可以查看：

- 活动、暂停和重复任务数量。
- 三种时钟在 Process/Physics 中的任务分布。
- 最近一次派发数量和累计取消数量。
- Owner 自动取消及回调异常取消计数。
- 各时间域下一任务的剩余时间。

快照只在页面查看时低频生成，不属于 Release API。如果任务数量持续增长，优先检查无 Owner 的重复任务、未释放的 Procedure Token 和只创建不取消的跨场景等待。

## 参数和关闭行为

- 延迟必须是有限且不小于 0 的秒数。
- 重复间隔必须是有限且大于 0 的秒数。
- 所有 public API 只能从 Godot 主线程调用，只有 CancellationToken 的触发可以来自后台。
- GoDoRuntime 关闭时取消所有任务，未完成的 `DelayAsync` 以取消结束。
- 框架关闭后拒绝创建新任务。

## 常见错误

- 暂停菜单中的倒计时停止：选择了 GameTime 或 UnscaledGameTime；改用 RealTime。
- 慢动作时 UI 提示也变慢：使用了默认 GameTime；改用 UnscaledGameTime 或 RealTime。
- 切换场景后回调访问已释放 Node：任务没有 Owner，或闭包持有旧场景对象。
- 重复任务在卡顿后没有补发多次：遗漏周期会合并，这是设计行为。
- `DelayAsync` 抛取消异常：Owner、Token 或框架关闭触发了预期取消。
- 物理回调仍不完全确定：Physics 只是派发阶段，不是确定性 Tick 系统。
- Release 中无法读取调试快照：Scheduler 快照仅供 Debugger 的 Debug 构建使用。

精确接口可查询 <xref:GoDo.ISchedulerService>、<xref:GoDo.ScheduleOptions>、<xref:GoDo.ScheduleClock>、<xref:GoDo.SchedulePhase> 和 <xref:GoDo.ScheduleHandle>。
