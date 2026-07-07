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

    /// <summary>创建一条包含资源定位与类型上下文的加载异常。</summary>
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
