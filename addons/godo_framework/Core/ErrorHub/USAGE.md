# ErrorHub 使用指南

## 定位与优势

ErrorHub 是框架与业务层共享的结构化错误出口，提供等级、模块、上下文、异常、UTC 时间和调用栈信息。它隔离 `OnError` 监听者与 Reporter 的异常，防止递归上报，并把后台线程报告放入有界队列，由 GoDoRuntime 在主线程按帧分发。

ErrorHub 用于记录错误，不是普通业务日志系统，也不会替调用方决定重试、回退或退出游戏。

普通业务代码可以使用 `LogHub.Warn`、`LogHub.Error` 和 `LogHub.Fatal` 作为更紧凑的调用入口；这些方法直接进入 ErrorHub 的同一条结构化报告管线。需要注册 Reporter、监听 `OnError`、修改 `MinLevel` 或直接构造指定等级报告时，仍使用 ErrorHub。

## 快速上手

```csharp
try
{
    LoadSave();
}
catch (Exception exception)
{
    ErrorHub.Report(exception, "Save", context: "Load slot=1");
}

ErrorHub.Warn("配置项缺失，使用默认值", "Config", context: "Audio.Volume");
LogHub.Debug("资源已命中缓存", "Resources");
ErrorHub.Fatal("启动所需配置不可用", "Bootstrap");
```

同一类型反复记录同一模块时，可以绑定一次模块名：

```csharp
private static readonly LogChannel Log = LogHub.For("Save");

Log.Warn("备份缺失，继续读取主存档");
Log.Error(exception, context: "slot=slot-1");
```

`Fatal` 只表示最高严重等级，**不会主动退出游戏**；是否调用 `GetTree().Quit()` 由业务边界决定。

正常流程的开发诊断应使用 `LogHub.Debug` 或 `LogHub.Info`。ErrorHub 只接收 Warning、Error 与 Fatal 级别的异常和失败信息。

## 等级与过滤

| 等级 | 用途 | Debug 默认 | Release 默认 |
|---|---|---|---|
| `Warning` | 可恢复的异常情况 | 输出 | 输出 |
| `Error` | 当前操作失败 | 输出 | 输出 |
| `Fatal` | 最高严重等级 | 输出 | 输出 |

```csharp
ErrorHub.MinLevel = ErrorLevel.Warning;
```

过滤后的等级不会构造 `ErrorReport`。

## 异常链与输出策略

异常重载会保留原始 `Exception`，包装异常与 `InnerException` 的语义不变。各输出端统一从异常链生成有界诊断文本：

- Godot 控制台始终显示 `Cause` 根因摘要；Debug 构建额外显示完整 `ExceptionChain`，Release 不输出完整堆栈。
- 本地滚动文件保存根因摘要和完整异常链，便于导出后离线排障；日志文件可能包含路径等技术信息，不应直接展示给玩家或未经审查上传。
- Debugger 只保存上下文和根因摘要字符串，不持有原始异常或完整堆栈；上下文与根因都可搜索。
- 自定义远程 Reporter 应先复制需要的有界字符串，并根据隐私策略决定是否上传堆栈。内置模板只复制根因摘要，不跨异步边界持有异常对象。

上层异常应描述“哪个操作失败”，最内层异常应尽量说明“缺少什么、值是什么或下一步检查什么”。ErrorHub 负责保真呈现原因，不会根据异常类型猜测修复方案。

## 监听与生命周期

`OnError` 是原始 C# event。生命周期短于 GoDoRuntime 的订阅者必须对称解绑：

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

不要在 `OnError` 或 Reporter 内再次调用 ErrorHub；递归报告会走降级输出，不再进入监听链。

## 自定义 Reporter

```csharp
public sealed class FileReporter : IErrorReporter, IDisposable
{
    public void Report(in ErrorReport report)
    {
        // 保持轻量；需要 I/O 时交给自己的有界后台队列。
    }

    public void Dispose()
    {
        // 刷新并释放资源。
    }
}

var reporter = new FileReporter();
ErrorHub.AddReporter(reporter);
ErrorHub.RemoveReporter(reporter);
```

- Reporter 按实例引用去重和移除。
- Reporter 在错误分发调用栈上同步执行，禁止 `.Wait()`、`.Result` 或同步网络请求。
- GoDoRuntime Shutdown 会清理监听者，并 Dispose 仍在注册且实现 `IDisposable` 的 Reporter。
- `RemoteErrorReporterTemplate` 只是扩展骨架，不是生产可用的远程上报实现。

## 后台线程与队列

- 后台线程可以调用 ErrorHub，但监听者和 Reporter 不会在后台线程执行。
- 报告进入最大 1024 条的有界队列，GoDoRuntime 每帧最多分发 256 条。
- 队列满时会丢弃报告并在主线程汇总警告；后台 Fatal 还会同步写入降级控制台。
- 应控制错误风暴源头，不能把有界队列当作无限日志缓冲。

上述后台队列以 GoDoRuntime 已经进入场景树并记录主线程为前提。初始化之前调用 ErrorHub 会在调用线程同步分发，不提供启动前的后台线程安全保证；启动阶段的后台工作应等 Runtime 就绪后再上报，或先由调用方保存结果并回到主线程处理。

GoDoRuntime 默认注册本地滚动文件 Reporter，将 Warning、Error 与 Fatal 写入 `user://logs`。其磁盘 I/O 使用独立的 2048 条有界后台队列；文件写入失败不会递归调用 ErrorHub，运行时只输出一次降级警告。

## GoDoRuntime 兜底

GoDoRuntime 安装 `AppDomain.UnhandledException`，以 `Fatal`、模块 `Runtime` 上报进程级未处理异常。它不承诺捕获所有已经被 Godot 引擎处理的脚本回调异常。

## 自动回归验证

`Verification/Automated/ErrorHubRegression.tscn` 验证最低等级过滤、结构化异常报告、嵌套与聚合异常诊断、Reporter 引用去重与移除、OnError 与 Reporter 异常隔离、递归上报降级、Fatal 只上报不主动退出，以及后台队列满汇总。runner 会恢复原始 `MinLevel` 并对称移除自己的监听者和 Reporter，不调用全局 Shutdown。

```powershell
& $env:GODOT_PATH --headless --path . Verification/Automated/ErrorHubRegression.tscn
```

Debug runner 包含 9 项验证，Release runner 包含 8 项；成功退出码为 0，失败退出码为 1。测试会刻意产生 Warning、Error、Fatal 和降级隔离日志，以断言结果与进程退出码判断成功。

## 常见误用

| 应该 | 避免 |
|---|---|
| 填写稳定的模块名和有价值的 context | 只写“出错了” |
| 在功能边界决定恢复或退出 | 认为 `Fatal` 会自动退出 |
| Reporter 快速返回 | 在 Reporter 内同步联网 |
| 对称解绑 `OnError` | 短生命周期对象永久订阅 |
| 捕获异常后只上报一次 | 先 Report 再抛给上层重复 Report |
