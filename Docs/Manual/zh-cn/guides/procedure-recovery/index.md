# 组织 Procedure 切换、清理与失败恢复

Procedure 表达启动、主菜单、加载、游戏中和结算等顶层阶段。它的价值是把 Scene、UI、Audio、Input 和 Save 的调用顺序集中起来，并为每个阶段建立对称的进入和退出边界。

它不是角色、AI、技能或 UI 页面状态机，也不提供流程栈和自动回滚。

## 1. 保持进入顺序可回滚

```csharp
public sealed class GameplayProcedure : IProcedure
{
    private IUiService? _ui;
    private Control? _hud;

    public string Name => "Gameplay";

    public async Task EnterAsync(ProcedureContext context)
    {
        ISceneService scenes = context.GetService<ISceneService>();
        IAudioService audio = context.GetService<IAudioService>();
        IInputService input = context.GetService<IInputService>();
        _ui = context.GetService<IUiService>();

        await scenes.ChangeAsync(GameResources.GameplayScene);
        _hud = _ui.Open(GameResources.GameplayHud, UiLayer.Scene);
        input.SetBaseContext(GameInput.Gameplay);
        await audio.PlayBgmAsync(GameAudio.GameplayTheme);
    }
```

先完成最可能失败且不会污染现有状态的步骤，再提交后续状态。场景切换必须先于 Scene 层 UI，否则场景提交会清理刚打开的 UI。

如果某一步成功后，后续步骤仍可能失败，Procedure 应在 `catch` 中清理自己已经创建的内容，再重新抛出，让 ProcedureService 包装为 `ProcedureChangeException`。

## 2. 对称退出且不依赖字段猜测

```csharp
    public Task ExitAsync(ProcedureContext context)
    {
        if (_ui != null && _hud != null && GodotObject.IsInstanceValid(_hud))
            _ui.Close(_hud);

        _hud = null;
        _ui = null;
        return Task.CompletedTask;
    }
}
```

退出只清理本流程拥有的 UI、事件订阅、Scheduler Handle、CancellationToken 和临时业务对象。不要全局清空其他系统的 View、Modal、音频或事件监听。

Godot 退出时 GoDoRuntime 不会调用当前业务 Procedure 的 `ExitAsync`。退出前必须保存的数据由游戏自己的退出边界主动处理。

## 3. 理解两种失败状态

切换顺序是旧流程 Exit，再将 `Current` 清空，然后新流程 Enter：

- 旧流程 Exit 失败：不进入新流程，`Current` 仍是旧流程。
- 新流程 Enter 失败：旧流程已经退出，`Current` 为 null。

因此新流程进入失败后不能假设框架会自动回到旧流程。调用边界应选择明确恢复目标：

```csharp
try
{
    await procedures.ChangeAsync<GameplayProcedure>();
}
catch (ProcedureChangeException exception)
{
    ErrorHub.Report(exception, "Game.Flow", context: "MainMenu -> Gameplay");

    if (procedures.Current == null)
        await procedures.ChangeAsync(new RecoveryProcedure(exception));
}
```

RecoveryProcedure 应只依赖最小可靠资源，例如内置错误页面或返回标题场景。避免在恢复过程中重复调用刚刚失败的同一资源链。

## 4. 不在 Enter/Exit 中递归 ChangeAsync

直接递归切换会被拒绝。流程内部决定下一步时使用 Context 请求：

```csharp
public Task EnterAsync(ProcedureContext context)
{
    if (!HasValidProfile())
        context.RequestChange<ProfileSelectionProcedure>();

    return Task.CompletedTask;
}
```

UI 和场景脚本应发送玩家意图，由当前 Procedure 的协调对象调用 `RequestChange`。请求会在当前边界安全结束后串行处理。

只保留最近一次请求，因此不要把它当作命令队列。请求执行失败会通过 ErrorHub 报告；重要业务数据用 `RequestChange(new ResultProcedure(data))` 显式传入实例。

## 5. 防止按钮连点和并发切换

```csharp
private async void OnStartPressed()
{
    if (_procedures.IsChanging)
        return;

    _startButton.Disabled = true;
    try
    {
        await _procedures.ChangeAsync<GameplayProcedure>();
    }
    catch (ProcedureChangeException exception)
    {
        ErrorHub.Report(exception, "Game.Flow", "Start gameplay");
        _startButton.Disabled = false;
    }
}
```

同时执行第二个 ChangeAsync 会抛 `ProcedureChangeException`。禁用入口只是用户体验保护，ProcedureService 的拒绝语义仍是最终正确性边界。

不要在 `_Process()` 中触发流程切换，也不要 fire-and-forget 丢失异常。

## 6. 管理取消和长期异步操作

Procedure 自己创建 CancellationTokenSource，并在 Exit 中取消：

```csharp
private CancellationTokenSource? _lifetime;

public async Task EnterAsync(ProcedureContext context)
{
    _lifetime = new CancellationTokenSource();
    ISchedulerService scheduler = context.GetService<ISchedulerService>();
    await scheduler.DelayAsync(1.0, ScheduleOptions.RealTime, _lifetime.Token);
}

public Task ExitAsync(ProcedureContext context)
{
    _lifetime?.Cancel();
    _lifetime?.Dispose();
    _lifetime = null;
    return Task.CompletedTask;
}
```

对预期取消单独处理 `OperationCanceledException`，不要当作资源损坏上报。进入尚未返回时不会同时调用该实例的 Exit，因此 Enter 内启动的后台式业务工作也必须有清晰所有者和异常观察方式。

## 7. 设计可诊断的小流程

- `Name` 使用稳定、可读名称，便于异常和日志定位。
- Procedure 只协调模块，不承载角色移动、战斗规则或复杂 UI 逻辑。
- 大型流程把具体工作交给业务服务或场景 Controller。
- 每次切换记录来源、目标和关键上下文，但不要输出玩家敏感数据。
- `Current` 只代表成功进入的当前流程，不是历史记录。

## 常见错误

- Enter 失败后仍认为旧流程有效：此时 `Current` 通常为 null，需要明确恢复流程。
- Exit 失败后手工进入新流程：旧流程仍是 Current，应先修复或处理旧状态。
- Enter 内直接 ChangeAsync：构成重入，改用 RequestChange。
- 多个按钮同时切换：集中意图处理并观察 `IsChanging`。
- 退出时清空所有 UI：破坏其他长期系统的所有权，只清理本流程创建的实例。
- Godot 退出时依赖 ExitAsync 保存：Runtime Shutdown 不调用业务退出，提前保存。
- Procedure 变成巨大控制器：把具体玩法拆回业务服务和场景节点。

精确接口可查询 <xref:GoDo.IProcedure>、<xref:GoDo.IProcedureService>、<xref:GoDo.ProcedureContext> 和 <xref:GoDo.ProcedureChangeException>。
