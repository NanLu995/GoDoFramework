# SchedulerService 设计草案

> 状态：首版完成；人工时钟核心、完整调度状态、DelayAsync、跨线程取消、Owner 生命周期、真实时间采样、GoDoRuntime 接入、Shutdown、Debug/Release 稳态性能基准与 Debug-only 快照已完成。Debugger UI 展示和真实游戏/跨平台验证尚未完成，不代表稳定 public API。

## 1. 要解决的问题

Godot 的 `Timer` 与 `SceneTree.CreateTimer()` 足以处理局部等待，但跨场景业务仍会重复处理：

- 延迟回调与重复回调的创建、取消、暂停和剩余时间查询；
- 游戏暂停、`Engine.TimeScale` 与真实经过时间的不同语义；
- Process 与 Physics 两种执行阶段；
- 场景节点退出后，旧回调仍持有对象或继续执行；
- 回调异常打断后续任务，或重复任务持续刷错；
- 大量 Timer Node、Signal 和逐帧扫描造成的额外生命周期与性能成本；
- 异步等待在框架退出或调用方取消时无法可靠结束。

`SchedulerService` 的价值是提供统一的主线程时间调度边界，不替代 Godot 场景内适合 Inspector 配置的 `Timer`，也不扩展成通用异步或确定性模拟框架。

## 2. 已确认的方案

采用“单一长期 Scheduler Node + 纯 C# 调度条目 + 分时间域优先队列”：

```text
业务代码
    ↓ Schedule / ScheduleRepeating / DelayAsync
ISchedulerService
    ↓
SchedulerService（GoDoRuntime 子节点，ProcessMode.Always）
    ├── Process 阶段：GameTime / UnscaledGameTime / RealTime
    └── Physics 阶段：GameTime / UnscaledGameTime / RealTime
```

每个任务不会创建 Godot `Timer` 节点。Scheduler 自身是唯一 Node，用于接收 `_Process()`、`_PhysicsProcess()`、场景树暂停状态和退出生命周期。

## 3. 时间语义

### 3.1 ScheduleClock

| 时钟 | TimeScale | SceneTree.Paused | 主要用途 |
|---|---|---|---|
| `GameTime` | 受影响 | 停止推进 | 技能冷却、刷怪、玩法倒计时 |
| `UnscaledGameTime` | 不受影响 | 停止推进 | UI、过渡和不随慢动作变化的游戏内等待 |
| `RealTime` | 不受影响 | 继续推进 | 连接超时、系统提示、暂停菜单中的真实时间等待 |

`GameTime` 使用 Godot 传入的缩放后 `delta`。`UnscaledGameTime` 与 `RealTime` 使用 `Time.GetTicksUsec()` 的单调时间差，不使用可被系统或用户调整的日历时间。

Scheduler 每次收到 Process 或 Physics 更新时都刷新对应阶段的单调时间采样。暂停期间仍刷新采样基准，但不推进 `GameTime` 和 `UnscaledGameTime`，因此恢复后不会把暂停时长一次性补入。

### 3.2 SchedulePhase

- `Process`：在 Scheduler Node 的 `_Process()` 中派发。
- `Physics`：在 Scheduler Node 的 `_PhysicsProcess()` 中派发。

只保证在对应 Scheduler 更新阶段执行，不承诺位于整个场景树所有节点之后。需要严格依赖其他节点处理顺序时，业务应使用 Godot 的 process priority、Signal 或显式调用关系。

Physics + RealTime 在暂停期间仍可到期并回调，但 Godot 物理服务器默认处于暂停状态；这类回调不得假设物理查询或模拟仍在正常运行。

## 4. 第一版职责

### 4.1 负责

- 一次性延迟主线程回调。
- 固定周期的重复主线程回调。
- 返回稳定句柄，支持取消、独立暂停、恢复和剩余时间查询。
- 提供受 `CancellationToken` 控制的异步延迟。
- 支持 GameTime、UnscaledGameTime 与 RealTime。
- 支持 Process 与 Physics 派发阶段。
- 可选绑定 Node Owner，在 Owner 退出场景树时取消关联任务。
- 隔离回调异常，并通过 ErrorHub 给出任务上下文。
- 框架退出时取消全部任务和未完成异步等待。
- Debug 构建提供只读调度快照。

### 4.2 不负责

- 确定性战斗 Tick、固定步模拟或网络同步时钟。
- 在后台线程执行工作，或封装 `Task.Run`。
- `async void` / `Func<Task>` 回调的串行、重试和异常管理。
- Tween、动画、音频采样级或亚帧高精度计时。
- 日历、Cron、系统通知、离线收益和跨存档持久化。
- 跨场景业务任务的自动序列化与恢复。
- 替换适合场景局部配置的 Godot `Timer` Node。

## 5. 公共 API 草案

下面签名用于固定语义，最终名称和 Godot C# 绑定细节在实现阶段以编译结果为准。

```csharp
public interface ISchedulerService
{
    ScheduleHandle Schedule(
        double delaySeconds,
        Action callback,
        ScheduleOptions options = default);

    ScheduleHandle ScheduleRepeating(
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default);

    ScheduleHandle ScheduleRepeating(
        double initialDelaySeconds,
        double intervalSeconds,
        Action callback,
        ScheduleOptions options = default);

    Task DelayAsync(
        double delaySeconds,
        ScheduleOptions options = default,
        CancellationToken cancellationToken = default);

    bool Cancel(ScheduleHandle handle);
    bool Pause(ScheduleHandle handle);
    bool Resume(ScheduleHandle handle);
    bool IsScheduled(ScheduleHandle handle);
    bool TryGetRemainingSeconds(ScheduleHandle handle, out double remainingSeconds);
}
```

`ScheduleOptions` 第一版只包含：

- `ScheduleClock Clock`，默认 `GameTime`；
- `SchedulePhase Phase`，默认 `Process`；
- `Node? Owner`，默认 null。

默认值必须对应枚举的零值，确保 `default(ScheduleOptions)` 具有明确语义。

`ScheduleHandle` 是只读值类型。内部 ID 从 1 单调递增且进程内不复用，0 表示无效句柄；因此不额外引入 generation 和池化复杂度。

## 6. 调度语义

### 6.1 创建与到期

- 秒数必须是有限值且不小于 0；重复间隔必须严格大于 0，否则抛 `ArgumentOutOfRangeException`。
- callback 为 null 时抛 `ArgumentNullException`。
- 绑定的 Owner 必须是有效且已进入场景树的 Node，否则拒绝创建。
- 0 秒任务不在 `Schedule()` 调用栈内同步执行，最早在下一次对应 Scheduler 更新派发。
- 同一时钟、阶段与到期时间的任务按创建顺序执行。
- 回调始终位于 Godot 主线程。

### 6.2 重入

Scheduler 在每轮派发开始时记录序号上限。本轮回调中新建的任务即使延迟为 0，也不会在同一轮继续执行，避免无限重入。

回调可以取消、暂停或恢复其他任务。重复任务回调也可以通过自身句柄取消自己；回调返回后 Scheduler 再决定是否重新入队。

### 6.3 重复任务与卡帧

重复任务采用固定节奏、遗漏合并语义：

- 正常情况下以下一次理论到期点为基准，降低长期漂移；
- 一次卡帧跨过多个周期时，本轮最多回调一次；
- 已错过的周期不追赶式连续执行；
- 下一到期点跳到当前时刻之后的第一个理论周期。

该语义适合普通游戏调度，不适合必须逐 Tick 补算的确定性模拟。

### 6.4 独立暂停

`Pause(handle)` 保存该任务在自身时钟中的剩余时间并使其离开有效派发队列。`Resume(handle)` 从当前时钟重新计算到期点。

SceneTree 暂停导致时钟不推进，不等同于任务独立暂停；恢复游戏时任务仍保持原调度状态。

### 6.5 Owner 生命周期

Owner 绑定是可选的：

- 有 Owner 的任务在该 Node 退出场景树时统一取消；
- 同一 Owner 只建立一份内部生命周期绑定，最后一个任务结束后解除；
- Owner 退出导致的 `DelayAsync` 以取消结束；
- Owner 为 null 的任务可跨主场景切换，直到完成、显式取消或框架关闭。

业务回调仍应避免不必要地闭包捕获大型对象。Owner 绑定解决执行生命周期，不承诺消除任意闭包造成的托管引用。

## 7. 异步与线程约束

- 所有 public 查询和修改 API 默认调用 `MainThreadGuard.VerifyAccess()`。
- `DelayAsync` 不使用 `Task.Delay`，由 Scheduler 到期后在主线程完成。
- 正常完成后的 await continuation 保持在 Scheduler 派发所处的 Godot 主线程调用链。
- `CancellationToken` 可能从后台线程触发；Token 回调只把句柄写入线程安全取消队列，不访问 Godot 对象和主调度容器。
- Scheduler 在下一次更新时于主线程排空取消队列并完成取消；取消不是后台线程上的同步完成保证。
- Shutdown 在主线程取消所有未完成 `DelayAsync`，避免永久悬挂。

第一版不接受异步回调委托。调用者需要异步流程时使用 `DelayAsync` 组织自己的方法，避免 `async void` 异常逃出 Scheduler 的保护范围。

## 8. 内部数据结构

每个 `ScheduleClock × SchedulePhase` 组合维护一个 `PriorityQueue`，共 6 个逻辑队列。优先级由以下值组成：

```text
(DueTime, CreationSequence)
```

另有 ID 到活动条目的 Dictionary，用于句柄查询和状态修改。

取消和暂停采用 revision 失效策略：队列项携带 ID 与 revision；出队时如果与活动条目不匹配则丢弃。这样取消为 O(1)，恢复和创建为 O(log n)，且不需要实现容易出错的自定义索引堆。

为避免远期取消项长期占用内存，当某队列的失效项达到固定下限，且超过活动项数量时，从活动注册表重建该队列。重建是低频 O(n) 操作；具体阈值在基准测试中确定，不作为 public 配置。

重复任务复用原活动条目，只更新 revision、到期时间并重新入队，不为每次触发创建新的回调对象。

每次阶段更新设置内部派发上限，防止同一帧海量到期回调无限占用主线程。达到上限时保留其余到期任务到下一对应阶段，并通过 ErrorHub 产生一次聚合警告；首版阈值通过回归与性能验证确定。

## 9. 异常与失败语义

- public 参数和句柄状态错误遵循普通 C# 参数异常或 bool 结果，不先上报再抛出。
- 无效、已完成或已取消句柄的 Cancel/Pause/Resume 返回 false。
- 一次性 callback 抛出异常时，由 ErrorHub 上报后结束该任务，继续派发其他任务。
- 重复 callback 抛出异常时，由 ErrorHub 上报并取消该任务，避免周期性刷错。
- ErrorHub 上下文至少包含 handle、clock、phase 和是否重复，不记录委托目标对象内容。
- Scheduler 自身数据结构不因业务 callback 异常离开派发状态。

## 10. 性能目标

- 无任务到期时，每帧只读取 6 个逻辑队列的队首，不遍历全部活动任务。
- 创建 O(log n)，取消 O(1)，恢复 O(log n)，到期任务 O(log n)。
- 稳定等待帧零托管分配。
- 新建任务允许分配活动条目和 callback；`DelayAsync` 另有 Task 与 CancellationTokenRegistration 成本。
- 1,000 个活动任务的无到期更新作为基础基准；Debug 与 Release 分别记录结果。
- 小于约 0.05 秒的等待只保证帧级精度，不声明高精度 Timer 能力。

## 11. GoDoRuntime 接入

当前 GoDoRuntime 场景树：

```text
GoDoRuntime
├── SchedulerService   （新增，单一 Node）
├── SceneService
├── AudioService
├── UiService
└── GoDoUI
```

GoDoRuntime 新增导出的 `SchedulerServicePath`，在 `_Ready()` 中验证节点、注册 `ISchedulerService`；退出时先 Shutdown、注销，再释放其他框架状态。Scheduler 只依赖 Core 的 MainThreadGuard、ErrorHub 与 Services 注册边界，不通过 Services 查找其他长期服务。

Scheduler 不通过 EventChannel 广播每次触发，避免把点对点回调变成高频全局事件。

## 12. Debug 快照

Debug-only 快照已包含：

- 活动、暂停、重复任务数量；
- 三种时钟和两个阶段的任务数量；
- 最近一次派发数量；
- 累计取消、其中 Owner 自动取消和异常取消数；
- 下一任务剩余时间，不包含 callback 或 Owner 的字符串详情。

快照不扩大 `ISchedulerService` public API，由 `SchedulerService` 提供 internal Debug 接口。查询时才遍历活动条目，Release 不包含统计字段或快照类型；Debugger UI 展示留到后续与 Input 诊断面板统一优化。

## 13. 验证计划

### 13.1 不依赖编辑器的确定性回归

- 三种时钟在 TimeScale、暂停和恢复下的推进差异；
- Process / Physics 队列隔离；
- 一次性、重复、初始延迟、取消、暂停、恢复和剩余时间；
- 同时到期稳定顺序；
- 回调内新增、取消自身和取消其他任务；
- 卡帧后重复任务只执行一次并保持理论节奏；
- callback 异常隔离与重复任务异常取消；
- revision 失效项和队列压缩；
- 派发上限；
- Shutdown 取消。

Scheduler 的时间推进逻辑应允许测试传入人工 delta 和单调时间，不依赖真实等待。

### 13.2 Godot Headless 集成

- `Services.Get<ISchedulerService>()` 可取得服务；
- Process 与 Physics 实际阶段触发；
- SceneTree 暂停时三种时钟行为符合定义；
- Owner 退出场景树自动取消；
- `DelayAsync` 正常完成、Token 取消和 Runtime 退出取消；
- ErrorHub 收到结构化 callback 异常。

### 13.3 手动验证

- 编辑器暂停菜单与慢动作体验；
- 窗口失焦、最小化和恢复后的 RealTime 行为；
- 低 FPS 与长卡帧下的回调节奏；
- Debugger 快照可读性。

## 14. 实现顺序

1. 公共模型、人工时钟核心和确定性回归。
2. 一次性、重复、取消、暂停、恢复与异常隔离。
3. CancellationToken、DelayAsync 与 Shutdown。
4. Owner 生命周期绑定。（已完成）
5. SchedulerService 真实时钟采样、GoDoRuntime 场景树与 Services 接入。（已完成）
6. Godot Headless 集成、性能基准和 `USAGE.md`。（已完成）
7. Debug-only 快照。（已完成；Debugger UI 展示与后续面板优化合并）

每一步单独验证。首版完成后标记为“首版完成”，在真实游戏验证前不提升为稳定基线。
