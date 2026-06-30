using System;
using System.Collections.Generic;
using Godot;

namespace GoDo;

/// <summary>
/// GoDo 框架级统一错误捕获器。
/// <para>
/// 框架内所有模块通过此类上报错误，不直接调用 <c>GD.PrintErr</c>。
/// 外部可通过 <see cref="AddReporter"/> 挂载自定义上报逻辑（Sentry、自建服务器等）。
/// </para>
/// <example>
/// <code>
/// // 框架模块内部使用
/// ErrorHandler.Report(ex, "EventChannel", context: "Dispatch");
/// ErrorHandler.Warn("重复注册 handler", "EventChannel");
///
/// // 业务层挂载自定义上报器
/// GoDo.ErrorHandler.AddReporter(new MyServerReporter("https://errors.mygame.com"));
///
/// // 监听所有错误事件
/// GoDo.ErrorHandler.OnError += report => GD.Print(report);
/// </code>
/// </example>
/// </summary>
public static class ErrorHandler
{
    // ── 内部状态 ──────────────────────────────────────────────────────────────

    private static readonly List<IErrorReporter> _reporters = new(2);
    private static readonly object _reportersLock = new();

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

        Dispatch(BuildReport(
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

        Dispatch(BuildReport(level, module, message, context, exception: null));
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
    /// </summary>
    public static void Fatal(Exception exception, string module, string? context = null)
    {
        if (exception == null) return;
        Dispatch(BuildReport(
            ErrorLevel.Fatal,
            module,
            exception.Message,
            context,
            exception));
    }

    // ── 内部实现 ──────────────────────────────────────────────────────────────

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

        // 1. 内置 Godot 日志输出
        GodotLog(in report);

        // 2. 触发事件（供业务层监听）
        try
        {
            OnError?.Invoke(report);
        }
        catch (Exception ex)
        {
            // 避免 OnError 回调崩溃淹没原始报告
            GD.PrintErr($"[GoDo.ErrorHandler] OnError 回调内部异常: {ex.Message}");
        }

        // 3. 分发给所有自定义上报器
        //    先在锁内取快照，避免遍历期间其他线程 Add/Remove 导致异常，
        //    也避免上报器执行耗时逻辑（如网络 IO）时长期占用锁。
        IErrorReporter[] reportersSnapshot;
        lock (_reportersLock)
        {
            if (_reporters.Count == 0) return;
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
                // 上报器自身崩溃不影响其他上报器
                GD.PrintErr($"[GoDo.ErrorHandler] 上报器 [{reportersSnapshot[i].GetType().Name}] 内部异常: {ex.Message}");
            }
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
            ? $"[GoDo.{report.Module}] [{LevelLabel(report.Level)}] {report.Message}"
            : $"[GoDo.{report.Module}] [{LevelLabel(report.Level)}] ({report.Context}) {report.Message}";

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
