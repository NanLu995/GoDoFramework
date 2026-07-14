using System;
using System.Collections.Generic;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 管理屏幕空间 UI 的显示层、生命周期与返回顺序。
/// <para>本服务必须位于场景树中，并且所有调用都必须发生在 Godot 主线程。</para>
/// </summary>
public sealed partial class UiService : Node, IUiService
{
    private readonly List<Control> _sceneViews = new();
    private readonly List<Control> _views = new();
    private readonly List<ModalEntry> _modals = new();
    private UiRoot? _root;

    internal void Initialize(UiRoot root)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(root);
        if (_root != null)
            throw new InvalidOperationException("UiService 已经完成初始化。");
        if (!IsInsideTree() || !root.IsInsideTree() || !root.IsInitialized)
            throw new InvalidOperationException("UiService 和 UiRoot 必须完成场景树初始化。");

        _root = root;
        EventChannel.Bind<FrameworkMainSceneChangedEvent>(this, OnMainSceneChanged);
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _sceneViews.Clear();
        _views.Clear();
        _modals.Clear();
        _root = null;
    }

    /// <summary>在指定层打开 UI 界面。</summary>
    public Control Open(ResourceKey key, UiLayer layer)
    {
        VerifyReady();
        return layer switch
        {
            UiLayer.Scene => OpenSceneView(key),
            UiLayer.View => OpenView(key),
            UiLayer.Modal => OpenModal(key),
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知 UI 层。")
        };
    }

    /// <summary>关闭由本服务管理的界面。</summary>
    public void Close(Control view)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(view);
        VerifyReady();

        int sceneIndex = _sceneViews.IndexOf(view);
        if (sceneIndex >= 0)
        {
            _sceneViews.RemoveAt(sceneIndex);
            view.QueueFree();
            return;
        }

        if (_modals.Count > 0)
        {
            ModalEntry top = _modals[^1];
            if (top.View != view)
                throw new InvalidOperationException("只能关闭当前最上层的 UI。请先关闭顶部模态。");

            _modals.RemoveAt(_modals.Count - 1);
            top.Host.QueueFree();
            return;
        }

        if (_views.Count == 0 || _views[^1] != view)
            throw new InvalidOperationException("目标 UI 不受服务管理，或不是当前最上层 View。");

        _views.RemoveAt(_views.Count - 1);
        view.QueueFree();
        if (_views.Count > 0)
            _views[^1].Show();
    }

    /// <summary>优先关闭顶部模态，其次返回前一个 View；没有可返回界面时返回 false。</summary>
    public bool TryGoBack()
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();

        if (_modals.Count > 0)
        {
            Close(_modals[^1].View);
            return true;
        }

        if (_views.Count > 0)
        {
            Close(_views[^1]);
            return true;
        }

        return false;
    }

    private Control OpenSceneView(ResourceKey key)
    {
        Control view = InstantiateView(key);
        AddToRoot(view, _root!.SceneRoot, key, UiLayer.Scene);
        _sceneViews.Add(view);
        return view;
    }

    private Control OpenView(ResourceKey key)
    {
        Control view = InstantiateView(key);
        AddToRoot(view, _root!.ViewRoot, key, UiLayer.View);
        if (_views.Count > 0)
            _views[^1].Hide();
        _views.Add(view);
        return view;
    }

    private Control OpenModal(ResourceKey key)
    {
        Control view = InstantiateView(key);
        var host = new Control
        {
            Name = "ModalHost",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(view);

        try
        {
            _root!.ModalRoot.AddChild(host);
        }
        catch (Exception exception)
        {
            host.QueueFree();
            throw new UiOpenException(key, $"UI 模态无法加入场景树: {key.Value}", exception);
        }

        _modals.Add(new ModalEntry(view, host));
        return view;
    }

    private static void AddToRoot(Control view, Control root, ResourceKey key, UiLayer layer)
    {
        try
        {
            root.AddChild(view);
        }
        catch (Exception exception)
        {
            view.QueueFree();
            throw new UiOpenException(
                key,
                $"UI 无法加入 {layer} 层: {key.Value}",
                exception);
        }
    }

    private static Control InstantiateView(ResourceKey key)
    {
        try
        {
            PackedScene scene = ResourceHub.Load<PackedScene>(key);
            if (!scene.CanInstantiate())
                throw new InvalidOperationException("PackedScene 不包含可实例化的节点。");

            Node node = scene.Instantiate();
            if (node is Control control)
                return control;

            node.QueueFree();
            throw new InvalidOperationException("UI 场景根节点必须继承 Control。");
        }
        catch (Exception exception) when (exception is not UiOpenException)
        {
            throw new UiOpenException(key, $"UI 场景无法打开: {key.Value}", exception);
        }
    }

    private void OnMainSceneChanged(FrameworkMainSceneChangedEvent _)
    {
        for (int i = 0; i < _sceneViews.Count; i++)
        {
            if (IsInstanceValid(_sceneViews[i]))
                _sceneViews[i].QueueFree();
        }
        _sceneViews.Clear();
    }

    private void VerifyReady()
    {
        MainThreadGuard.VerifyAccess();
        if (!IsInsideTree() || !IsInstanceValid(_root) || !_root.IsInitialized)
            throw new InvalidOperationException("UiService 必须完成 UiRoot 初始化后才能使用。");
    }

    private readonly struct ModalEntry
    {
        public Control View { get; }
        public Control Host { get; }

        public ModalEntry(Control view, Control host)
        {
            View = view;
            Host = host;
        }
    }
}
