# SchedulerService 使用指南

> 当前状态：首版完成。运行时核心已接入 GoDoRuntime，并注册 `ISchedulerService`；自动回归已覆盖人工时钟、真实帧采样、暂停、TimeScale、Owner、退出清理、Debug/Release 稳态性能与 Debug-only 快照。Windows Demo3D 已完成人工验收；真实项目长期体验与跨平台验证尚未完成，因此不标记为稳定基线。

## 定位

SchedulerService 用于统一管理主线程上的一次性延迟、重复调度、取消、独立暂停和异步等待，并明确游戏时间、非缩放游戏时间与真实时间的差异。

它不会为每个任务创建 Godot `Timer` Node。场景内适合 Inspector 配置、生命周期完全局部的简单计时仍可直接使用 Godot `Timer`。

## 适用场景

- 延迟若干秒后调用业务回调。
- 固定周期执行主线程回调。
- 技能冷却、玩法倒计时、UI 延迟和连接超时需要不同暂停/缩放语义。
- 场景切换时需要取消绑定到旧 Node 的任务。
- Procedure 等异步流程需要可取消的游戏时间等待。

## 非适用场景

- 确定性 Tick、固定步模拟和网络同步时钟。
- 后台线程任务执行。
- Tween、动画或音频采样级高精度计时。
- 日历提醒、离线收益和跨存档持久化。

## 上手

```csharp
ISchedulerService scheduler = Services.Get<ISchedulerService>();

ScheduleHandle handle = scheduler.Schedule(
    0.5,
    () => GD.Print("延迟执行"),
    new ScheduleOptions(
        clock: ScheduleClock.GameTime,
        phase: SchedulePhase.Process,
        owner: this));

using var cancellation = new CancellationTokenSource();
await scheduler.DelayAsync(1.0, ScheduleOptions.RealTime, cancellation.Token);
```

`Owner` 应使用当前业务生命周期对应、且已经进入场景树的 Node。Owner 为 null 时任务可以跨主场景切换，调用方必须自行保存句柄或 Token 并负责清理。

## Public API

- `ISchedulerService`：通过 `Services.Get<ISchedulerService>()` 获取的业务服务接口。
- `ScheduleHandle`：不透明任务句柄；默认值无效。
- `ScheduleOptions`：时钟、Process/Physics 阶段与可选 Owner。
- `ScheduleClock`：`GameTime`、`UnscaledGameTime`、`RealTime`。
- `SchedulePhase`：`Process`、`Physics`。

业务代码不要自行实例化 `SchedulerService`，也不要在业务场景重复注册服务。

### 调度与等待

| 成员 | 输入契约 | 返回与可观察结果 |
|---|---|---|
| `Schedule(delaySeconds, callback, options)` | 延迟有限且不小于 0；callback 非 null | 返回有效 `ScheduleHandle`；回调最早在下一次所选阶段执行，任务执行后结束 |
| `ScheduleRepeating(intervalSeconds, callback, options)` | 间隔有限且大于 0；callback 非 null | 返回有效句柄；首次执行也等待一个完整间隔，卡帧遗漏周期合并为一次 |
| `ScheduleRepeating(initialDelaySeconds, intervalSeconds, callback, options)` | 初始延迟有限且不小于 0；间隔有限且大于 0 | 返回有效句柄；首次执行使用独立延迟，后续使用固定间隔 |
| `DelayAsync(delaySeconds, options, cancellationToken)` | 延迟有限且不小于 0 | 到期时在所选主线程阶段完成；Token、Owner 或框架关闭会使 Task 取消 |

### 句柄操作

| 成员 | 成功结果 | 不成功结果 |
|---|---|---|
| `Cancel(handle)` | 取消活动或独立暂停任务并返回 `true` | 无效、已结束或已取消时返回 `false` |
| `Pause(handle)` | 保存任务自身时钟中的剩余时间并返回 `true` | 无效、已结束、已经暂停或当前状态不允许时返回 `false` |
| `Resume(handle)` | 从保存的剩余时间恢复并返回 `true` | 不是独立暂停任务时返回 `false` |
| `IsScheduled(handle)` | 活动、派发中或独立暂停时返回 `true` | Scheduler 已不再管理该句柄时返回 `false` |
| `TryGetRemainingSeconds(handle, out remainingSeconds)` | 返回 `true` 和不小于 0 的剩余秒数；派发中为 0 | 句柄不存在时返回 `false`，输出 0 |

## 失败语义与生命周期

- 非有限或负延迟抛 `ArgumentOutOfRangeException`；重复间隔还必须大于 0。`ScheduleOptions` 的 Clock 或 Phase 不是已定义枚举值时也抛同类异常。
- callback 为 null 时抛 `ArgumentNullException`。
- Owner 已失效时抛 `ArgumentException`；Owner 有效但尚未进入场景树时抛 `InvalidOperationException`。Owner 在创建任务时校验，而不是在构造 `ScheduleOptions` 时校验。
- 0 秒任务最早在下一次对应 Scheduler 更新执行，不同步重入。
- public 服务 API 限制在 GoDo 主线程；框架未记录主线程、从错误线程调用或服务不在场景树时抛 `InvalidOperationException`。
- Scheduler 已永久关闭但节点仍在树内时，继续创建任务或等待会抛 `ObjectDisposedException`；查询或修改已清空的句柄返回 `false`。节点退出树后调用则先因服务生命周期无效抛 `InvalidOperationException`。
- 同一 Owner 只建立一次退出树监听；任务自然结束或显式取消后会解除不再需要的绑定。
- Owner 退出、显式取消与框架关闭会取消关联异步等待。
- callback 异常由 ErrorHub 隔离；重复任务发生异常后取消。
- `CancellationToken` 可从后台线程触发，但取消会在下一次 Scheduler 主线程更新时生效。
- GoDoRuntime 退出会取消全部任务；尚未完成的 `DelayAsync` 以取消结束。

完整设计、性能目标和分步验证见 `Docs/SchedulerServiceDesign.md`。

## 性能

人工时钟核心按时间域和阶段维护优先队列。无任务到期时只检查队首，不遍历全部活动任务。目标是在稳定等待帧零托管分配；新建任务和 `DelayAsync` 允许产生必要分配。

Windows、20 逻辑处理器环境的首轮基准中，1,000 个等待任务连续空闲推进和 1,000 个已有任务同轮派发在 Debug/Release 稳态均为零托管分配。10,000 次创建取消约分配 1.125 MB，属于任务条目和队列项的创建成本。完整方法、原始规模、耗时与 Release 运行方式见 `Verification/Performance/README.md`；这些数据不构成跨机器或跨平台保证。

## Debug 诊断

Debug 构建中的 `SchedulerService` 提供 internal 只读快照，包含：

- 活动、暂停和重复任务数量；
- 三种时钟与 Process/Physics 的六组任务分布；
- 最近一次 Process/Physics 派发数量；
- 累计取消、其中 Owner 自动取消与 callback 异常取消数量；
- 下一任务在自身时钟中的剩余时间；
- 最多 64 条活动任务明细，包括自动生成的任务标签、Owner 名称/路径/实例 ID、时钟、阶段、等待/暂停/执行状态、是否重复、单调时钟存活时间和剩余时间；
- 最近 16 条结束记录，区分正常完成、主动取消、Owner 退出、Token 取消、框架关闭和 callback 异常。

任务标签由调度类型与 callback 方法自动生成，`DelayAsync` 使用固定标签，不增加 public API。Owner 身份在任务创建时复制为最多 256 字符的诊断文本，快照和结束历史不会额外持有 Node；存活时间来自单调时钟，只用于观察，不参与到期计算。

快照只在被查询时 O(n) 遍历活动条目，不在每帧维护分组统计。类型、字符串和历史入口都位于 `#if DEBUG`，Release 不包含。GoDo Debugger 的 `运行时 / Scheduler` 页面每 0.25 秒按需读取一次当前快照，折叠或查看其他页面时不查询。

## 当前验证

`Verification/Automated/SchedulerCoreRegression.tscn` 使用人工时间验证：

- 默认选项；
- TimeScale 对三种时钟的差异；
- SceneTree 暂停语义；
- Process / Physics 隔离；
- 零延迟与回调重入；
- 同到期任务的稳定顺序；
- 一次性与重复任务、独立初始延迟和卡帧周期合并；
- 取消、自取消、暂停、恢复和剩余时间；
- callback 异常隔离、派发上限和失效队列压缩；
- Debug-only 快照的状态分布、最近派发与取消原因；
- 活动任务自动标签、Owner 身份、存活时间、暂停状态和最近结束原因；
- DelayAsync 正常完成、主线程 continuation、后台 Token 取消和预取消；
- Owner 入树校验、同 Owner 绑定复用、任务结束解绑与退出树自动取消；
- Shutdown 取消未完成等待并拒绝新任务；
- 非法时间参数。

`Verification/Automated/SchedulerRuntimeRegression.tscn` 另验证 Runtime 服务注册、真实 Process/Physics、TimeScale、SceneTree 暂停、10 FPS 持续低帧下重复任务不补发、Owner 与服务退出清理。

`Verification/Performance/SchedulerBenchmark.tscn` 验证 1,000 个等待任务的空闲热路径、10,000 次创建取消、队列压缩和 1,000 个任务同轮派发。

Windows Demo3D 人工验收已覆盖暂停/恢复、慢动作、窗口失焦/最小化、Owner 清理、750 毫秒主线程卡顿恢复和 Debugger 快照观察；Windows Headless 自动回归已覆盖 10 FPS 持续低帧。真实项目长期体验与其他平台行为仍需后续验证。
