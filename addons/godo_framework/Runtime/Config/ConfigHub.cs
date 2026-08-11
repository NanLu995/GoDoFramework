using System;
using Godot;

namespace GoDo;

/// <summary>通过 ResourceHub 加载并校验强类型 Godot Resource 配置。</summary>
public static class ConfigHub
{
    /// <summary>
    /// 同步加载指定配置资源并执行其完整性校验。
    /// 资源加载失败时抛出 ResourceLoadException，内容校验失败时抛出 ConfigValidationException。
    /// </summary>
    /// <typeparam name="T">同时继承 <see cref="Resource"/> 并实现 <see cref="IConfigResource"/> 的配置类型。</typeparam>
    /// <param name="key">通过 <see cref="ResourceKey.Create"/> 或 <see cref="ResourceKey.FromPath"/> 创建的配置资源键。</param>
    /// <returns>已由 ResourceHub 加载并通过 <see cref="IConfigResource.Validate"/> 校验的非空资源。</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> 未初始化。</exception>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，或 ResourceHub 尚未初始化。</exception>
    /// <exception cref="ResourceLoadException">资源不存在、加载失败或实际类型与 <typeparamref name="T"/> 不匹配。</exception>
    /// <exception cref="ConfigValidationException">配置校验主动抛出该异常，或其他校验异常被包装并保留为内部异常。</exception>
    public static T Load<T>(ResourceKey key) where T : Resource, IConfigResource
    {
        T config = ResourceHub.Load<T>(key);
        try
        {
            config.Validate();
            return config;
        }
        catch (Exception exception) when (exception is not ConfigValidationException)
        {
            throw new ConfigValidationException(
                key,
                typeof(T),
                $"配置资源未通过内容校验: {key.Value}",
                exception);
        }
    }
}
