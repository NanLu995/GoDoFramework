using System;

#nullable enable

namespace GoDo;

/// <summary>表示主内容场景切换失败。</summary>
public sealed class SceneChangeException : Exception
{
    /// <summary>目标场景资源键。</summary>
    public ResourceKey Key { get; }

    /// <summary>失败发生时所在的场景切换阶段。</summary>
    public SceneChangePhase Phase { get; }

    /// <summary>
    /// 创建未指定失败阶段的场景切换异常。
    /// <para><see cref="Phase"/> 将设为 <see cref="SceneChangePhase.Unknown"/>。</para>
    /// </summary>
    /// <param name="key">目标场景资源键。</param>
    /// <param name="message">描述失败原因的消息。</param>
    /// <param name="innerException">导致场景切换失败的内部异常。</param>
    public SceneChangeException(ResourceKey key, string message, Exception? innerException = null)
        : this(key, SceneChangePhase.Unknown, message, innerException)
    {
    }

    /// <summary>创建包含结构化失败阶段的场景切换异常。</summary>
    /// <param name="key">目标场景资源键。</param>
    /// <param name="phase">失败发生时所在的生命周期阶段。</param>
    /// <param name="message">描述失败原因的消息。</param>
    /// <param name="innerException">导致场景切换失败的内部异常。</param>
    public SceneChangeException(
        ResourceKey key,
        SceneChangePhase phase,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
        Phase = phase;
    }
}
