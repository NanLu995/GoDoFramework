using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>当前渲染帧输入状态的零分配只读句柄。</summary>
/// <remarks>句柄只能在取得它的当前渲染帧中从 Godot 主线程读取；它不复制 Action 状态。</remarks>
public readonly struct InputFrame
{
    private readonly InputService? _owner;

    /// <summary>当前快照的单调递增序号。</summary>
    public ulong Sequence { get; }

    internal InputFrame(InputService owner, ulong sequence)
    {
        _owner = owner;
        Sequence = sequence;
    }

    /// <summary>读取 Action 当前是否处于按下或触发状态。</summary>
    /// <param name="action">要读取的已注册 Action ID。</param>
    /// <returns>当前样本处于按下或触发状态时为 <see langword="true"/>。</returns>
    /// <exception cref="InvalidOperationException">当前值是未关联 InputService 的默认 Frame。</exception>
    /// <exception cref="ArgumentException"><paramref name="action"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">Frame 已过期或 Action 未注册。</exception>
    public bool Pressed(InputActionId action) => Resolve(action).Pressed;

    /// <summary>读取 Action 是否在本帧由未按下变为按下。</summary>
    /// <param name="action">要读取的已注册 Action ID。</param>
    /// <returns>Action 仅在本帧产生按下边沿时为 <see langword="true"/>。</returns>
    /// <exception cref="InvalidOperationException">当前值是默认 Frame。</exception>
    /// <exception cref="ArgumentException"><paramref name="action"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">Frame 已过期或 Action 未注册。</exception>
    public bool JustPressed(InputActionId action) => Resolve(action).JustPressed;

    /// <summary>读取 Action 是否在本帧由按下变为未按下。</summary>
    /// <param name="action">要读取的已注册 Action ID。</param>
    /// <returns>Action 仅在本帧产生释放边沿时为 <see langword="true"/>。</returns>
    /// <exception cref="InvalidOperationException">当前值是默认 Frame。</exception>
    /// <exception cref="ArgumentException"><paramref name="action"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">Frame 已过期或 Action 未注册。</exception>
    public bool JustReleased(InputActionId action) => Resolve(action).JustReleased;

    /// <summary>读取 Axis1D Action 的当前值。</summary>
    /// <param name="action">后端声明为 Axis1D 的已注册 Action ID。</param>
    /// <returns>当前单轴值。</returns>
    /// <exception cref="InvalidOperationException">当前值是默认 Frame。</exception>
    /// <exception cref="ArgumentException"><paramref name="action"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">Frame 已过期、Action 未注册或类型不是 Axis1D。</exception>
    public float Axis1(InputActionId action) =>
        Resolve(action, InputActionValueType.Axis1D).Value.X;

    /// <summary>读取 Axis2D Action 的当前值。</summary>
    /// <param name="action">后端声明为 Axis2D 的已注册 Action ID。</param>
    /// <returns>当前二维轴值。</returns>
    /// <exception cref="InvalidOperationException">当前值是默认 Frame。</exception>
    /// <exception cref="ArgumentException"><paramref name="action"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">Frame 已过期、Action 未注册或类型不是 Axis2D。</exception>
    public Vector2 Axis2(InputActionId action)
    {
        Vector3 value = Resolve(action, InputActionValueType.Axis2D).Value;
        return new Vector2(value.X, value.Y);
    }

    /// <summary>读取 Axis3D Action 的当前值。</summary>
    /// <param name="action">后端声明为 Axis3D 的已注册 Action ID。</param>
    /// <returns>当前三维轴值。</returns>
    /// <exception cref="InvalidOperationException">当前值是默认 Frame。</exception>
    /// <exception cref="ArgumentException"><paramref name="action"/> 是默认 ID。</exception>
    /// <exception cref="InputOperationException">Frame 已过期、Action 未注册或类型不是 Axis3D。</exception>
    public Vector3 Axis3(InputActionId action) =>
        Resolve(action, InputActionValueType.Axis3D).Value;

    private ref readonly InputActionState Resolve(InputActionId action)
    {
        if (_owner == null)
            throw new InvalidOperationException("默认 InputFrame 没有关联输入服务。");

        return ref _owner.Resolve(action, Sequence);
    }

    private ref readonly InputActionState Resolve(InputActionId action, InputActionValueType expectedType)
    {
        ref readonly InputActionState state = ref Resolve(action);
        if (state.ValueType != expectedType)
        {
            throw new InputOperationException(
                $"输入 Action 类型不匹配: {action.Value}; 期望 {expectedType}，实际 {state.ValueType}");
        }

        return ref state;
    }
}
