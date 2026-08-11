using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>
/// 面向业务层的长期服务注册表。仅保存显式注册的服务接口，不负责自动构造、依赖注入或释放服务实例。
/// 所有成员只能在 GoDoRuntime 所在的 Godot 主线程调用。
/// </summary>
public static class Services
{
    private static readonly Dictionary<Type, object> _registrations = new();

    /// <summary>
    /// 注册一个服务接口。相同接口不能重复注册。
    /// </summary>
    /// <typeparam name="TService">用于查询服务的接口类型；不能是具体类型。</typeparam>
    /// <param name="service">要保存的长期服务实例。注册表只持有引用，不负责释放实例。</param>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><typeparamref name="TService"/> 不是接口。</exception>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，GoDoRuntime 尚未初始化，或相同接口已经注册。</exception>
    public static void Register<TService>(TService service) where TService : class
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(service);

        Type serviceType = typeof(TService);
        if (!serviceType.IsInterface)
            throw new ArgumentException($"服务必须按接口注册: {serviceType.FullName}", nameof(TService));

        if (!_registrations.TryAdd(serviceType, service))
            throw new InvalidOperationException($"服务接口已注册: {serviceType.FullName}");
    }

    /// <summary>
    /// 获取已注册的服务；缺失时明确抛出异常。
    /// </summary>
    /// <typeparam name="TService">注册时使用的服务接口类型。</typeparam>
    /// <returns>该接口当前注册的同一个服务实例。</returns>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程、GoDoRuntime 尚未初始化，或该接口尚未注册。</exception>
    public static TService Get<TService>() where TService : class
    {
        MainThreadGuard.VerifyAccess();

        if (_registrations.TryGetValue(typeof(TService), out object? service))
            return (TService)service;

        throw new InvalidOperationException($"服务接口尚未注册: {typeof(TService).FullName}");
    }

    /// <summary>
    /// 尝试获取已注册的服务。
    /// </summary>
    /// <typeparam name="TService">注册时使用的服务接口类型。</typeparam>
    /// <param name="service">找到时为当前注册实例；缺失时为 <see langword="null"/>。</param>
    /// <returns>找到注册实例时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    public static bool TryGet<TService>(out TService? service) where TService : class
    {
        MainThreadGuard.VerifyAccess();

        if (_registrations.TryGetValue(typeof(TService), out object? registered))
        {
            service = (TService)registered;
            return true;
        }

        service = null;
        return false;
    }

    /// <summary>
    /// 仅当当前注册实例与调用方提供的实例相同时注销服务。
    /// </summary>
    /// <typeparam name="TService">注册时使用的服务接口类型。</typeparam>
    /// <param name="service">预期正在注册的同一个服务实例。</param>
    /// <returns>成功移除匹配实例时为 <see langword="true"/>；接口未注册或实例不匹配时为 <see langword="false"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="service"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    public static bool Unregister<TService>(TService service) where TService : class
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(service);

        Type serviceType = typeof(TService);
        if (!_registrations.TryGetValue(serviceType, out object? registered) ||
            !ReferenceEquals(registered, service))
        {
            return false;
        }

        return _registrations.Remove(serviceType);
    }

    internal static void Clear()
    {
        MainThreadGuard.VerifyAccess();
        _registrations.Clear();
    }

#if DEBUG
    /// <summary>返回当前已注册服务接口及其实现类型的 Debug 快照。</summary>
    internal static ServiceDebugEntry[] GetDebugSnapshot()
    {
        MainThreadGuard.VerifyAccess();

        var snapshot = new ServiceDebugEntry[_registrations.Count];
        int index = 0;
        foreach (KeyValuePair<Type, object> registration in _registrations)
        {
            snapshot[index] = new ServiceDebugEntry(
                registration.Key,
                registration.Value.GetType());
            index++;
        }

        Array.Sort(snapshot, CompareServiceEntries);
        return snapshot;
    }

    private static int CompareServiceEntries(ServiceDebugEntry left, ServiceDebugEntry right) =>
        string.CompareOrdinal(left.ServiceType.FullName, right.ServiceType.FullName);

    internal readonly record struct ServiceDebugEntry(
        Type ServiceType,
        Type ImplementationType);
#endif
}
