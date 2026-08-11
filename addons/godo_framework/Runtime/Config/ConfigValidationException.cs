using System;

#nullable enable

namespace GoDo;

/// <summary>表示配置资源已加载，但内容未通过业务定义的完整性校验。</summary>
public sealed class ConfigValidationException : Exception
{
    /// <summary>校验失败的资源键。</summary>
    public ResourceKey Key { get; }

    /// <summary>校验失败的配置资源类型。</summary>
    public Type ConfigType { get; }

    /// <summary>创建配置校验异常。</summary>
    /// <param name="key">校验失败的资源键；构造器原样保存，不验证其初始化状态。</param>
    /// <param name="configType">校验失败的配置资源类型。</param>
    /// <param name="message">面向开发者的校验失败描述。</param>
    /// <param name="innerException">配置 <c>Validate()</c> 抛出的具体原因。</param>
    /// <exception cref="ArgumentNullException"><paramref name="configType"/> 为 <see langword="null"/>。</exception>
    public ConfigValidationException(
        ResourceKey key,
        Type configType,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
        ConfigType = configType ?? throw new ArgumentNullException(nameof(configType));
    }
}
