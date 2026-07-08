# ErrorHub 使用指南

## 定位与优势

ErrorHub 是框架与业务层共享的结构化错误出口，提供等级、模块、上下文、异常、UTC 时间和调用栈信息。它隔离 `OnError` 监听者与 Reporter 的异常，防止递归上报，并把后台线程报告放入有界队列，由 GoDoRuntime 在主线程按帧分发。

ErrorHub 用于记录错误，不是普通业务日志系统，也不会替调用方决定重试、回退或退出游戏。

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
ErrorHub.Debug("资源已命中缓存", "Resources");
ErrorHub.Fatal("启动所需配置不可用", "Bootstrap");
```

`Fatal` 只表示最高严重等级，**不会主动退出游戏**；是否调用 `GetTree().Quit()` 由业务边界决定。

## 等级与过滤

| 等级 | 用途 | Debug 默认 | Release 默认 |
|---|---|---|---|
| `Debug` | 开发期细节 | 输出 | 调用点被编译移除 |
| `Warning` | 可恢复的异常情况 | 输出 | 输出 |
| `Error` | 当前操作失败 | 输出 | 输出 |
| `Fatal` | 最高严重等级 | 输出 | 输出 |

```csharp
ErrorHub.MinLevel = ErrorLevel.Warning;
```

`Debug()` 带 `[Conditional("DEBUG")]`，Release / ExportRelease 下连参数表达式也不会求值。过滤后的普通等级不会构造 `ErrorReport`。

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
- `RemoteReporterStub` 只是扩展骨架，不是生产可用的远程上报实现。

## 后台线程与队列

- 后台线程可以调用 ErrorHub，但监听者和 Reporter 不会在后台线程执行。
- 报告进入最大 1024 条的有界队列，GoDoRuntime 每帧最多分发 256 条。
- 队列满时会丢弃报告并在主线程汇总警告；后台 Fatal 还会同步写入降级控制台。
- 应控制错误风暴源头，不能把有界队列当作无限日志缓冲。

## GoDoRuntime 兜底

GoDoRuntime 安装 `AppDomain.UnhandledException`，以 `Fatal`、模块 `Runtime` 上报进程级未处理异常。它不承诺捕获所有已经被 Godot 引擎处理的脚本回调异常。

## 自动回归验证

`Verification/Automated/ErrorHubRegression.tscn` 验证最低等级过滤、结构化异常报告、Reporter 引用去重与移除、OnError 与 Reporter 异常隔离，以及 Fatal 只上报不主动退出。runner 会恢复原始 `MinLevel` 并对称移除自己的监听者和 Reporter，不调用全局 Shutdown。

```powershell
Godot_v4.7-stable_mono_win64_console.exe --headless --path . Verification/Automated/ErrorHubRegression.tscn
```

当前 runner 已通过 `dotnet build` 编译，并在 Godot 4.7 Mono Headless 中完成 6/6 项验证；成功退出码为 0，失败退出码为 1。测试会刻意产生 Warning、Error、Fatal 和降级隔离日志，以断言结果与进程退出码判断成功。

## 常见误用

| 应该 | 避免 |
|---|---|
| 填写稳定的模块名和有价值的 context | 只写“出错了” |
| 在功能边界决定恢复或退出 | 认为 `Fatal` 会自动退出 |
| Reporter 快速返回 | 在 Reporter 内同步联网 |
| 对称解绑 `OnError` | 短生命周期对象永久订阅 |
| 捕获异常后只上报一次 | 先 Report 再抛给上层重复 Report |
