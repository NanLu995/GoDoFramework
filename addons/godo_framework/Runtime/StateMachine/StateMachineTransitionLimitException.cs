using System;

#nullable enable

namespace GoDo;

/// <summary>表示一次同步状态切换链超过允许的实际切换数量。</summary>
public sealed class StateMachineTransitionLimitException : InvalidOperationException
{
    internal StateMachineTransitionLimitException(int maximumTransitions)
        : base($"单次 Change 最多允许 {maximumTransitions} 次状态切换；检测到可能无法终止的生命周期切换链。")
    {
        MaximumTransitions = maximumTransitions;
    }

    /// <summary>获取本次状态机配置的单次 Change 最大实际切换数量。</summary>
    public int MaximumTransitions { get; }
}
