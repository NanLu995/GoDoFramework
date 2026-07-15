using System;

namespace GoDo;

/// <summary>CameraService 无法完成镜头注册、激活或停用操作时抛出的异常。</summary>
public sealed class CameraOperationException : Exception
{
    /// <summary>相关镜头 ID。</summary>
    public CameraId CameraId { get; }

    /// <summary>创建带镜头上下文的异常。</summary>
    public CameraOperationException(CameraId cameraId, string message)
        : base(message)
    {
        CameraId = cameraId;
    }

    /// <summary>创建带镜头上下文和内部异常的异常。</summary>
    public CameraOperationException(CameraId cameraId, string message, Exception innerException)
        : base(message, innerException)
    {
        CameraId = cameraId;
    }
}
