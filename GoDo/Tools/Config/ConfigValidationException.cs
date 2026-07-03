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
