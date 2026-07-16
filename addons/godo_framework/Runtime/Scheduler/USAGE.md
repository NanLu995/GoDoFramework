# SchedulerService 使用指南

> 当前状态：首版完成。运行时核心已接入 GoDoRuntime，并注册 `ISchedulerService`；自动回归已覆盖人工时钟、真实帧采样、暂停、TimeScale、Owner、退出清理、Debug/Release 稳态性能与 Debug-only 快照。真实游戏与跨平台手动验证尚未完成，因此不标记为稳定基线。

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

## 失败语义与生命周期

- 非有限或负延迟抛参数异常，重复间隔必须大于 0。
- 0 秒任务最早在下一次对应 Scheduler 更新执行，不同步重入。
- public 服务 API 限制在 GoDo 主线程。
- Owner 必须有效且已经进入场景树，否则拒绝创建关联任务。
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
- 下一任务在自身时钟中的剩余时间。

快照只在被查询时 O(n) 遍历活动条目，不在每帧维护分组统计。类型和入口都位于 `#if DEBUG`，Release 不包含。GoDo Debugger 的 `运行时 / Scheduler` 页面每 0.25 秒按需读取一次当前快照，折叠或查看其他页面时不查询。

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
- DelayAsync 正常完成、主线程 continuation、后台 Token 取消和预取消；
- Owner 入树校验、同 Owner 绑定复用、任务结束解绑与退出树自动取消；
- Shutdown 取消未完成等待并拒绝新任务；
- 非法时间参数。

`Verification/Automated/SchedulerRuntimeRegression.tscn` 另验证 Runtime 服务注册、真实 Process/Physics、TimeScale、SceneTree 暂停、Owner 与服务退出清理。

`Verification/Performance/SchedulerBenchmark.tscn` 验证 1,000 个等待任务的空闲热路径、10,000 次创建取消、队列压缩和 1,000 个任务同轮派发。

仍需手动验证窗口失焦/最小化、暂停菜单和慢动作体验，以及低 FPS、长卡帧下的回调节奏。
