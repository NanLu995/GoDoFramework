namespace GoDo;

/// <summary>
/// 可插拔的错误上报器接口。
/// 实现该接口并通过 <see cref="ErrorHandler.AddReporter"/> 注册，
/// 即可将错误数据转发到任意目标（本地日志、Sentry、自建服务器等）。
/// </summary>
public interface IErrorReporter
{
    /// <summary>
    /// 接收一条错误报告并执行上报逻辑。
    /// 实现中应捕获所有异常——上报器自身崩溃不应影响主流程。
    /// </summary>
    /// <param name="report">包含完整错误信息的只读快照。</param>
    void Report(in ErrorReport report);
}
