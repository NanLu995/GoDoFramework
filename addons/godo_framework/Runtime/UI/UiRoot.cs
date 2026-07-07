using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>承载 GoDo 管理的屏幕空间 UI，不包含具体业务逻辑。</summary>
public sealed partial class UiRoot : Node
{
    internal Control SceneRoot { get; private set; } = null!;
    internal Control ViewRoot { get; private set; } = null!;
    internal Control ModalRoot { get; private set; } = null!;

    /// <summary>Scene 层挂载根节点路径。</summary>
    [Export] public NodePath SceneRootPath { get; set; } = null!;

    /// <summary>View 层挂载根节点路径。</summary>
    [Export] public NodePath ViewRootPath { get; set; } = null!;

    /// <summary>Modal 层挂载根节点路径。</summary>
    [Export] public NodePath ModalRootPath { get; set; } = null!;

    internal bool IsInitialized =>
        IsInstanceValid(SceneRoot) && IsInstanceValid(ViewRoot) && IsInstanceValid(ModalRoot);

    public override void _Ready()
    {
        SceneRoot = GetNodeOrNull<Control>(SceneRootPath)!;
        ViewRoot = GetNodeOrNull<Control>(ViewRootPath)!;
        ModalRoot = GetNodeOrNull<Control>(ModalRootPath)!;
        if (!IsInitialized)
            throw new InvalidOperationException("UiRoot 缺少 SceneRoot、ViewRoot 或 ModalRoot 子节点。");
    }
}
