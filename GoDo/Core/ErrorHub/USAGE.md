# GoDo.ErrorHub 使用指南

## 快速上手

### 1. 上报一个异常

```csharp
try
{
    DoSomethingRisky();
}
catch (Exception ex)
{
    // 等级自动为 Error
    GoDo.ErrorHub.Report(ex, "EventChannel", context: "Dispatch<PlayerDiedEvent>");
}
```

### 2. 上报一条消息（不需要异常对象）

```csharp
// Warning：非致命，业务可继续运行
GoDo.ErrorHub.Warn("重复注册 handler，已跳过", "EventChannel");

// Debug：仅 Debug 构建输出，Release 下整个调用被编译器抹除，零开销
GoDo.ErrorHub.Debug("Bind 目标节点不在树中", "EventChannel", context: $"node={node.Name}");

// Fatal：框架无法继续运行的严重错误
GoDo.ErrorHub.Fatal("ServiceLocator 未初始化", "ServiceLocator");
```

### 3. 监听所有错误（业务层埋点统计、弹窗提示等）

```csharp
public override void _Ready()
{
    GoDo.ErrorHub.OnError += OnAnyError;
}

private void OnAnyError(ErrorReport report)
{
    if (report.Level >= ErrorLevel.Error)
        ShowErrorToast(report.Message);
}
```

## 错误等级

| 等级 | 含义 | Debug 默认 | Release 默认 |
|---|---|---|---|
| `Debug` | 调试信息，无关紧要 | 输出 | **不输出**（调用被编译器移除） |
| `Warning` | 非致命，业务可继续 | 输出 | 输出 |
| `Error` | 当前操作失败，框架可恢复 | 输出 | 输出 |
| `Fatal` | 框架无法继续正常运行 | 输出 | 输出 |

```csharp
// 运行时可调整最低上报等级，低于此等级的直接丢弃
GoDo.ErrorHub.MinLevel = ErrorLevel.Warning;
```

> ⚠️ `Debug()` 方法标记了 `[Conditional("GODOT_DEBUG")]`。
> 在 Release 构建中，**调用点本身会被编译器整体移除**，连参数表达式都不会求值——这比"运行时判断等级再丢弃"快得多，放心在高频路径上用。

## 接入远程上报（Sentry / 自建服务器）

```csharp
public class MyServerReporter : IErrorReporter
{
    private readonly string _endpoint;
    public MyServerReporter(string endpoint) => _endpoint = endpoint;

    public void Report(in ErrorReport report)
    {
        // 序列化 report 并 POST 到 _endpoint
        // 建议：Release 模式下裁剪 StackTrace，减少上报体积
    }
}

// 注册（通常在 GoDoRuntime._Ready 或游戏启动入口）
GoDo.ErrorHub.AddReporter(new MyServerReporter("https://errors.mygame.com"));

// 不再需要时可移除
GoDo.ErrorHub.RemoveReporter(reporter);
```

`RemoteReporterStub` 已提供骨架，复制改名即可填入序列化与 HTTP 逻辑。

## ErrorReport 数据结构

```csharp
public readonly struct ErrorReport
{
    public ErrorLevel  Level;       // Debug / Warning / Error / Fatal
    public string      Module;      // 来源模块，如 "EventChannel"
    public string      Message;     // 错误描述
    public string?     Context;     // 额外上下文（节点名、事件类型等），可为空
    public Exception?  Exception;   // 原始异常，无异常时为 null
    public DateTime    Timestamp;   // UTC 时间
    public string?     StackTrace;  // Debug 下总是有值；Release 下仅在有异常时有值
}
```

`ToString()` 已重写为单行摘要，直接 `GD.Print(report)` 就能看清楚。

## 未处理异常兜底：GoDoRuntime

`GoDoRuntime.tscn` 作为 Autoload 常驻时，会把进程级未处理异常交给 `ErrorHub` 留下最后一份报告：

- **`AppDomain.UnhandledException`**：C# 层任何未被 try/catch 捕获的异常，自动以 `Fatal` 等级上报，`Module` 固定为 `"AppDomain"`。
- 后续场景切换、节点生命周期相关的钩子，也会陆续挂载在这个节点上。

```
Project → Project Settings → Autoload
  Path: res://GoDo/Core/GoDoRuntime.tscn
  Name: GoDoRuntime
```

## 注意事项

| ✅ 应该 | ❌ 避免 |
|---|---|
| 框架模块统一用 `ErrorHub.Report/Warn/Debug/Fatal` | 模块内直接 `GD.PrintErr` |
| `Module` 参数填具体模块名（"EventChannel"） | 留空或随便写 |
| 高频路径上的调试信息用 `Debug()` | 用 `Warn()` 代替 `Debug()`，导致 Release 也有开销 |
| 自定义上报器内部自行 try/catch | 假设上报器一定不会抛异常 |
| `OnError` 回调里只做轻量处理（埋点、UI 提示） | 在 `OnError` 回调里再调用 `Report`（可能递归） |

## 选哪个 API？

| 场景 | 推荐方式 |
|---|---|
| catch 到了异常，需要记录 | `ErrorHub.Report(ex, module, context)` |
| 没有异常，只是想提示一句"这里不对" | `ErrorHub.Warn(message, module)` |
| 仅 Debug 阶段想看的细节日志 | `ErrorHub.Debug(message, module)` |
| 框架已经没法继续跑下去了 | `ErrorHub.Fatal(...)` |
| 想把所有错误转发到 Sentry / 自建后台 | 实现 `IErrorReporter` + `AddReporter` |
| 想在业务层做统一弹窗 / 埋点 | 订阅 `ErrorHub.OnError` |
