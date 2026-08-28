using Godot;

namespace GoDo;

/// <summary>一个 Action 在单次 InputService 采样提交后的独立值快照。</summary>
/// <remarks>该值不引用 InputFrame，可以安全跨帧保存。ElapsedRatio 始终位于 [0, 1]。</remarks>
public readonly struct InputActionFrameState
{
    /// <summary>Action 的固定输出类型。</summary>
    public InputActionValueType ValueType { get; }

    /// <summary>Action 当前三分量值；有效分量由 <see cref="ValueType"/> 决定。</summary>
    public Vector3 Value { get; }

    /// <summary>Action 当前稳定阶段。</summary>
    public InputActionStatus Status { get; }

    /// <summary>最近采样窗口累计的离散转换。</summary>
    public InputActionTransitions Transitions { get; }

    /// <summary>Action 当前求值已经持续的秒数；始终为有限非负值。</summary>
    public float ElapsedSeconds { get; }

    /// <summary>Action 当前求值进度；始终为有限的 [0, 1] 值。</summary>
    public float ElapsedRatio { get; }

    /// <summary>产生该快照的 InputFrame 单调序号。</summary>
    public ulong Sequence { get; }

    internal InputActionFrameState(
        InputActionValueType valueType,
        Vector3 value,
        InputActionStatus status,
        InputActionTransitions transitions,
        float elapsedSeconds,
        float elapsedRatio,
        ulong sequence)
    {
        ValueType = valueType;
        Value = value;
        Status = status;
        Transitions = transitions;
        ElapsedSeconds = elapsedSeconds;
        ElapsedRatio = elapsedRatio;
        Sequence = sequence;
    }
}
