using System;

#nullable enable

namespace GoDo;

/// <summary>进程内稳定标识一个调度任务的不透明句柄。</summary>
public readonly struct ScheduleHandle : IEquatable<ScheduleHandle>
{
    private readonly ulong _value;

    /// <summary>当前句柄是否由 Scheduler 创建且不是默认值。</summary>
    public bool IsValid => _value != 0;

    internal ulong Value => _value;

    internal ScheduleHandle(ulong value)
    {
        _value = value;
    }

    /// <inheritdoc />
    public bool Equals(ScheduleHandle other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ScheduleHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => IsValid ? _value.ToString() : "Invalid";

    /// <summary>比较两个调度句柄是否相等。</summary>
    public static bool operator ==(ScheduleHandle left, ScheduleHandle right) => left.Equals(right);

    /// <summary>比较两个调度句柄是否不相等。</summary>
    public static bool operator !=(ScheduleHandle left, ScheduleHandle right) => !left.Equals(right);
}
