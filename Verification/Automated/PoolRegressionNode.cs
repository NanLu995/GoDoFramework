using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>NodePool 回归验证使用的最小可池化节点。</summary>
public sealed partial class PoolRegressionNode : Node, IPoolable
{
    public int AcquireCount { get; private set; }
    public int ReleaseCount { get; private set; }
    public bool IsAcquired { get; private set; }
    public bool ThrowOnAcquire { get; set; }
    public bool ThrowOnRelease { get; set; }
    public Action? ReleaseAction { get; set; }

    /// <inheritdoc />
    public void OnAcquire()
    {
        AcquireCount++;
        IsAcquired = true;

        if (ThrowOnAcquire)
            throw new InvalidOperationException("NodePool Acquire 回归异常。");
    }

    /// <inheritdoc />
    public void OnRelease()
    {
        ReleaseCount++;
        IsAcquired = false;

        if (ThrowOnRelease)
            throw new InvalidOperationException("NodePool Release 回归异常。");

        ReleaseAction?.Invoke();
    }
}
