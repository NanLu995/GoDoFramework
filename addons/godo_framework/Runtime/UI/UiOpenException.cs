using System;

#nullable enable

namespace GoDo;

/// <summary>表示 UI 资源加载、实例化或挂载失败。</summary>
public sealed class UiOpenException : Exception
{
    /// <summary>打开失败的 UI 资源键。</summary>
    public ResourceKey Key { get; }

    /// <summary>创建 UI 打开异常。</summary>
    public UiOpenException(ResourceKey key, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
    }
}
