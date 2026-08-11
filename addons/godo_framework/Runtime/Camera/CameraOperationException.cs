using System;

namespace GoDo;

/// <summary>CameraService 无法完成镜头注册、激活或停用操作时抛出的异常。</summary>
public sealed class CameraOperationException : Exception
{
    /// <summary>相关镜头 ID。</summary>
    public CameraId CameraId { get; }

    /// <summary>创建带镜头上下文的操作异常。</summary>
    /// <param name="cameraId">注册、激活或停用失败所关联的镜头 ID。</param>
    /// <param name="message">描述失败阶段与结果的消息。</param>
    public CameraOperationException(CameraId cameraId, string message)
        : base(message)
    {
        CameraId = cameraId;
    }

    /// <summary>创建带镜头上下文和底层失败原因的操作异常。</summary>
    /// <param name="cameraId">注册、激活或停用失败所关联的镜头 ID。</param>
    /// <param name="message">描述失败阶段与回滚结果的消息。</param>
    /// <param name="innerException">实际镜头后端抛出的原始异常。</param>
    public CameraOperationException(CameraId cameraId, string message, Exception innerException)
        : base(message, innerException)
    {
        CameraId = cameraId;
    }
}
