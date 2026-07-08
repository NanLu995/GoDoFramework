using Godot;
using GoDo;

namespace GoDoFramework.Verification;

/// <summary>NodePool 回归验证使用的最小可池化节点。</summary>
public sealed partial class PoolRegressionNode : Node, IPoolable
{
    public int AcquireCount { get; private set; }
    public int ReleaseCount { get; private set; }
    public bool IsAcquired { get; private set; }

    /// <inheritdoc />
    public void OnAcquire()
    {
        AcquireCount++;
        IsAcquired = true;
    }

    /// <inheritdoc />
    public void OnRelease()
    {
        ReleaseCount++;
        IsAcquired = false;
    }
}
