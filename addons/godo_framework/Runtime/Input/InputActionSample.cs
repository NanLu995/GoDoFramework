using Godot;

namespace GoDo;

/// <summary>后端写入预分配缓冲区的单个 Action 当前值。</summary>
public readonly struct InputActionSample
{
    /// <summary>Action 的当前原始值。</summary>
    public Vector3 Value { get; }

    /// <summary>Action 当前是否处于按下或触发状态。</summary>
    public bool Pressed { get; }

    /// <summary>Action 当前稳定阶段。</summary>
    public InputActionStatus Status { get; }

    /// <summary>两次采样之间累计的离散转换。</summary>
    public InputActionTransitions Transitions { get; }

    /// <summary>Action 当前求值已经持续的秒数。</summary>
    public float ElapsedSeconds { get; }

    /// <summary>Action 当前求值进度；后端必须提供 [0, 1] 范围内的有限值。</summary>
    public float ElapsedRatio { get; }

    /// <summary>创建 Action 样本。</summary>
    /// <param name="value">Action 的原始三分量值；具体使用的分量由固定 Action 类型决定。</param>
    /// <param name="pressed">Action 当前是否处于按下或触发状态。</param>
    public InputActionSample(Vector3 value, bool pressed)
        : this(
            value,
            pressed,
            pressed ? InputActionStatus.Performed : InputActionStatus.Idle,
            InputActionTransitions.None,
            0f,
            pressed ? 1f : 0f)
    {
    }

    /// <summary>创建包含完整 Action 阶段信息的样本。</summary>
    /// <param name="value">Action 的当前三分量值。</param>
    /// <param name="pressed">供旧 Frame API 使用的当前按下或触发状态。</param>
    /// <param name="status">Action 当前稳定阶段。</param>
    /// <param name="transitions">本采样窗口累计的离散转换。</param>
    /// <param name="elapsedSeconds">当前求值持续秒数。</param>
    /// <param name="elapsedRatio">当前求值进度；必须位于 [0, 1]。</param>
    public InputActionSample(
        Vector3 value,
        bool pressed,
        InputActionStatus status,
        InputActionTransitions transitions,
        float elapsedSeconds,
        float elapsedRatio)
    {
        Value = value;
        Pressed = pressed;
        Status = status;
        Transitions = transitions;
        ElapsedSeconds = elapsedSeconds;
        ElapsedRatio = elapsedRatio;
    }
}
