using System;

#nullable enable

namespace GoDo;

/// <summary>表示主内容场景切换失败。</summary>
public sealed class SceneChangeException : Exception
{
    /// <summary>目标场景资源键。</summary>
    public ResourceKey Key { get; }

    /// <summary>创建场景切换异常。</summary>
    public SceneChangeException(ResourceKey key, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
    }
}
