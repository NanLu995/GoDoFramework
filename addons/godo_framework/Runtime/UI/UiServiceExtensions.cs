using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>为 <see cref="IUiService"/> 打开的 UI 实例提供限定作用域的所有权辅助方法。</summary>
public static class UiServiceExtensions
{
    /// <summary>
    /// 按已加载的配置打开 UI，并返回一个在释放时关闭该实例的所有权 Scope。
    /// </summary>
    /// <typeparam name="TView">UI 场景根节点必须兼容的类型。</typeparam>
    /// <param name="service">已初始化的 UI 服务。</param>
    /// <param name="id">已注册的 UI 标识。</param>
    /// <param name="configure">可选的挂载前配置回调。</param>
    /// <returns>持有已打开 UI 实例的 Scope。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> 未初始化。</exception>
    /// <exception cref="InvalidOperationException">
    /// 不在 Godot 主线程调用、UI 配置尚未加载，或 Single 实例已经打开。
    /// </exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">
    /// <paramref name="id"/> 未注册。
    /// </exception>
    /// <exception cref="UiOpenException">UI 无法加载、实例化、转换为目标类型或挂载。</exception>
    public static UiScope<TView> OpenScoped<TView>(
        this IUiService service,
        UiId id,
        Action<TView>? configure = null)
        where TView : Control
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(service);
        return new UiScope<TView>(service, service.Open(id, configure));
    }

    /// <summary>
    /// 在指定层打开 UI 资源，并返回一个在释放时关闭该实例的所有权 Scope。
    /// </summary>
    /// <typeparam name="TView">UI 场景根节点必须兼容的类型。</typeparam>
    /// <param name="service">已初始化的 UI 服务。</param>
    /// <param name="key">UI PackedScene 的资源键。</param>
    /// <param name="layer">目标 UI 层。</param>
    /// <param name="configure">可选的挂载前配置回调。</param>
    /// <returns>持有已打开 UI 实例的 Scope。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">不在 Godot 主线程调用。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="layer"/> 未知。</exception>
    /// <exception cref="UiOpenException">UI 无法加载、实例化、转换为目标类型或挂载。</exception>
    public static UiScope<TView> OpenScoped<TView>(
        this IUiService service,
        ResourceKey key,
        UiLayer layer,
        Action<TView>? configure = null)
        where TView : Control
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(service);
        return new UiScope<TView>(service, service.Open(key, layer, configure));
    }
}
