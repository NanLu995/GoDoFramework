using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 远程上报器骨架：将错误异步发送到远程服务器（Sentry、自建后台等）。
/// <para>
/// 复制此文件、重命名类名，并在 <c>SendAsync</c> 中实现实际的网络请求，
/// 然后调用 <c>GoDo.ErrorHub.AddReporter(new MyServerReporter(url))</c> 注册。
/// </para>
/// <para>
/// ⚠️ 重要：<see cref="IErrorReporter.Report"/> 在 <c>ErrorHub.Dispatch</c> 的调用栈上
/// 同步执行。如果在这里直接 <c>.Wait()</c> 或 <c>.Result</c> 阻塞等待网络请求，
/// 会让"上报一个错误"这个动作本身有阻塞主线程甚至死锁的风险
/// （尤其是 Fatal 错误往往发生在游戏状态已经不稳定的时刻，
/// 这时候最不需要的就是再卡死主线程）。
/// 本骨架通过 fire-and-forget（<c>_ = SendAsync(...)</c>）规避这个问题：
/// 网络请求在后台运行，失败也只会打印一行日志，绝不向上抛出或阻塞调用方。
/// </para>
/// </summary>
public sealed class RemoteErrorReporterTemplate : IErrorReporter
{
    private readonly string _endpoint;

    /// <summary>创建一个将报告异步投递到指定终结点的模板实例。</summary>
    /// <param name="endpoint">远程服务器 URL，例如 <c>https://errors.mygame.com/report</c>；该值由具体实现负责验证和使用。</param>
    public RemoteErrorReporterTemplate(string endpoint)
    {
        _endpoint = endpoint;
    }

    /// <summary>同步入口：仅负责把报告丢进后台任务，立即返回，不阻塞调用方。</summary>
    /// <param name="report">要投递的结构化错误报告；其字段会在返回前复制，避免跨异步延续保留 <c>in</c> 引用。</param>
    public void Report(in ErrorReport report)
    {
        // 必须先把需要的字段从 in 引用的 struct 中取出，
        // 因为 report 的生命周期不保证能跨越到异步延续之后。
        var level   = report.Level;
        var module  = report.Module;
        var message = report.Message;
        var context = report.Context;
        var time    = report.Timestamp;

        // fire-and-forget：故意不 await，也不让异常向上传播到 ErrorHub.Dispatch。
        _ = SendAsync(level, module, message, context, time);
    }

    private async System.Threading.Tasks.Task SendAsync(
        ErrorLevel level,
        string module,
        string message,
        string? context,
        System.DateTime timestamp)
    {
        try
        {
            // TODO: 序列化并通过 HttpClient 异步 POST 到 _endpoint。
            // 建议 Release 模式下不携带 StackTrace，减少上报体积与隐私风险。
            //
            // var payload = JsonSerializer.Serialize(new {
            //     level, module, message, context, time = timestamp,
            // });
            // using var client = SharedHttpClient.Instance; // 复用单例，不要每次 new HttpClient
            // await client.PostAsync(_endpoint, new StringContent(payload));

            await System.Threading.Tasks.Task.CompletedTask;
        }
        catch (System.Exception ex)
        {
            // 网络层失败绝不可以再抛回 ErrorHub，否则可能死循环上报错误。
            GD.PrintErr($"[RemoteErrorReporterTemplate] 上报到 {_endpoint} 失败: {ex.Message}");
        }
    }
}
