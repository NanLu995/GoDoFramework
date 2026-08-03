using System;

#nullable enable

namespace GoDo;

/// <summary>表示 UI 资源加载、实例化或挂载失败。</summary>
public sealed class UiOpenException : Exception
{
    /// <summary>打开失败的 UI 资源键。</summary>
    public ResourceKey Key { get; }

    /// <summary>失败发生时所在的 UI 打开阶段。</summary>
    public UiOpenPhase Phase { get; }

    /// <summary>
    /// 创建未指定失败阶段的 UI 打开异常。
    /// <para><see cref="Phase"/> 将设为 <see cref="UiOpenPhase.Unknown"/>。</para>
    /// </summary>
    /// <param name="key">打开失败的 UI 资源键。</param>
    /// <param name="message">描述失败原因的消息。</param>
    /// <param name="innerException">导致 UI 打开失败的内部异常。</param>
    public UiOpenException(ResourceKey key, string message, Exception? innerException = null)
        : this(key, UiOpenPhase.Unknown, message, innerException)
    {
    }

    /// <summary>创建包含结构化失败阶段的 UI 打开异常。</summary>
    /// <param name="key">打开失败的 UI 资源键。</param>
    /// <param name="phase">失败发生时所在的生命周期阶段。</param>
    /// <param name="message">描述失败原因的消息。</param>
    /// <param name="innerException">导致 UI 打开失败的内部异常。</param>
    public UiOpenException(
        ResourceKey key,
        UiOpenPhase phase,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
        Phase = phase;
    }
}
