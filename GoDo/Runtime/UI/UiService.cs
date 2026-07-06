using System;
using System.Collections.Generic;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 管理 Control 页面、模态界面与返回顺序。
/// <para>本服务必须位于场景树中，并且所有调用都必须发生在 Godot 主线程。</para>
/// </summary>
public sealed partial class UiService : CanvasLayer, IUiService
{
    private readonly List<Control> _pages = new();
    private readonly List<ModalEntry> _modals = new();
    private Control? _pageRoot;
    private Control? _modalRoot;

    [Export]
    public NodePath PageRootPath { get; set; } = null!;

    [Export]
    public NodePath ModalRootPath { get; set; } = null!;

    public override void _Ready()
    {
        _pageRoot = GetNodeOrNull<Control>(PageRootPath);
        _modalRoot = GetNodeOrNull<Control>(ModalRootPath);
        if (!IsInstanceValid(_pageRoot) || !IsInstanceValid(_modalRoot))
            throw new InvalidOperationException("UiService 缺少 PageRoot 或 ModalRoot 子节点。");
    }

    public override void _ExitTree()
    {
        _pages.Clear();
        _modals.Clear();
        _pageRoot = null;
        _modalRoot = null;
    }

    /// <summary>打开页面；当前页面会隐藏，关闭新页面后恢复。</summary>
    public Control OpenPage(ResourceKey key)
    {
        VerifyReady();
        Control view = InstantiateView(key);

        try
        {
            _pageRoot!.AddChild(view);
        }
        catch (Exception exception)
        {
            view.QueueFree();
            throw new UiOpenException(key, $"UI 页面无法加入场景树: {key.Value}", exception);
        }

        if (_pages.Count > 0)
            _pages[^1].Hide();
        _pages.Add(view);
        return view;
    }

    /// <summary>在页面之上打开模态界面，并阻止 GUI 指针输入穿透。</summary>
    public Control OpenModal(ResourceKey key)
    {
        VerifyReady();
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
            _modalRoot!.AddChild(host);
        }
        catch (Exception exception)
        {
            host.QueueFree();
            throw new UiOpenException(key, $"UI 模态无法加入场景树: {key.Value}", exception);
        }

        _modals.Add(new ModalEntry(view, host));
        return view;
    }

    /// <summary>关闭当前最上层且由本服务管理的界面。</summary>
    public void Close(Control view)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(view);
        VerifyReady();

        if (_modals.Count > 0)
        {
            ModalEntry top = _modals[^1];
            if (top.View != view)
                throw new InvalidOperationException("只能关闭当前最上层的 UI。请先关闭顶部模态。");

            _modals.RemoveAt(_modals.Count - 1);
            top.Host.QueueFree();
            return;
        }

        if (_pages.Count == 0 || _pages[^1] != view)
            throw new InvalidOperationException("目标 UI 不受服务管理，或不是当前最上层页面。");

        _pages.RemoveAt(_pages.Count - 1);
        view.QueueFree();
        if (_pages.Count > 0)
            _pages[^1].Show();
    }

    /// <summary>优先关闭顶部模态，其次关闭顶部页面；没有界面时返回 false。</summary>
    public bool TryGoBack()
    {
        MainThreadGuard.VerifyAccess();
        VerifyReady();

        if (_modals.Count > 0)
        {
            Close(_modals[^1].View);
            return true;
        }

        if (_pages.Count > 0)
        {
            Close(_pages[^1]);
            return true;
        }

        return false;
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

    private void VerifyReady()
    {
        MainThreadGuard.VerifyAccess();
        if (!IsInsideTree() || !IsInstanceValid(_pageRoot) || !IsInstanceValid(_modalRoot))
            throw new InvalidOperationException("UiService 必须完成场景树初始化后才能使用。");
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
