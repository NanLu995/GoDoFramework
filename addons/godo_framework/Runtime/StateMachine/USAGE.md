# StateMachine 使用指南

## 定位

`StateMachine<TContext, TState>` 是同步、纯 C#、可继承的有限状态机内核。它只管理当前状态、Enter/Exit 生命周期、可选 Update、FIFO 重入切换与关闭，不依赖 Godot Node、场景树、Services、事件或具体玩法类型。

业务可以通过继承得到语义明确的状态机，例如角色动作、敌人决策或技能执行状态机。顶层场景、UI 和资源生命周期继续使用 Procedure；StateMachine 不替代 Procedure，也不继承 Procedure。

## 定义状态与业务状态机

```csharp
public sealed class CharacterActionContext
{
    public required CharacterModel Model { get; init; }
}

public interface ICharacterActionState : IState<CharacterActionContext>
{
}

public sealed class CharacterActionStateMachine
    : StateMachine<CharacterActionContext, ICharacterActionState>
{
    public CharacterActionStateMachine(CharacterActionContext context)
        : base(context)
    {
    }
}

public sealed class IdleState : ICharacterActionState,
    IUpdatableState<CharacterActionContext>
{
    public void Enter(CharacterActionContext context)
    {
    }

    public void Update(CharacterActionContext context, double deltaSeconds)
    {
    }

    public void Exit(CharacterActionContext context)
    {
    }
}
```

状态是具有生命周期身份的引用对象。`TState` 必须为实现 `IState<TContext>` 的引用类型；状态机使用引用相等判断是否为同一个状态，不使用状态名称、枚举、反射或值相等。

## 初始化、切换与更新

```csharp
using var actions = new CharacterActionStateMachine(context);

StateChangeResult initial = actions.Change(idleState);
StateChangeResult changed = actions.Change(runState);
actions.Tick(deltaSeconds);
```

- 默认构造函数允许最外层一次 `Change` 完成最多 64 次实际状态切换；可通过 `StateMachine(context, maxTransitionsPerChange)` 为具体业务状态机设置其他正整数上限。
- 构造后 `CurrentState` 为 `null`；未初始化时调用 `Tick` 是无操作。
- 首次 `Change` 直接设置 CurrentState 后调用目标 Enter。
- 后续切换固定执行旧状态 Exit、更新 CurrentState、新状态 Enter。
- 对当前同一实例调用 `Change` 返回 `IgnoredSameState`，不调用 Enter 或 Exit。
- 正常同步完成返回 `Changed`。
- Enter 或 Exit 内调用 `Change` 不递归执行，返回 `Queued` 并进入 FIFO 队列；最外层 `Change` 会在返回前处理完整个队列。
- 队列中的目标在轮到执行时若已经是当前实例，会被忽略。
- 忽略的重复实例不计入切换上限；只有实际开始执行生命周期的目标才计数。
- `Tick` 只调用当前状态实现的 `IUpdatableState<TContext>.Update`。`deltaSeconds` 原样传递，合法范围和固定/可变时间步由业务驱动层决定。

## 异常与终止故障策略

Enter 或 Exit 抛出的原始异常不包装、不吞掉，并原样传播给当前 `Change` 或 `Dispose` 调用方。状态机同时进入永久终止故障状态：

- `IsFaulted` 为 true，`Failure` 保留原始异常实例；
- 清除未处理的 FIFO 请求；
- 后续 `Change` 和 `Tick` 抛出 `InvalidOperationException`，其 InnerException 指向原始异常；
- 不自动回滚、不重试 Enter/Exit，也不尝试进入队列中的后续状态；
- Exit 失败时 `CurrentState` 保留旧状态用于诊断；Enter 失败时保留已提交的新状态用于诊断。

若 FIFO 切换链试图超过 `MaxTransitionsPerChange`，状态机会在下一次 Exit 前抛出 `StateMachineTransitionLimitException` 并进入相同的终止故障状态。异常的 `MaximumTransitions` 保留实际配置值，`CurrentState` 保留最后一个完整 Enter 成功的状态。这能终止 A.Enter 请求 B、B.Enter 又请求 A 等无法收敛的业务切换链，而不会留下退出一半的状态。

Enter 实现必须在抛出前自行撤销本次部分初始化，因为状态机不会对未成功进入的状态调用 Exit。Exit 实现也应把必要清理写成单次调用可完成的操作；Exit 抛出后不会重试。该约束避免二次生命周期异常遮盖最初根因。

Update 异常同样原样传播，但不会改变 CurrentState 或把状态机标记为故障，因为 Update 不改变生命周期提交点。是否关闭所属实体或模拟分区由调用边界决定。

## Dispose 与所有权

- 正常 `Dispose` 对当前状态调用一次 Exit，然后清空 CurrentState 并永久关闭状态机。
- 重复 `Dispose` 没有副作用。
- Dispose 的 Exit 即使抛出，状态机仍保持已关闭、记录 Failure、清空 CurrentState，后续 Dispose 不重试。
- Enter/Exit 生命周期故障后的 Dispose 只清空诊断状态，不重试失败或未完成的生命周期。
- 切换上限故障发生在下一次 Exit 前，最后一个状态仍是完整激活状态；Dispose 会对它执行一次 Exit 后清空 CurrentState。
- Enter/Exit 内重入 Dispose 会抛出 `InvalidOperationException`；生命周期回调只能请求后续 Change。
- Dispose 后 Change 和 Tick 抛出 `ObjectDisposedException`。

状态机不创建或释放业务 Context，也不拥有状态对象本身；调用方负责 Context、状态实例以及状态机实例的整体生命周期。

## 线程与性能

- 状态机不限制 Godot 主线程，但实例不是线程安全的。Change、Tick、Dispose 必须由同一串行执行边界调用。
- MMO 服务端可以让每个实体或模拟分区在所属逻辑线程串行驱动状态机；跨线程消息应先进入外部调度或命令队列，不直接并发调用状态机。
- 普通 Change 和 Tick 不创建 FIFO 队列；只有生命周期回调首次请求嵌套切换时才延迟创建队列。切换上限检查只使用局部计数，不产生额外集合。
- Tick 不使用 LINQ、反射、字符串状态名或事件派发；无可更新当前状态时只执行类型检查。
- 本模块不提供层级状态机、并行状态、状态栈、异步生命周期、计时器、网络复制、客户端预测、回滚或状态图编辑器。

`Verification/Performance/StateMachineBenchmark.tscn` 在 1,000 与 10,000 台状态机规模下分别累计测量一千万次 Tick 和一百万次普通 Change，并测量 FIFO 首次创建与复用路径。2026-08-21 Windows Godot 4.7.1 Mono Headless、.NET 8 Debug/Release 样本中，Tick、普通 Change 和队列复用后的嵌套 Change 均为 0 B 稳态当前线程托管分配；10,000 个状态机对象本体约 56 B/台，首次创建 FIFO 队列分配 120 B。绝对耗时和对象大小只作为当前运行时与机器的后续同配置基线，不包含业务状态回调、Context、Godot、网络或跨线程调度成本。

## 自动回归验证

`Verification/Automated/StateMachineRegression.tscn` 验证未初始化状态、业务继承、首次进入、A 到 B 顺序、重复实例、Enter/Exit 嵌套 FIFO、可选 Tick、Dispose 幂等、Enter/Exit/Dispose 异常终止、队列清理、Update 异常传播，以及循环切换上限与故障后的单次清理。

```powershell
& $env:GODOT_PATH --headless --path . Verification/Automated/StateMachineRegression.tscn
```

当前已在项目声明的 Godot 4.7.1 Mono Headless 中完成 12/12 项验证。测试场景只作为 runner；被测状态机源码和测试状态均不依赖 Godot 类型。

`Verification/Package/verify_core_package.py` 会把无可选集成的核心目录复制到系统临时 Godot C# 项目，并在该干净项目中编译和执行 StateMachine 的业务继承、Change、Tick 与 Dispose 烟雾用例。`Verification/Package/test_release_package.py` 另外生成真实核心 ZIP，检查 StateMachine 必需源码全部位于发布文件集。

## 适用边界

| 适用 | 不适用 |
|---|---|
| 角色动作、敌人决策、技能执行等互斥局部状态 | 顶层场景、UI 和资源流程编排；使用 Procedure |
| 单线程或已串行化的客户端/服务端模拟 | 多线程直接并发 Change/Tick |
| 同步、确定的 Enter/Exit 生命周期 | 异步加载、网络确认或动画完成等待 |
| 业务显式创建和持有状态实例 | 反射扫描、字符串状态名或自动依赖注入 |
