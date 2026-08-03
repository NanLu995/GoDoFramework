using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 在明确的代码作用域内持有一个由 <see cref="IUiService"/> 打开的 UI 实例。
/// </summary>
/// <typeparam name="TView">受管理 UI 的根节点类型。</typeparam>
/// <remarks>
/// 必须在 Godot 主线程释放 Scope。释放操作通过 <see cref="IUiService.TryClose(Control)"/> 尝试关闭实例，
/// 且可安全重复调用。Scope 不提供终结器兜底，因为垃圾回收线程不能安全操作 Godot 对象。
/// </remarks>
public sealed class UiScope<TView> : IDisposable where TView : Control
{
    private IUiService? _service;

    internal UiScope(IUiService service, TView view)
    {
        _service = service;
        View = view;
    }

    /// <summary>
    /// 获取当前 Scope 持有的 UI 实例。
    /// </summary>
    /// <remarks>
    /// 释放后仍可读取该引用以进行身份比较，但 Godot 对象可能已经等待删除或失效；
    /// 再次使用前必须检查实例有效性。
    /// </remarks>
    public TView View { get; }

    /// <summary>获取当前 Scope 是否已经释放 UI 所有权。</summary>
    public bool IsDisposed => _service is null;

    /// <summary>
    /// 释放所有权，并请求最初打开实例的 UI 服务关闭该受管理界面。
    /// </summary>
    /// <remarks>
    /// 重复调用不会再次关闭；界面已经被其他所有者关闭时也会正常完成。
    /// UI 服务抛出的异常会原样向调用方传播，但 Scope 仍标记为已释放，避免对可能已部分完成的关闭重复执行清理。
    /// </remarks>
    /// <exception cref="InvalidOperationException">不在 Godot 主线程调用。</exception>
    public void Dispose()
    {
        MainThreadGuard.VerifyAccess();
        IUiService? service = _service;
        if (service is null)
            return;

        _service = null;
        service.TryClose(View);
    }
}
