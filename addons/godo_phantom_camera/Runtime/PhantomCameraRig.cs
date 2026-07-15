using System;
using Godot;
using PhantomCamera;

#nullable enable

namespace GoDo;

/// <summary>将 GoDo 主镜头激活语义映射到 Phantom Camera 3D 优先级。</summary>
public sealed partial class PhantomCameraRig : CameraRig
{
    private PhantomCamera3D? _phantomCamera;

    /// <summary>由 Phantom Camera 3D 脚本驱动的节点。</summary>
    [Export]
    public Node3D PhantomCameraNode { get; set; } = null!;

    /// <summary>CameraService 激活本 Rig 时写入的 Phantom Camera 优先级。</summary>
    [Export]
    public int ActivePriority { get; set; } = 20;

    /// <summary>CameraService 停用本 Rig 或 Rig 初始化时写入的 Phantom Camera 优先级。</summary>
    [Export]
    public int InactivePriority { get; set; }

    /// <inheritdoc />
    protected override void OnRigReady() => InitializeBackend();

    /// <inheritdoc />
    protected override void OnRigExitTree() => _phantomCamera = null;

    /// <inheritdoc />
    protected override void ActivateRig() => GetBackend().Priority = ActivePriority;

    /// <inheritdoc />
    protected override void DeactivateRig() => GetBackend().Priority = InactivePriority;

    internal void InitializeBackend()
    {
        if (!GodotObject.IsInstanceValid(PhantomCameraNode))
            throw new InvalidOperationException($"PhantomCameraRig '{RigId}' 缺少 PhantomCameraNode。");
        if (ActivePriority <= InactivePriority)
        {
            throw new InvalidOperationException(
                $"PhantomCameraRig '{RigId}' 的 ActivePriority 必须大于 InactivePriority。");
        }
        if (!PhantomCameraNode.HasMethod(global::PhantomCamera.PhantomCamera.MethodName.GetPriority) ||
            !PhantomCameraNode.HasMethod(global::PhantomCamera.PhantomCamera.MethodName.SetPriority))
        {
            throw new InvalidOperationException(
                $"PhantomCameraRig '{RigId}' 引用的节点不是兼容的 Phantom Camera 3D 节点。");
        }

        _phantomCamera = PhantomCameraNode.AsPhantomCamera3D();
        _phantomCamera.Priority = InactivePriority;
    }

    private PhantomCamera3D GetBackend() =>
        _phantomCamera ??
        throw new InvalidOperationException($"PhantomCameraRig '{RigId}' 尚未完成初始化。");
}
