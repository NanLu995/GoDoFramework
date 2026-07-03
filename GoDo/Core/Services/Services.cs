using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>
/// 面向业务层的长期服务注册表。仅保存显式注册的服务接口，不负责自动构造或依赖注入。
/// </summary>
public static class Services
{
    private static readonly Dictionary<Type, object> _registrations = new();

    /// <summary>
    /// 注册一个服务接口。相同接口不能重复注册。
    /// </summary>
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
}
