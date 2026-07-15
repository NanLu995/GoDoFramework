using Godot;

namespace GoDo;

/// <summary>后端写入预分配缓冲区的单个 Action 当前值。</summary>
public readonly struct InputActionSample
{
    /// <summary>Action 的当前原始值。</summary>
    public Vector3 Value { get; }

    /// <summary>Action 当前是否处于按下或触发状态。</summary>
    public bool Pressed { get; }

    /// <summary>创建 Action 样本。</summary>
    public InputActionSample(Vector3 value, bool pressed)
    {
        Value = value;
        Pressed = pressed;
    }
}
