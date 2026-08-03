using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 面向业务层的屏幕空间 UI 层级与生命周期服务。
/// <para>
/// 打开 View 或 Modal 时会隔离当前受管理 UI 的键盘/手柄焦点；关闭顶部界面或重开缓存实例时，
/// 服务会尝试恢复最后一个仍有效、可见且可聚焦的控件。
/// 首次打开界面的默认焦点由业务在 Open 返回或 OpenAsync 完成后显式设置。
/// </para>
/// </summary>
public interface IUiService
{
    /// <summary>
    /// 通过 ResourceHub 加载并校验 UI 目录。
    /// <para>存在任何已打开的受管理 UI 时拒绝替换目录。</para>
    /// </summary>
    /// <exception cref="ResourceLoadException">目录资源不存在、加载失败或类型不匹配。</exception>
    /// <exception cref="ConfigValidationException">目录内容未通过完整性校验。</exception>
    /// <exception cref="System.InvalidOperationException">存在已打开的受管理 UI。</exception>
    void LoadUiConfig(ResourceKey key);

    /// <summary>
    /// 按已加载目录中的语义标识和默认层级打开 UI。
    /// <para>目录未加载、标识未注册或 Single 界面已经打开时抛出异常。</para>
    /// </summary>
    /// <exception cref="System.ArgumentException">标识未初始化。</exception>
    /// <exception cref="System.InvalidOperationException">目录未加载或 Single 界面已经打开。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">目录中不存在该标识。</exception>
    /// <exception cref="UiOpenException">UI 资源无法加载、实例化或挂载。</exception>
    Control Open(UiId id);

    /// <summary>
    /// 按已加载配置中的语义标识打开指定根节点类型的 UI，并在加入场景树前完成可选配置。
    /// </summary>
    /// <typeparam name="TView">UI 场景根节点必须继承的类型。</typeparam>
    /// <param name="id">已注册的 UI 标识。</param>
    /// <param name="configure">可选的挂载前配置；回调异常会原样传递，实例不会加入托管栈。</param>
    /// <returns>已加入对应 UI 层的强类型根节点。</returns>
    /// <exception cref="ArgumentException">标识未初始化。</exception>
    /// <exception cref="InvalidOperationException">配置未加载或 Single 界面已经打开。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">配置中不存在该标识。</exception>
    /// <exception cref="UiOpenException">UI 无法加载、实例化、转换为目标类型或挂载。</exception>
    TView Open<TView>(UiId id, Action<TView>? configure = null)
        where TView : Control;

    /// <summary>
    /// 按已加载配置中的语义标识异步加载并打开强类型 UI。
    /// <para>资源加载使用 ResourceHub；节点实例化、配置和挂载仍在 Godot 主线程完成。</para>
    /// </summary>
    /// <typeparam name="TView">UI 场景根节点必须继承的类型。</typeparam>
    /// <param name="id">已注册的 UI 标识。</param>
    /// <param name="configure">可选的挂载前配置；回调异常会使任务失败且不登记实例。</param>
    /// <param name="onProgress">可选的资源加载进度回调；由服务自动清理。</param>
    /// <param name="cancellationToken">取消当前 UI 打开请求；不会取消 ResourceHub 中可能共享的底层资源加载。</param>
    /// <returns>最终返回已挂载强类型 UI 的任务。</returns>
    /// <exception cref="ArgumentException">标识未初始化。</exception>
    /// <exception cref="InvalidOperationException">配置未加载，或 Single 界面已经打开或正在打开。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">配置中不存在该标识。</exception>
    /// <exception cref="ResourceLoadException">无法启动资源异步加载。</exception>
    /// <exception cref="UiOpenException">UI 无法异步加载、实例化、转换为目标类型或提交到配置层。</exception>
    /// <exception cref="OperationCanceledException">调用方或 UiService 取消请求，或 Scene 层请求在加载期间因主场景变更而过期。</exception>
    Task<TView> OpenAsync<TView>(
        UiId id,
        Action<TView>? configure = null,
        Action<float>? onProgress = null,
        CancellationToken cancellationToken = default)
        where TView : Control;

    /// <summary>在指定层同步加载并打开 UI 界面。</summary>
    /// <param name="key">UI PackedScene 的资源键。</param>
    /// <param name="layer">目标 UI 层。</param>
    /// <returns>已加入指定 UI 层的 Control 根节点。</returns>
    /// <exception cref="InvalidOperationException">UiService 尚未完成初始化。</exception>
    /// <exception cref="ArgumentOutOfRangeException">层值未知。</exception>
    /// <exception cref="UiOpenException">UI 无法加载、实例化或提交到目标层。</exception>
    Control Open(ResourceKey key, UiLayer layer);

    /// <summary>在指定层打开指定根节点类型的 UI，并在加入场景树前完成可选配置。</summary>
    /// <typeparam name="TView">UI 场景根节点必须继承的类型。</typeparam>
    /// <param name="key">UI 场景资源键。</param>
    /// <param name="layer">目标 UI 层。</param>
    /// <param name="configure">可选的挂载前配置；回调异常会原样传递，实例不会加入托管栈。</param>
    /// <returns>已加入指定 UI 层的强类型根节点。</returns>
    /// <exception cref="InvalidOperationException">UiService 尚未完成初始化。</exception>
    /// <exception cref="ArgumentOutOfRangeException">层值未知。</exception>
    /// <exception cref="UiOpenException">UI 无法加载、实例化、转换为目标类型或挂载。</exception>
    TView Open<TView>(ResourceKey key, UiLayer layer, Action<TView>? configure = null)
        where TView : Control;

    /// <summary>异步加载并在指定层打开强类型 UI。</summary>
    /// <typeparam name="TView">UI 场景根节点必须继承的类型。</typeparam>
    /// <param name="key">UI 场景资源键。</param>
    /// <param name="layer">目标 UI 层。</param>
    /// <param name="configure">可选的挂载前配置；回调异常会使任务失败且不登记实例。</param>
    /// <param name="onProgress">可选的资源加载进度回调；由服务自动清理。</param>
    /// <param name="cancellationToken">取消当前 UI 打开请求；不会取消 ResourceHub 中可能共享的底层资源加载。</param>
    /// <returns>最终返回已挂载强类型 UI 的任务。</returns>
    /// <exception cref="ArgumentOutOfRangeException">层值未知。</exception>
    /// <exception cref="InvalidOperationException">UiService 尚未完成初始化。</exception>
    /// <exception cref="ResourceLoadException">无法启动资源异步加载。</exception>
    /// <exception cref="UiOpenException">UI 无法异步加载、实例化、转换为目标类型或提交到目标层。</exception>
    /// <exception cref="OperationCanceledException">调用方或 UiService 取消请求，或 Scene 层请求在加载期间因主场景变更而过期。</exception>
    Task<TView> OpenAsync<TView>(
        ResourceKey key,
        UiLayer layer,
        Action<TView>? configure = null,
        Action<float>? onProgress = null,
        CancellationToken cancellationToken = default)
        where TView : Control;

    /// <summary>判断指定已注册标识是否存在打开实例。</summary>
    /// <exception cref="System.ArgumentException">标识未初始化。</exception>
    /// <exception cref="System.InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    bool IsOpen(UiId id);

    /// <summary>获取指定已注册标识当前打开的实例数量。</summary>
    /// <exception cref="System.ArgumentException">标识未初始化。</exception>
    /// <exception cref="System.InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    int GetOpenCount(UiId id);

    /// <summary>判断指定已注册标识是否存在尚未完成的异步打开请求。</summary>
    /// <exception cref="ArgumentException">标识未初始化。</exception>
    /// <exception cref="InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    bool IsOpening(UiId id);

    /// <summary>获取指定已注册标识当前尚未完成的异步打开请求数量。</summary>
    /// <exception cref="ArgumentException">标识未初始化。</exception>
    /// <exception cref="InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    int GetOpeningCount(UiId id);

    /// <summary>
    /// 取消指定已注册标识的全部未完成异步打开请求，并返回本次实际发出取消的请求数。
    /// <para>取消只阻止对应请求继续实例化和挂载 UI，不会中止 ResourceHub 中可能共享的底层资源加载。</para>
    /// </summary>
    /// <exception cref="ArgumentException">标识未初始化。</exception>
    /// <exception cref="InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    int CancelOpenRequests(UiId id);

    /// <summary>
    /// 取消指定 UI 层的全部未完成异步打开请求，并返回本次实际发出取消的请求数。
    /// <para>取消只阻止对应请求继续实例化和挂载 UI，不会中止 ResourceHub 中可能共享的底层资源加载。</para>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">层值未知。</exception>
    int CancelOpenRequests(UiLayer layer);

    /// <summary>判断指定已注册标识是否存在已关闭且可复用的缓存实例。</summary>
    /// <exception cref="ArgumentException">标识未初始化。</exception>
    /// <exception cref="InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    bool HasCachedInstance(UiId id);

    /// <summary>清理指定已注册标识的缓存实例；打开中和加载中的实例不受影响。</summary>
    /// <returns>存在缓存登记并已清理时返回 true，否则返回 false。</returns>
    /// <exception cref="ArgumentException">标识未初始化。</exception>
    /// <exception cref="InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    bool ClearCachedInstance(UiId id);

    /// <summary>清理全部已关闭且可复用的缓存实例；打开中和加载中的实例不受影响。</summary>
    /// <returns>本次移除的缓存登记数量。</returns>
    /// <exception cref="InvalidOperationException">UI 服务尚未初始化。</exception>
    int ClearCachedInstances();

    /// <summary>尝试获取指定已注册标识最上层的实例。</summary>
    /// <exception cref="System.ArgumentException">标识未初始化。</exception>
    /// <exception cref="System.InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    bool TryGetTop(UiId id, out Control? view);

    /// <summary>尝试获取指定已注册标识最上层的强类型实例。</summary>
    /// <typeparam name="TView">UI 场景根节点必须继承的类型。</typeparam>
    /// <param name="id">已注册的 UI 标识。</param>
    /// <param name="view">成功时返回最上层实例；没有打开实例时返回 null。</param>
    /// <returns>存在打开实例时返回 true，否则返回 false。</returns>
    /// <exception cref="ArgumentException">标识未初始化。</exception>
    /// <exception cref="InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    /// <exception cref="InvalidCastException">已打开实例的根节点不兼容 <typeparamref name="TView"/>。</exception>
    bool TryGetTop<TView>(UiId id, out TView? view)
        where TView : Control;

    /// <summary>尝试获取指定 UI 层最上层或最后打开的实例。</summary>
    /// <exception cref="System.ArgumentOutOfRangeException">层值未知。</exception>
    bool TryGetTop(UiLayer layer, out Control? view);

    /// <summary>关闭指定受管理界面；目标无效或不受管理时抛出异常。</summary>
    /// <exception cref="System.ArgumentNullException">目标为 null。</exception>
    /// <exception cref="System.InvalidOperationException">目标已经释放或不受服务管理。</exception>
    void Close(Control view);

    /// <summary>尝试关闭指定受管理实例；目标已经释放或不受管理时返回 false。</summary>
    /// <exception cref="System.ArgumentNullException">目标为 null。</exception>
    bool TryClose(Control view);

    /// <summary>尝试关闭指定标识最上层的实例；该标识没有打开时返回 false。</summary>
    /// <exception cref="System.ArgumentException">标识未初始化。</exception>
    /// <exception cref="System.InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    bool TryClose(UiId id);

    /// <summary>关闭指定标识的全部实例，并返回实际关闭数量。</summary>
    /// <exception cref="System.ArgumentException">标识未初始化。</exception>
    /// <exception cref="System.InvalidOperationException">UiConfig 尚未加载。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    int CloseAll(UiId id);

    /// <summary>关闭指定 UI 层的全部实例，并返回实际关闭数量。</summary>
    /// <exception cref="System.ArgumentOutOfRangeException">层值未知。</exception>
    int CloseAll(UiLayer layer);

    /// <summary>
    /// 保留指定实例并关闭其显示层级之上的全部受管理 UI，返回实际关闭数量。
    /// </summary>
    /// <exception cref="System.ArgumentNullException">目标为 null。</exception>
    /// <exception cref="System.InvalidOperationException">目标已经释放或不受服务管理。</exception>
    int CloseTo(Control view);

    /// <summary>
    /// 保留指定标识最上层的实例并关闭其显示层级之上的全部受管理 UI，返回实际关闭数量。
    /// </summary>
    /// <exception cref="System.ArgumentException">标识未初始化。</exception>
    /// <exception cref="System.InvalidOperationException">UiConfig 尚未加载或该标识没有打开实例。</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">UiConfig 中不存在该标识。</exception>
    int CloseTo(UiId id);

    /// <summary>优先关闭顶部模态，其次返回前一个 View；没有可返回界面时返回 false。</summary>
    bool TryGoBack();
}
