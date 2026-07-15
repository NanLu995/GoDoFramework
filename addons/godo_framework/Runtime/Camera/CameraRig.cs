using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 场景侧主镜头适配器基类；负责随节点生命周期向 CameraService 注册和注销。
/// </summary>
public abstract partial class CameraRig : Node, ICameraRigDriver
{
    private CameraService? _cameraService;
    private CameraId _cameraId;

    /// <summary>业务语义镜头 ID；节点进入场景树后不可修改。</summary>
    [Export]
    public string RigId { get; set; } = string.Empty;

    CameraId ICameraRigDriver.CameraId => _cameraId;

    GodotObject ICameraRigDriver.LifetimeOwner => this;

    /// <inheritdoc />
    public sealed override void _Ready()
    {
        _cameraId = CameraId.Create(RigId);
        OnRigReady();

        if (Services.Get<ICameraService>() is not CameraService service)
            throw new InvalidOperationException("ICameraService 不是 GoDo CameraService 实例，CameraRig 无法注册。");

        _cameraService = service;
        service.Register(this, FindSceneScopeRoot());
    }

    /// <inheritdoc />
    public sealed override void _ExitTree()
    {
        if (_cameraService != null)
        {
            _cameraService.Unregister(this);
            _cameraService = null;
        }

        OnRigExitTree();
    }

    /// <summary>派生适配器在注册前解析并验证插件节点引用。</summary>
    protected virtual void OnRigReady() { }

    /// <summary>派生适配器在注销后清理自身临时引用。</summary>
    protected virtual void OnRigExitTree() { }

    /// <summary>由 CameraService 调用以激活实际镜头后端。</summary>
    protected abstract void ActivateRig();

    /// <summary>由 CameraService 调用以停用实际镜头后端。</summary>
    protected abstract void DeactivateRig();

    void ICameraRigDriver.Activate() => ActivateRig();

    void ICameraRigDriver.Deactivate() => DeactivateRig();

    private Node FindSceneScopeRoot()
    {
        Node root = this;
        Node? parent = root.GetParent();
        Window treeRoot = GetTree().Root;
        while (parent != null && parent != treeRoot)
        {
            root = parent;
            parent = root.GetParent();
        }

        return root;
    }
}

internal interface ICameraRigDriver
{
    CameraId CameraId { get; }
    GodotObject LifetimeOwner { get; }
    void Activate();
    void Deactivate();
}
