using Godot;
using GoDo;

namespace GoDoFramework.Tests;

/// <summary>NodePool 运行时验证使用的最小节点。</summary>
public sealed partial class PoolTestNode : Node, IPoolable
{
    public int AcquireCount { get; private set; }
    public int ReleaseCount { get; private set; }
    public bool ThrowOnRelease { get; set; }

    public void OnAcquire()
    {
        AcquireCount++;
    }

    public void OnRelease()
    {
        ReleaseCount++;

        if (ThrowOnRelease)
            throw new System.InvalidOperationException("[PoolTest] 预期的 OnRelease 异常。");
    }
}
