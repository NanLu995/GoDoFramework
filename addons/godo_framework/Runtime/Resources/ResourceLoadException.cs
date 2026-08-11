using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>资源不存在、类型不匹配或 Godot 加载失败时抛出的异常。</summary>
public sealed class ResourceLoadException : Exception
{
    /// <summary>发生错误的资源键。</summary>
    public ResourceKey Key { get; }

    /// <summary>调用方请求的资源类型。</summary>
    public Type RequestedType { get; }

    /// <summary>Godot 启动请求时返回的错误码；不适用时为 null。</summary>
    public Error? ErrorCode { get; }

    /// <summary>创建一条包含资源定位、请求类型和可选 Godot 错误码的加载异常。</summary>
    /// <param name="key">发生失败的资源键。</param>
    /// <param name="requestedType">调用方请求的资源类型。</param>
    /// <param name="message">描述失败原因的消息。</param>
    /// <param name="errorCode">Godot 启动加载请求返回的错误码；不适用时为 <see langword="null"/>。</param>
    /// <param name="innerException">导致当前加载失败的底层异常；没有时为 <see langword="null"/>。</param>
    public ResourceLoadException(
        ResourceKey key,
        Type requestedType,
        string message,
        Error? errorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Key = key;
        RequestedType = requestedType;
        ErrorCode = errorCode;
    }
}
