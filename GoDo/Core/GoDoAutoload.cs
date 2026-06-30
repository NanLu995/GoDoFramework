using Godot;

namespace GoDo;

/// <summary>
/// GoDo 框架的 Godot Autoload 节点。
/// <para>
/// 将此节点添加到项目的 Autoload 列表（Project → Project Settings → Autoload），
/// 路径填写本文件的路径，名称建议设为 "GoDoAutoload"。
/// </para>
/// <para>
/// 职责：钩入 <see cref="System.AppDomain"/> 的未处理异常事件，
/// 确保游戏内所有未被 try/catch 捕获的异常都经过 <see cref="ErrorHandler"/> 统一处理。
/// </para>
/// <para>
/// 生命周期说明：本节点应作为 Autoload 全局唯一存在。
/// <see cref="System.AppDomain.CurrentDomain"/> 是进程级单例，其生命周期跨越整局游戏，
/// 因此订阅必须在 <see cref="_ExitTree"/> 中精确反订阅，
/// 并通过 <see cref="_subscribed"/> 防止编辑器热重载等场景下重复订阅。
/// </para>
/// </summary>
public sealed partial class GoDoAutoload : Node
{
    // 防止场景重载 / 编辑器热重载导致 _Ready 被多次调用时重复订阅，
    // 否则同一个 AppDomain 异常会被 ErrorHandler 处理 N 次（N = 重载次数）。
    private bool _subscribed;

    public override void _Ready()
    {
        if (!_subscribed)
        {
            System.AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            _subscribed = true;
        }

        ErrorHandler.Debug("GoDoAutoload 初始化完成", "GoDoAutoload");
    }

    public override void _ExitTree()
    {
        if (_subscribed)
        {
            System.AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
            _subscribed = false;
        }
    }

    // ── 私有处理 ──────────────────────────────────────────────────────────────

    private static void OnDomainUnhandledException(
        object sender,
        System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is System.Exception ex)
        {
            ErrorHandler.Fatal(
                ex,
                module: "AppDomain",
                context: e.IsTerminating ? "IsTerminating=true" : null);
        }
        else
        {
            ErrorHandler.Fatal(
                $"未知未处理异常对象: {e.ExceptionObject}",
                module: "AppDomain");
        }
    }
}
