using System;
using System.Collections.Generic;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>
/// 基于 <see cref="PackedScene"/> 的 Godot 节点对象池。
/// <para>
/// 空闲节点保持在场景树外；<see cref="Acquire"/> 时加入调用方指定的父节点，
/// <see cref="Release"/> 时从场景树移除。所有方法只能在创建本池的线程调用。
/// 活动节点不得由外部直接 QueueFree，正常回收必须调用 <see cref="Release"/>；
/// Pool 关闭时会强制释放仍然活动的节点。
/// </para>
/// </summary>
/// <typeparam name="T">PackedScene 根节点的 C# 类型。</typeparam>
public sealed class NodePool<T> : IDisposable where T : Node
{
    private readonly PackedScene _scene;
    private readonly Stack<T> _idleNodes;
    private readonly HashSet<T> _activeNodes = new(ReferenceEqualityComparer.Instance);
    private readonly int _idleCapacity;
    private bool _disposed;

    /// <summary>当前位于空闲区、可直接复用的节点数量。</summary>
    public int IdleCount => _idleNodes.Count;

    /// <summary>当前已经激活、尚未释放回空闲区的节点数量。</summary>
    public int ActiveCount => _activeNodes.Count;

    /// <summary>
    /// 创建一个节点池并按需预热。
    /// </summary>
    /// <param name="scene">根节点必须兼容 <typeparamref name="T"/> 的场景资源。</param>
    /// <param name="initialSize">创建时提前实例化的空闲节点数量。</param>
    /// <param name="idleCapacity">空闲区最多容纳的节点数量；超出后释放，不限制活动节点数量。</param>
    public NodePool(PackedScene scene, int initialSize = 0, int idleCapacity = 32)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));

        if (initialSize < 0)
            throw new ArgumentOutOfRangeException(nameof(initialSize), "初始数量不能小于 0。");
        if (idleCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(idleCapacity), "空闲区容量不能小于 0。");
        if (initialSize > idleCapacity)
            throw new ArgumentException("初始数量不能大于空闲区容量。", nameof(initialSize));

        _idleCapacity = idleCapacity;
        _idleNodes = new Stack<T>(Math.Max(initialSize, 4));

        try
        {
            for (int i = 0; i < initialSize; i++)
                _idleNodes.Push(CreateInstance());
        }
        catch
        {
            FreeIdleNodes();
            throw;
        }
    }

    /// <summary>
    /// 激活一个节点并添加到指定父节点。
    /// </summary>
    /// <param name="parent">新节点进入场景树时使用的父节点。</param>
    /// <returns>已加入 <paramref name="parent"/> 的可用节点。</returns>
    public T Acquire(Node parent)
    {
        VerifyThreadAccess();
        ThrowIfDisposed();

        if (parent == null)
            throw new ArgumentNullException(nameof(parent));
        if (!GodotObject.IsInstanceValid(parent))
            throw new ArgumentException("目标父节点已经被释放。", nameof(parent));

        T node = TakeIdleOrCreate();
        try
        {
            parent.AddChild(node);

            if (node.GetParent() != parent)
            {
                throw new InvalidOperationException(
                    $"无法将池化节点 {typeof(T).Name} 添加到目标父节点 {parent.Name}。");
            }
        }
        catch
        {
            DetachFromParent(node);
            CacheOrFree(node);
            throw;
        }

        _activeNodes.Add(node);

        try
        {
            if (node is IPoolable poolable)
                poolable.OnAcquire();
        }
        catch (Exception exception)
        {
            _activeNodes.Remove(node);
            DetachFromParent(node);
            FreeNode(node);

            throw new InvalidOperationException(
                $"池化节点 {typeof(T).Name} 的 OnAcquire 执行失败。",
                exception);
        }

        if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion())
        {
            _activeNodes.Remove(node);
            if (GodotObject.IsInstanceValid(node))
                DetachFromParent(node);

            throw new InvalidOperationException(
                $"池化节点 {typeof(T).Name} 在 OnAcquire 后已失效或进入删除队列。");
        }

        return node;
    }

    /// <summary>
    /// 释放一个由本池激活的节点，使其进入空闲区或销毁。
    /// </summary>
    /// <returns>成功释放返回 true；节点不属于本池或已经释放时返回 false。</returns>
    public bool Release(T node)
    {
        VerifyThreadAccess();
        ThrowIfDisposed();

        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (!_activeNodes.Remove(node))
        {
            ErrorHub.Warn(
                "尝试释放不属于本池或已经释放的节点",
                "Pool",
                context: $"Release<{typeof(T).Name}>");
            return false;
        }

        if (!GodotObject.IsInstanceValid(node))
        {
            ErrorHub.Warn(
                "活动节点已被外部释放，无法进入空闲区",
                "Pool",
                context: $"Release<{typeof(T).Name}>");
            return false;
        }

        if (node.IsQueuedForDeletion())
        {
            ErrorHub.Warn(
                "活动节点已被外部 QueueFree，无法进入空闲区",
                "Pool",
                context: $"Release<{typeof(T).Name}>");
            return false;
        }

        Exception? callbackException = null;
        try
        {
            if (node is IPoolable poolable)
                poolable.OnRelease();
        }
        catch (Exception exception)
        {
            callbackException = exception;
        }

        DetachFromParent(node);

        if (callbackException != null)
        {
            FreeNode(node);
            throw new InvalidOperationException(
                $"池化节点 {typeof(T).Name} 的 OnRelease 执行失败。",
                callbackException);
        }

        CacheOrFree(node);

        return true;
    }

    /// <summary>释放空闲区的全部节点，不影响当前活动节点。</summary>
    public void Clear()
    {
        VerifyThreadAccess();
        ThrowIfDisposed();
        FreeIdleNodes();
    }

    /// <summary>
    /// 释放空闲区并关闭本池。仍处于活动状态的节点会执行一次尽力清理，
    /// 随后从场景树移除并释放。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        VerifyThreadAccess();
        Clear();

        if (_activeNodes.Count > 0)
        {
            ErrorHub.Warn(
                "对象池关闭时仍有活动节点，将执行强制释放",
                "Pool",
                context: $"Dispose<{typeof(T).Name}> active={_activeNodes.Count}");

            T[] activeNodes = new T[_activeNodes.Count];
            _activeNodes.CopyTo(activeNodes);
            _activeNodes.Clear();

            for (int i = 0; i < activeNodes.Length; i++)
                ForceFreeActiveNode(activeNodes[i]);
        }

        _disposed = true;
    }

    private T TakeIdleOrCreate()
    {
        while (_idleNodes.Count > 0)
        {
            T node = _idleNodes.Pop();
            if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
                return node;
        }

        return CreateInstance();
    }

    private T CreateInstance()
    {
        return _scene.Instantiate<T>();
    }

    private void CacheOrFree(T node)
    {
        if (!GodotObject.IsInstanceValid(node) || node.IsQueuedForDeletion())
            return;

        if (_idleNodes.Count < _idleCapacity)
            _idleNodes.Push(node);
        else
            FreeNode(node);
    }

    private void FreeIdleNodes()
    {
        while (_idleNodes.Count > 0)
            FreeNode(_idleNodes.Pop());
    }

    private static void FreeNode(T node)
    {
        if (GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            node.QueueFree();
    }

    private static void ForceFreeActiveNode(T node)
    {
        if (!GodotObject.IsInstanceValid(node))
            return;

        if (!node.IsQueuedForDeletion() && node is IPoolable poolable)
        {
            try
            {
                poolable.OnRelease();
            }
            catch (Exception exception)
            {
                ErrorHub.Report(
                    exception,
                    "Pool",
                    context: $"Dispose.OnRelease<{typeof(T).Name}>");
            }
        }

        DetachFromParent(node);
        FreeNode(node);
    }

    private static void DetachFromParent(T node)
    {
        Node? parent = node.GetParent();
        if (parent != null && GodotObject.IsInstanceValid(parent))
            parent.RemoveChild(node);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void VerifyThreadAccess()
    {
        RuntimeThreadGuard.VerifyAccess();
    }
}
