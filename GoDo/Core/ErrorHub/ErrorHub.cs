using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// GoDo 框架级统一错误中心。
/// <para>
/// 框架内所有模块通过此类上报错误，不直接调用 <c>GD.PrintErr</c>。
/// 外部可通过 <see cref="AddReporter"/> 挂载自定义上报逻辑（Sentry、自建服务器等）。
/// </para>
/// <example>
/// <code>
/// // 框架模块内部使用
/// ErrorHub.Report(ex, "EventChannel", context: "Dispatch");
/// ErrorHub.Warn("重复注册 handler", "EventChannel");
///
/// // 业务层挂载自定义上报器
/// GoDo.ErrorHub.AddReporter(new MyServerReporter("https://errors.mygame.com"));
///
/// // 监听所有错误事件
/// GoDo.ErrorHub.OnError += report => GD.Print(report);
/// </code>
/// </example>
/// </summary>
public static class ErrorHub
{
    private const int MaxPendingReports = 1024;
    private const int MaxReportsPerFlush = 256;

    // ── 内部状态 ──────────────────────────────────────────────────────────────

    private static readonly List<IErrorReporter> _reporters = new(2);
    private static readonly object _reportersLock = new();
    private static readonly ConcurrentQueue<ErrorReport> _pendingReports = new();
    private static int _pendingReportCount;
    private static int _droppedReportCount;

    // 错误处理链自身再次报错时必须走降级输出，不能递归进入 Reporter/OnError。
    [ThreadStatic]
    private static int _dispatchDepth;

    /// <summary>
    /// 每当有错误被上报时触发。
    /// 订阅者应避免在回调中再次调用 Report，防止递归。
    /// </summary>
    public static event Action<ErrorReport>? OnError;

    // ── 最低上报等级（可在运行时动态调整）────────────────────────────────────

    /// <summary>
    /// 低于此等级的错误将被静默丢弃。
    /// 默认值：Debug 模式为 <see cref="ErrorLevel.Debug"/>，
    /// Release 模式为 <see cref="ErrorLevel.Warning"/>。
    /// </summary>
#if GODOT_DEBUG
    public static ErrorLevel MinLevel { get; set; } = ErrorLevel.Debug;
#else
    public static ErrorLevel MinLevel { get; set; } = ErrorLevel.Warning;
#endif

    // ── 上报器管理 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 注册一个自定义上报器。同一实例（按引用比较）不会被重复添加，
    /// 与上报器自身是否重写 <see cref="object.Equals(object)"/> 无关。
    /// </summary>
    /// <param name="reporter">实现 <see cref="IErrorReporter"/> 的上报器。</param>
    public static void AddReporter(IErrorReporter reporter)
    {
        if (reporter == null) return;

        lock (_reportersLock)
        {
            // 显式引用比较，避免自定义 Equals 导致"按配置相同"误判为重复
            for (int i = 0; i < _reporters.Count; i++)
            {
                if (ReferenceEquals(_reporters[i], reporter))
                    return;
            }
            _reporters.Add(reporter);
        }
    }

    /// <summary>
    /// 移除一个已注册的上报器（按引用比较）。
    /// </summary>
    public static void RemoveReporter(IErrorReporter reporter)
    {
        if (reporter == null) return;

        lock (_reportersLock)
        {
            for (int i = 0; i < _reporters.Count; i++)
            {
                if (ReferenceEquals(_reporters[i], reporter))
                {
                    _reporters.RemoveAt(i);
                    return;
                }
            }
        }
    }

    // ── 核心上报 API ──────────────────────────────────────────────────────────

    /// <summary>
    /// 上报一个带异常的错误（等级 <see cref="ErrorLevel.Error"/>）。
    /// </summary>
    /// <param name="exception">原始异常对象。</param>
    /// <param name="module">来源模块名称，如 "EventChannel"。</param>
    /// <param name="context">可选上下文描述，如方法名、节点路径。</param>
    public static void Report(
        Exception exception,
        string module,
        string? context = null)
    {
        if (exception == null) return;
        if (ErrorLevel.Error < MinLevel) return; // 提前过滤，避免无谓的 BuildReport 开销

        Submit(BuildReport(
            ErrorLevel.Error,
            module,
            exception.Message,
            context,
            exception));
    }

    /// <summary>
    /// 上报一条指定等级的消息（无关联异常）。
    /// </summary>
    /// <param name="level">错误等级。</param>
    /// <param name="message">人类可读的描述。</param>
    /// <param name="module">来源模块名称。</param>
    /// <param name="context">可选上下文。</param>
    public static void Report(
        ErrorLevel level,
        string message,
        string module,
        string? context = null)
    {
        if (level < MinLevel) return; // 提前过滤，避免无谓的 BuildReport 开销

        Submit(BuildReport(level, module, message, context, exception: null));
    }

    /// <summary>
    /// 上报一条 <see cref="ErrorLevel.Warning"/> 级别消息的便捷方法。
    /// </summary>
    public static void Warn(string message, string module, string? context = null)
        => Report(ErrorLevel.Warning, message, module, context);

    /// <summary>
    /// 上报一条 <see cref="ErrorLevel.Debug"/> 级别消息的便捷方法。
    /// 在 Release 构建中，若 <see cref="MinLevel"/> 高于 Debug，则无任何开销。
    /// </summary>
    [System.Diagnostics.Conditional("GODOT_DEBUG")]
    public static void Debug(string message, string module, string? context = null)
        => Report(ErrorLevel.Debug, message, module, context);

    /// <summary>
    /// 上报一条 <see cref="ErrorLevel.Fatal"/> 级别消息。
    /// </summary>
    public static void Fatal(string message, string module, string? context = null)
        => Report(ErrorLevel.Fatal, message, module, context);

    /// <summary>
    /// 上报一条带异常的 <see cref="ErrorLevel.Fatal"/> 消息。
    /// Fatal 仅表示最高严重等级，不会主动终止游戏；是否退出由调用方决定。
    /// </summary>
    public static void Fatal(Exception exception, string module, string? context = null)
    {
        if (exception == null) return;
        Submit(BuildReport(
            ErrorLevel.Fatal,
            module,
            exception.Message,
            context,
            exception));
    }

    // ── 内部实现 ──────────────────────────────────────────────────────────────

    /// <summary>在 Godot 主线程分发后台线程排队的报告。</summary>
    internal static void FlushPending()
    {
        if (MainThreadGuard.IsInitialized)
            MainThreadGuard.VerifyAccess();

        int processedCount = 0;
        while (processedCount < MaxReportsPerFlush &&
               _pendingReports.TryDequeue(out ErrorReport report))
        {
            Interlocked.Decrement(ref _pendingReportCount);
            Dispatch(in report);
            processedCount++;
        }

        int droppedCount = Interlocked.Exchange(ref _droppedReportCount, 0);
        if (droppedCount > 0)
        {
            ErrorReport droppedReport = BuildReport(
                ErrorLevel.Warning,
                "ErrorHub",
                $"后台错误队列已满，丢弃 {droppedCount} 条报告。",
                context: $"MaxPendingReports={MaxPendingReports}",
                exception: null);
            Dispatch(in droppedReport);
        }
    }

    /// <summary>清理静态监听者和 Reporter，由 GoDoRuntime 退出时调用。</summary>
    internal static void Shutdown()
    {
        if (MainThreadGuard.IsInitialized)
            MainThreadGuard.VerifyAccess();

        while (!_pendingReports.IsEmpty)
            FlushPending();
        OnError = null;

        IErrorReporter[] reporters;
        lock (_reportersLock)
        {
            reporters = _reporters.ToArray();
            _reporters.Clear();
        }

        for (int i = 0; i < reporters.Length; i++)
        {
            if (reporters[i] is not IDisposable disposable)
                continue;

            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    $"[GoDo.ErrorHub] Reporter [{reporters[i].GetType().Name}] Dispose 失败: {exception}");
            }
        }

        while (_pendingReports.TryDequeue(out _)) { }
        Volatile.Write(ref _pendingReportCount, 0);
        Volatile.Write(ref _droppedReportCount, 0);
    }

    private static void Submit(in ErrorReport report)
    {
        if (MainThreadGuard.IsInitialized && !MainThreadGuard.IsMainThread)
        {
            int pendingCount = Interlocked.Increment(ref _pendingReportCount);
            if (pendingCount > MaxPendingReports)
            {
                Interlocked.Decrement(ref _pendingReportCount);
                Interlocked.Increment(ref _droppedReportCount);

                if (report.Level == ErrorLevel.Fatal)
                    Console.Error.WriteLine($"[GoDo.ErrorHub] 队列已满，Fatal 报告未入队: {report}");
                return;
            }

            _pendingReports.Enqueue(report);

            // 进程终止阶段可能没有下一帧可供 FlushPending，至少保留同步降级输出。
            if (report.Level == ErrorLevel.Fatal)
                Console.Error.WriteLine($"[GoDo.ErrorHub] {report}");
            return;
        }

        Dispatch(in report);
    }

    private static ErrorReport BuildReport(
        ErrorLevel level,
        string module,
        string message,
        string? context,
        Exception? exception)
    {
        return new ErrorReport
        {
            Level     = level,
            Module    = module ?? "Unknown",
            Message   = message ?? string.Empty,
            Context   = context,
            Exception = exception,
            Timestamp = DateTime.UtcNow,
#if GODOT_DEBUG
            // 有异常时直接用异常自带的栈；没有异常时，只在 Fatal 级别才
            // 额外捕获当前调用栈（Environment.StackTrace 开销不小，
            // 不应该让高频的 Debug/Warn 调用都承担这个成本）。
            StackTrace = exception?.StackTrace
                         ?? (level == ErrorLevel.Fatal ? Environment.StackTrace : null),
#else
            StackTrace = exception?.StackTrace,
#endif
        };
    }

    private static void Dispatch(in ErrorReport report)
    {
        // 等级过滤
        if (report.Level < MinLevel) return;

        if (_dispatchDepth > 0)
        {
            FallbackLog("检测到递归错误上报，已跳过监听者与 Reporter", in report);
            return;
        }

        _dispatchDepth++;
        try
        {
            // 1. 内置 Godot 日志输出
            GodotLog(in report);

            // 2. 逐个触发监听者。多播委托直接 Invoke 时，一个监听者抛出异常
            // 会阻止后续监听者，因此这里必须逐个隔离。
            var onError = OnError;
            if (onError != null)
            {
                Delegate[] listeners = onError.GetInvocationList();
                for (int i = 0; i < listeners.Length; i++)
                {
                    try
                    {
                        ((Action<ErrorReport>)listeners[i]).Invoke(report);
                    }
                    catch (Exception ex)
                    {
                        FallbackLog($"OnError 监听者 [{listeners[i].Method.Name}] 内部异常: {ex.Message}", in report);
                    }
                }
            }

            // 3. 在锁内取快照，在锁外逐个调用 Reporter。
            IErrorReporter[] reportersSnapshot;
            lock (_reportersLock)
            {
                if (_reporters.Count == 0)
                    return;

                reportersSnapshot = _reporters.ToArray();
            }

            for (int i = 0; i < reportersSnapshot.Length; i++)
            {
                try
                {
                    reportersSnapshot[i].Report(in report);
                }
                catch (Exception ex)
                {
                    FallbackLog($"Reporter [{reportersSnapshot[i].GetType().Name}] 内部异常: {ex.Message}", in report);
                }
            }
        }
        catch (Exception ex)
        {
            // ErrorHub 自身必须保证不把异常抛回业务流程。
            FallbackLog($"错误分发器内部异常: {ex.Message}", in report);
        }
        finally
        {
            _dispatchDepth--;
        }
    }

    private static void FallbackLog(string reason, in ErrorReport originalReport)
    {
        // 降级路径绝不再调用 ErrorHub，避免形成递归。
        if (MainThreadGuard.IsInitialized && !MainThreadGuard.IsMainThread)
        {
            Console.Error.WriteLine($"[GoDo.ErrorHub] {reason}; 原始报告: {originalReport}");
            return;
        }

        try
        {
            GD.PrintErr($"[GoDo.ErrorHub] {reason}; 原始报告: {originalReport}");
        }
        catch
        {
            // 错误系统的最后防线：即使 Godot 日志接口不可用，也不能继续抛出。
        }
    }

    private static void GodotLog(in ErrorReport report)
    {
        string formatted = FormatForConsole(in report);

        switch (report.Level)
        {
            case ErrorLevel.Debug:
#if GODOT_DEBUG
                GD.Print(formatted);
#endif
                break;

            case ErrorLevel.Warning:
                GD.PushWarning(formatted);
                break;

            case ErrorLevel.Error:
            case ErrorLevel.Fatal:
                GD.PushError(formatted);
                break;
        }
    }

    private static string FormatForConsole(in ErrorReport report)
    {
        // 注意：不用共享/池化的 StringBuilder——错误处理本身有可能在处理过程中
        // 再触发另一条错误上报（例如本类型转换失败），共享 buffer 会被嵌套调用污染。
        // 这里改为直接拼接，对短字符串而言比手动管理可复用 StringBuilder 更安全，
        // 且没有额外的状态需要维护。

        string head = string.IsNullOrEmpty(report.Context)
            ? $"[{report.Module}] [{LevelLabel(report.Level)}] {report.Message}"
            : $"[{report.Module}] [{LevelLabel(report.Level)}] ({report.Context}) {report.Message}";

#if GODOT_DEBUG
        // Debug 下才拼接异常类型与调用栈，这部分本身就比 head 大得多，
        // 没必要在 Release（Warning/Error/Fatal 仍会输出）路径上保留它的拼接逻辑。
        if (report.Exception != null || !string.IsNullOrEmpty(report.StackTrace))
        {
            string exceptionPart = report.Exception != null
                ? $"\n  Exception: {report.Exception.GetType().Name}"
                : string.Empty;

            string stackPart = !string.IsNullOrEmpty(report.StackTrace)
                ? $"\n  StackTrace:\n{report.StackTrace}"
                : string.Empty;

            return head + exceptionPart + stackPart;
        }
#endif

        return head;
    }

    // 等级标签预先大写好，避免每次调用 ToString().ToUpperInvariant() 的双重分配
    // （枚举 ToString 本身分配一次字符串，ToUpperInvariant 再分配一次）。
    private static string LevelLabel(ErrorLevel level) => level switch
    {
        ErrorLevel.Debug   => "DEBUG",
        ErrorLevel.Warning => "WARNING",
        ErrorLevel.Error   => "ERROR",
        ErrorLevel.Fatal   => "FATAL",
        _                  => "UNKNOWN",
    };
}
