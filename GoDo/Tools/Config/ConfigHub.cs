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
