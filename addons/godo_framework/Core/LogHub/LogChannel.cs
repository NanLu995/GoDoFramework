using System;
using System.Diagnostics;

#nullable enable

namespace GoDo;

/// <summary>
/// 绑定固定模块名的轻量日志通道。
/// <para>通过 <see cref="LogHub.For(string)"/> 创建并复用；默认值不包含有效模块，不能用于日志调用。</para>
/// </summary>
public readonly struct LogChannel
{
    private readonly string _module;

    internal LogChannel(string module)
    {
        _module = module;
    }

    /// <summary>输出开发期细节日志；仅限 Godot 主线程，Release 构建会在调用点移除。</summary>
    /// <param name="message">可读的诊断描述；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentException">消息为空，或当前通道是没有有效模块的默认值。</exception>
    [Conditional("DEBUG")]
    public void Debug(string message, string? context = null) =>
        LogHub.Debug(message, _module, context);

    /// <summary>输出开发期低频正常流程里程碑；仅限 Godot 主线程，Release 构建会在调用点移除。</summary>
    /// <param name="message">可读的里程碑描述；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentException">消息为空，或当前通道是没有有效模块的默认值。</exception>
    [Conditional("DEBUG")]
    public void Info(string message, string? context = null) =>
        LogHub.Info(message, _module, context);

    /// <summary>上报可恢复的异常情况或降级结果。</summary>
    /// <param name="message">可读的降级描述；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentException">消息为空，或当前通道是没有有效模块的默认值。</exception>
    public void Warn(string message, string? context = null) =>
        LogHub.Warn(message, _module, context);

    /// <summary>上报没有关联异常对象的当前操作失败。</summary>
    /// <param name="message">可读的失败描述；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentException">消息为空，或当前通道是没有有效模块的默认值。</exception>
    public void Error(string message, string? context = null) =>
        LogHub.Error(message, _module, context);

    /// <summary>上报带有原始异常对象的当前操作失败。</summary>
    /// <param name="exception">需要保留的原始异常对象。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> 为 null。</exception>
    /// <exception cref="ArgumentException">当前通道是没有有效模块的默认值。</exception>
    public void Error(Exception exception, string? context = null) =>
        LogHub.Error(exception, _module, context);

    /// <summary>
    /// 上报没有关联异常对象的最高严重等级错误；不会主动退出游戏。
    /// </summary>
    /// <param name="message">可读的致命错误描述；不能为 null、空或全空白。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentException">消息为空，或当前通道是没有有效模块的默认值。</exception>
    public void Fatal(string message, string? context = null) =>
        LogHub.Fatal(message, _module, context);

    /// <summary>
    /// 上报带有原始异常对象的最高严重等级错误；不会主动退出游戏。
    /// </summary>
    /// <param name="exception">需要保留的原始异常对象。</param>
    /// <param name="context">可选的定位上下文。</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> 为 null。</exception>
    /// <exception cref="ArgumentException">当前通道是没有有效模块的默认值。</exception>
    public void Fatal(Exception exception, string? context = null) =>
        LogHub.Fatal(exception, _module, context);
}
