using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>NodePool 的无交互回归验证入口。</summary>
public sealed partial class NodePoolRegression : Node
{
    private int _passed;

    /// <summary>用于实例化池节点的验证场景。</summary>
    [Export]
    public PackedScene TestNodeScene { get; set; } = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            if (TestNodeScene is null)
                throw new InvalidOperationException("NodePoolRegression 未配置 TestNodeScene。");

            Run("预热数量", VerifyPrewarm);
            Run("Acquire Release 与复用", VerifyAcquireReleaseReuse);
            Run("空闲容量上限", VerifyIdleCapacity);
            Run("拒绝重复与外部节点", VerifyInvalidRelease);
            Run("Clear 不影响活动节点", VerifyClear);
            Run("Dispose 强制清理活动节点", VerifyDispose);

            GD.Print($"[NodePoolRegression] PASS ({_passed}/6)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[NodePoolRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[NodePoolRegression] PASS: {name}");
    }

    private void VerifyPrewarm()
    {
        using var pool = new NodePool<PoolRegressionNode>(
            TestNodeScene,
            initialSize: 2,
            idleCapacity: 3);

        AssertEqual(2, pool.IdleCount, "预热空闲数量错误");
        AssertEqual(0, pool.ActiveCount, "预热后存在活动节点");
    }

    private void VerifyAcquireReleaseReuse()
    {
        using var pool = new NodePool<PoolRegressionNode>(TestNodeScene, idleCapacity: 1);
        PoolRegressionNode first = pool.Acquire(this);

        AssertEqual(this, first.GetParent(), "Acquire 后父节点错误");
        Assert(first.IsAcquired, "OnAcquire 没有设置激活状态");
        AssertEqual(1, first.AcquireCount, "OnAcquire 调用次数错误");
        AssertEqual(1, pool.ActiveCount, "Acquire 后活动数量错误");

        Assert(pool.Release(first), "首次 Release 返回 false");
        Assert(first.GetParent() is null, "Release 后节点仍在场景树中");
        Assert(!first.IsAcquired, "OnRelease 没有清理激活状态");
        AssertEqual(1, first.ReleaseCount, "OnRelease 调用次数错误");

        PoolRegressionNode reused = pool.Acquire(this);
        Assert(ReferenceEquals(first, reused), "空闲节点没有被复用");
        AssertEqual(2, reused.AcquireCount, "复用时 OnAcquire 没有再次执行");
        pool.Release(reused);
    }

    private void VerifyIdleCapacity()
    {
        using var pool = new NodePool<PoolRegressionNode>(TestNodeScene, idleCapacity: 1);
        PoolRegressionNode first = pool.Acquire(this);
        PoolRegressionNode second = pool.Acquire(this);

        pool.Release(first);
        pool.Release(second);

        AssertEqual(1, pool.IdleCount, "空闲数量超过容量上限");
        AssertEqual(0, pool.ActiveCount, "归还后仍有活动节点");
    }

    private void VerifyInvalidRelease()
    {
        using var pool = new NodePool<PoolRegressionNode>(TestNodeScene, idleCapacity: 1);
        PoolRegressionNode node = pool.Acquire(this);
        Assert(pool.Release(node), "首次 Release 返回 false");
        Assert(!pool.Release(node), "重复 Release 返回 true");

        var foreign = new PoolRegressionNode();
        AddChild(foreign);
        try
        {
            Assert(!pool.Release(foreign), "外部节点 Release 返回 true");
        }
        finally
        {
            RemoveChild(foreign);
            foreign.QueueFree();
        }
    }

    private void VerifyClear()
    {
        using var pool = new NodePool<PoolRegressionNode>(
            TestNodeScene,
            initialSize: 2,
            idleCapacity: 2);
        PoolRegressionNode active = pool.Acquire(this);

        pool.Clear();
        AssertEqual(0, pool.IdleCount, "Clear 后仍有空闲节点");
        AssertEqual(1, pool.ActiveCount, "Clear 影响了活动节点");
        pool.Release(active);
    }

    private void VerifyDispose()
    {
        var pool = new NodePool<PoolRegressionNode>(TestNodeScene, idleCapacity: 1);
        PoolRegressionNode active = pool.Acquire(this);

        pool.Dispose();
        pool.Dispose();

        AssertEqual(1, active.ReleaseCount, "Dispose 没有对活动节点执行 OnRelease");
        Assert(active.GetParent() is null, "Dispose 后活动节点仍有父节点");
        AssertThrows<ObjectDisposedException>(
            () => pool.Acquire(this),
            "Dispose 后 Acquire 没有抛出 ObjectDisposedException");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}；期望 {expected}，实际 {actual}");
        }
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
