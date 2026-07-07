using System;

#nullable enable

namespace GoDo;

/// <summary>表示音频资源加载或播放准备失败。</summary>
public sealed class AudioPlaybackException : Exception
{
    /// <summary>失败的音频资源键。</summary>
    public ResourceKey Key { get; }

    /// <summary>目标音频分组。</summary>
    public AudioGroup Group { get; }

    /// <summary>创建音频播放异常。</summary>
    public AudioPlaybackException(
        ResourceKey key,
        AudioGroup group,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
        Group = group;
    }
}
