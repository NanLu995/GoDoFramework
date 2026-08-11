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

    /// <summary>创建包含资源键、目标分组与可选底层原因的音频播放异常。</summary>
    /// <param name="key">加载或播放准备失败的音频资源键。</param>
    /// <param name="group">请求目标的 BGM 或 SFX 分组。</param>
    /// <param name="message">描述失败阶段的消息。</param>
    /// <param name="innerException">资源加载或 Godot 播放准备抛出的底层异常；没有时为 <see langword="null"/>。</param>
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
