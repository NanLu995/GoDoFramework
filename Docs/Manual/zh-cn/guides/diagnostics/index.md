# 记录日志、上报错误与查看运行状态

GoDo 把运行信息分为两条通道：LogHub 记录正常流程的开发诊断，ErrorHub 记录需要关注的降级、失败和致命问题。Debug 构建还会自动显示只读 Debugger，方便在游戏运行时查看框架状态。

这套分工的目标不是“多打印一些文字”，而是让开发日志在 Release 中消失，同时让真正的错误在发布版本中仍然可见。

## 先选择正确的出口

| 情况 | 使用 |
|---|---|
| 正常进入流程、缓存命中、开发期状态变化 | `LogHub.Debug` / `LogHub.Info` |
| 可以恢复，但结果发生了降级 | `ErrorHub.Warn` |
| 当前操作失败，并且拿到了异常 | `ErrorHub.Report` |
| 游戏已无法安全继续 | `ErrorHub.Fatal`，再由业务边界决定退出或回到安全页面 |
| 给玩家显示提示 | 游戏自己的 UI；不要直接展示控制台文本 |

`Fatal` 只是最高错误等级，不会自动退出游戏。重试、回退、返回标题页或退出进程，始终由知道业务上下文的调用方决定。

## 1. 为正常流程添加开发日志

```csharp
LogHub.Info("进入主菜单流程", "Game.Procedure");
LogHub.Debug("资源已命中缓存", "Game.Inventory", context: "item=sword");
```

输出格式统一为：

```text
[模块] [等级] (可选上下文) 消息
```

模块名应稳定，例如 `Game.Boot`、`Game.Save`、`Game.Inventory`。消息说明发生了什么，`context` 放槽位、资源 ID 或流程名等定位信息。不要把等级和模块再次拼进消息。

LogHub 只能从 Godot 主线程调用。它的调用带有 `Conditional("DEBUG")`：Release 构建会在调用点移除，连参数表达式也不会求值。因此不要依赖日志参数中的函数产生副作用。

## 2. 上报可恢复问题

当操作可以继续，但使用了备用值或降级路径：

```csharp
ErrorHub.Warn(
    "音量配置缺失，已使用默认值。",
    "Game.Settings",
    context: "key=audio.master");
```

Warning 应该能回答“哪里降级、采用了什么结果”。不要把频繁出现的正常状态当作 Warning；错误风暴会淹没真正的问题。

## 3. 在功能边界处理异常

只在能够决定恢复策略的边界捕获：

```csharp
try
{
    SaveLoadResult<PlayerSave> result = saves.Load<PlayerSave>(
        SaveSlot.Create("slot-1"),
        PlayerSaveCodec.Instance);

    ApplySave(result.Value);
}
catch (SaveException exception)
{
    ErrorHub.Report(exception, "Game.Save", context: "slot=slot-1");
    ShowLoadFailedDialog();
}
```

同一个失败只上报一次。如果底层抛出异常并由上层统一处理，底层不要先上报再重新抛出，否则控制台、Reporter 和玩家遥测都会出现重复记录。

无法继续启动时可以：

```csharp
catch (Exception exception)
{
    ErrorHub.Fatal(exception, "Game.Boot", context: "phase=initialization");
    ShowFatalStartupScreen();
}
```

这里仍由启动边界选择显示安全页面、返回标题页或退出。

## 4. 临时监听错误并显示游戏 UI

`OnError` 是原始 C# event。生命周期短于 GoDoRuntime 的 Node 必须对称解绑：

```csharp
public override void _EnterTree()
{
    ErrorHub.OnError += OnError;
}

public override void _ExitTree()
{
    ErrorHub.OnError -= OnError;
}

private void OnError(ErrorReport report)
{
    if (report.Level >= ErrorLevel.Error)
        ShowErrorToast(report.Message);
}
```

监听者应快速返回，不要修改错误系统状态，也不要在回调中再次调用 ErrorHub。某个监听者抛出异常时，ErrorHub 会隔离它并继续通知其他监听者。

面向玩家的文案通常需要本地化和隐私清理。`ErrorReport.Message` 更适合开发诊断，不应默认原样展示给玩家。

## 5. 添加自定义 Reporter

需要写文件或接入错误平台时，实现 `IErrorReporter`：

```csharp
public sealed class GameErrorReporter : IErrorReporter, IDisposable
{
    public void Report(in ErrorReport report)
    {
        // 只做快速入队；不要在这里同步写磁盘或等待网络。
    }

    public void Dispose()
    {
        // 刷新自己的有限队列并释放资源。
    }
}
```

在一次性 Boot 中注册并保留同一实例：

```csharp
_reporter = new GameErrorReporter();
ErrorHub.AddReporter(_reporter);
```

如果需要提前卸载：

```csharp
ErrorHub.RemoveReporter(_reporter);
_reporter.Dispose();
```

Reporter 在错误分发调用栈上同步执行，因此禁止 `.Wait()`、`.Result` 和同步网络请求。GoDoRuntime 关闭时会清理仍注册的 Reporter，并对实现 `IDisposable` 的实例调用 `Dispose()`。

接入远程平台前还应由游戏项目明确决定用户同意、隐私字段过滤、离线缓存、重试上限和平台合规策略；框架不会替你上传数据。

## 6. 使用运行时 Debugger

启用 `GoDoRuntime.tscn` Autoload 后，Debug 构建会自动出现紧凑状态按钮，不需要配置快捷键。

- 折叠状态显示 FPS 和最近 Warning/Error 数量。
- 点击后可查看 Services、Events、Input、Scheduler 和 Console 页面。
- 展开时每 0.25 秒刷新当前页面；折叠时不会创建模块快照。
- 面板只读，不允许修改服务或游戏数据。
- Release 构建不会创建 Debugger，业务逻辑不能依赖它。

控制台页面只保留有限的最近记录：LogHub 使用 64 条环形历史，面板最多显示最近 5 条普通日志；ErrorHub 摘要容量为 16，页面按等级显示最近记录。它是快速观察工具，不是完整日志存档或性能分析器。

## 后台线程与错误风暴

LogHub 仅允许主线程。ErrorHub 可以从后台线程调用，但报告会先进入最多 1024 条的有界队列，再由 GoDoRuntime 每帧最多分发 256 条；监听者与 Reporter 仍在主线程运行。

队列满时报告会被丢弃，并在主线程汇总为 Warning。后台 Fatal 还会同步写入降级控制台。遇到大量重复错误时，应修复或限流源头，不能把 ErrorHub 当作无限队列。

## 常见错误

- Release 中看不到 Info：这是预期行为；线上失败必须使用 ErrorHub。
- 玩家提示暴露技术细节：不要直接展示异常消息，改为本地化的业务提示。
- 同一异常出现多次：检查是否在多个调用层先上报再抛出。
- 切换场景后回调仍触发：短生命周期对象忘记解绑 `OnError`。
- 上报错误时游戏卡顿：Reporter 在同步写文件、等待锁或请求网络。
- `Fatal` 后游戏仍运行：Fatal 不负责退出，业务边界必须显式采取行动。
- Debugger 在导出版本消失：Release 默认不创建它，这是设计行为。

精确接口可查询 <xref:GoDo.LogHub>、<xref:GoDo.ErrorHub>、<xref:GoDo.ErrorReport>、<xref:GoDo.ErrorLevel> 和 <xref:GoDo.IErrorReporter>。
