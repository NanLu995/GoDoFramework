# Procedure 使用说明

Procedure 是顶层游戏流程切换服务，用来表达启动、主菜单、加载、游戏中、暂停、结算和返回菜单等全局阶段。它借用状态机思想，但不是通用 StateMachine 框架，也不内置任何具体业务流程。

## 适用场景

- 需要在多个顶层阶段之间串行切换。
- 需要为每个阶段提供对称的进入和退出清理。
- 需要避免按钮连点或异步过渡导致重复切换。
- 需要把 Scene、UI、Audio、Save 等服务的调用顺序集中到业务流程类中。

## 非适用场景

- 角色 Idle / Run / Attack 等局部状态。
- AI、战斗阶段、技能阶段等玩法状态机。
- UI 内部页面的小范围切换。
- 需要流程栈、层级状态机、黑板或自动发现的复杂状态系统。

这些需求可以由游戏项目自行实现；只有出现跨项目重复痛点后，才考虑抽象新的通用机制。

## 快速上手

业务项目定义自己的流程类：

```csharp
using System.Threading.Tasks;
using GoDo;

public sealed class MainMenuProcedure : IProcedure
{
    public string Name => "MainMenu";

    public async Task EnterAsync(ProcedureContext context)
    {
        IUiService ui = context.GetService<IUiService>();
        IAudioService audio = context.GetService<IAudioService>();

        // 打开主菜单 UI、播放菜单 BGM 等。
        await Task.CompletedTask;
    }

    public Task ExitAsync(ProcedureContext context)
    {
        // 关闭主菜单 UI、清理临时状态等。
        return Task.CompletedTask;
    }
}
```

发起切换：

```csharp
IProcedureService procedures = Services.Get<IProcedureService>();
await procedures.ChangeAsync(new MainMenuProcedure());
```

GoDoRuntime 只注册 `IProcedureService`，不会自动进入任何业务流程。游戏项目应在自己的启动场景或启动脚本中决定第一个 Procedure。

## Public API

### IProcedure

```csharp
public interface IProcedure
{
    string Name { get; }
    Task EnterAsync(ProcedureContext context);
    Task ExitAsync(ProcedureContext context);
}
```

- `Name`：用于诊断和异常信息。
- `EnterAsync`：进入流程。
- `ExitAsync`：退出流程。

### IProcedureService

```csharp
public interface IProcedureService
{
    IProcedure? Current { get; }
    bool IsChanging { get; }
    Task ChangeAsync(IProcedure next);
}
```

- `Current`：当前已成功进入的流程；无流程或进入失败后为 `null`。
- `IsChanging`：是否正在切换。
- `ChangeAsync`：退出当前流程并进入目标流程。

### ProcedureContext

```csharp
public sealed class ProcedureContext
{
    public TService GetService<TService>() where TService : class;
    public bool TryGetService<TService>(out TService? service) where TService : class;
}
```

Procedure 模块本身不直接依赖 Scene、UI、Audio、Save 等具体服务。业务 Procedure 可以通过 `ProcedureContext` 显式获取已注册服务。

## 切换语义

`ChangeAsync(next)` 的顺序为：

1. 验证主线程、目标流程和并发状态。
2. 如果存在旧流程，调用旧流程 `ExitAsync(context)`。
3. 将 `Current` 置为 `null`。
4. 调用新流程 `EnterAsync(context)`。
5. 进入成功后，将 `Current` 更新为新流程。
6. 无论成功或失败，最终复位 `IsChanging`。

首版不做切换排队。切换过程中再次调用 `ChangeAsync` 会直接失败。

## 失败语义

- `next == null`：抛出 `ArgumentNullException`。
- 已有切换正在执行：抛出 `ProcedureChangeException`。
- 旧流程 `ExitAsync` 失败：抛出 `ProcedureChangeException`，不进入新流程，`Current` 保持旧流程。
- 新流程 `EnterAsync` 失败：抛出 `ProcedureChangeException`，`Current` 为 `null`。
- `EnterAsync` 或 `ExitAsync` 内部异常会作为 `ProcedureChangeException.InnerException` 保留。

Procedure 不会静默吞掉异常，也不会自动回滚旧流程。需要复杂恢复策略时，应由业务层明确处理。

## 生命周期与线程

- 所有公共 API 必须在 GoDoRuntime 记录的 Godot 主线程调用。
- `IProcedureService` 由 GoDoRuntime 创建并注册到 Services。
- GoDoRuntime 退出时会清空当前 Procedure 引用，但不会调用业务 Procedure 的 `ExitAsync`。项目退出阶段如需保存或清理业务状态，应由业务层主动完成。
- Procedure 对象由业务项目创建，框架不负责复用、释放或自动发现。

## 性能

Procedure 切换不是高频路径。首版优先保证生命周期清楚和失败可见，不为每帧零分配做额外复杂化。不要在 `_Process` 中频繁调用 `ChangeAsync`。

## 常见误用

- 把角色状态、AI 状态放进 Procedure。
- 在 Procedure 内写具体玩法规则，把流程类变成大控制器。
- 在 `GoDoRuntime` 中主动进入业务 Procedure。
- 在 `EnterAsync` 尚未完成时再次发起切换。
- 依赖 `Current` 表示“上一次退出过的流程”；`Current` 只表示当前成功进入的流程。

## 验证

自动回归入口：

```text
Verification/Automated/ProcedureRegression.tscn
```

覆盖范围：

- 初始状态为空。
- 首次进入流程。
- `ExitAsync` → `EnterAsync` 切换顺序。
- 并发切换拒绝。
- `ExitAsync` 失败保留旧流程。
- `EnterAsync` 失败后当前流程为空。
- `ProcedureContext` 获取已注册服务。

手动验证建议：

- 用真实业务流程验证主菜单进入游戏。
- 连续点击开始按钮不会重复切换。
- 返回主菜单时 UI、场景、音频清理符合预期。
