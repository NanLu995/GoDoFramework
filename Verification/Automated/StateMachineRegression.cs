using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>纯 C# 泛型状态机的无交互回归验证入口。</summary>
public sealed partial class StateMachineRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("初始状态与继承入口", VerifyInitialStateAndDerivedMachine);
            Run("首次进入", VerifyFirstEnter);
            Run("正常切换顺序", VerifyChangeOrder);
            Run("相同实例重复切换", VerifyRepeatedState);
            Run("生命周期嵌套切换 FIFO", VerifyNestedChangeFifo);
            Run("可选 Tick", VerifyOptionalTick);
            Run("正常 Dispose", VerifyDispose);
            Run("Enter 异常终止", VerifyEnterFailure);
            Run("Exit 异常终止", VerifyExitFailure);
            Run("Dispose Exit 异常", VerifyDisposeFailure);
            Run("Update 异常不改变生命周期", VerifyUpdateFailure);
            Run("无限切换链防御", VerifyTransitionLimit);

            GD.Print($"[StateMachineRegression] PASS ({_passed}/12)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[StateMachineRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[StateMachineRegression] PASS: {name}");
    }

    private static void VerifyInitialStateAndDerivedMachine()
    {
        var context = new TestContext();
        using var machine = new TestStateMachine(context);
        context.Machine = machine;

        Assert(machine.CurrentState is null, "新状态机的 CurrentState 不是 null");
        Assert(!machine.IsFaulted, "新状态机错误地处于故障状态");
        Assert(machine.Failure is null, "新状态机错误地保留了异常");
        Assert(!machine.IsDisposed, "新状态机错误地处于关闭状态");

        machine.Tick(0.25);
        Assert(context.Log.Count == 0, "未初始化 Tick 产生了状态回调");
    }

    private static void VerifyFirstEnter()
    {
        var context = new TestContext();
        using var machine = CreateMachine(context);
        var initial = new TestState("A");

        StateChangeResult result = machine.Change(initial);

        Assert(result == StateChangeResult.Changed, "首次 Change 没有返回 Changed");
        Assert(ReferenceEquals(machine.CurrentState, initial), "首次 Change 后 CurrentState 不正确");
        AssertSequence(context.Log, "Enter:A");
    }

    private static void VerifyChangeOrder()
    {
        var context = new TestContext();
        using var machine = CreateMachine(context);
        var first = new TestState("A");
        var second = new TestState("B");
        first.ExitAction = stateContext =>
            Assert(ReferenceEquals(stateContext.Machine.CurrentState, first),
                "Exit 期间 CurrentState 不再是旧状态");
        second.EnterAction = stateContext =>
            Assert(ReferenceEquals(stateContext.Machine.CurrentState, second),
                "Enter 期间 CurrentState 还不是新状态");

        machine.Change(first);
        machine.Change(second);

        AssertSequence(context.Log, "Enter:A", "Exit:A", "Enter:B");
    }

    private static void VerifyRepeatedState()
    {
        var context = new TestContext();
        using var machine = CreateMachine(context);
        var state = new TestState("A");
        machine.Change(state);

        StateChangeResult result = machine.Change(state);

        Assert(result == StateChangeResult.IgnoredSameState,
            "相同实例没有返回 IgnoredSameState");
        Assert(state.EnterCount == 1, "相同实例被重复 Enter");
        Assert(state.ExitCount == 0, "相同实例触发了 Exit");
        AssertSequence(context.Log, "Enter:A");
    }

    private static void VerifyNestedChangeFifo()
    {
        var context = new TestContext();
        using var machine = CreateMachine(context);
        var first = new TestState("A");
        var second = new TestState("B");
        var third = new TestState("C");
        var fourth = new TestState("D");
        StateChangeResult exitRequest = default;
        StateChangeResult enterRequest = default;

        first.ExitAction = stateContext => exitRequest = stateContext.Machine.Change(third);
        second.EnterAction = stateContext => enterRequest = stateContext.Machine.Change(fourth);

        machine.Change(first);
        machine.Change(second);

        Assert(exitRequest == StateChangeResult.Queued, "Exit 内 Change 没有返回 Queued");
        Assert(enterRequest == StateChangeResult.Queued, "Enter 内 Change 没有返回 Queued");
        Assert(ReferenceEquals(machine.CurrentState, fourth), "FIFO 处理后的最终状态不正确");
        AssertSequence(
            context.Log,
            "Enter:A",
            "Exit:A",
            "Enter:B",
            "Exit:B",
            "Enter:C",
            "Exit:C",
            "Enter:D");
    }

    private static void VerifyOptionalTick()
    {
        var context = new TestContext();
        using var machine = CreateMachine(context);
        var passive = new TestState("Passive");
        var active = new UpdatableTestState("Active");

        machine.Change(passive);
        machine.Tick(0.1);
        machine.Change(active);
        machine.Tick(0.25);

        Assert(active.UpdateCount == 1, "可更新状态没有收到一次 Update");
        Assert(active.LastDeltaSeconds == 0.25, "Update 没有收到原始 deltaSeconds");
        Assert(ReferenceEquals(active.LastContext, context), "Update 没有收到状态机 Context");
    }

    private static void VerifyDispose()
    {
        var context = new TestContext();
        var machine = CreateMachine(context);
        var state = new TestState("A");
        machine.Change(state);

        machine.Dispose();
        machine.Dispose();

        Assert(machine.IsDisposed, "Dispose 后 IsDisposed 不是 true");
        Assert(machine.CurrentState is null, "Dispose 后 CurrentState 没有清空");
        Assert(state.ExitCount == 1, "Dispose 没有确保当前状态只 Exit 一次");
        AssertThrows<ObjectDisposedException>(() => machine.Change(new TestState("B")),
            "Dispose 后 Change 没有失败");
        AssertThrows<ObjectDisposedException>(() => machine.Tick(0.1),
            "Dispose 后 Tick 没有失败");
    }

    private static void VerifyEnterFailure()
    {
        var context = new TestContext();
        var machine = CreateMachine(context);
        var queued = new TestState("Queued");
        var failure = new TestLifecycleException("Enter failed");
        var failing = new TestState("Broken")
        {
            EnterAction = stateContext =>
            {
                Assert(stateContext.Machine.Change(queued) == StateChangeResult.Queued,
                    "失败 Enter 内的后续请求没有进入队列");
                throw failure;
            },
        };

        TestLifecycleException thrown = AssertThrows<TestLifecycleException>(
            () => machine.Change(failing),
            "Enter 异常没有原样传播");

        Assert(ReferenceEquals(thrown, failure), "Enter 没有保留原始异常实例");
        Assert(machine.IsFaulted, "Enter 异常后状态机没有终止");
        Assert(ReferenceEquals(machine.Failure, failure), "Failure 不是 Enter 原始异常");
        Assert(ReferenceEquals(machine.CurrentState, failing), "Enter 异常后没有保留故障目标用于诊断");
        Assert(queued.EnterCount == 0, "Enter 异常后仍处理了待切换队列");
        InvalidOperationException rejected = AssertThrows<InvalidOperationException>(
            () => machine.Change(new TestState("Other")),
            "故障后 Change 没有失败");
        Assert(ReferenceEquals(rejected.InnerException, failure), "故障拒绝没有关联原始异常");
        AssertThrows<InvalidOperationException>(() => machine.Tick(0.1), "故障后 Tick 没有失败");

        machine.Dispose();
        Assert(failing.ExitCount == 0, "Dispose 重试了未成功进入状态的生命周期");
        Assert(machine.CurrentState is null, "故障 Dispose 后 CurrentState 没有清空");
    }

    private static void VerifyExitFailure()
    {
        var context = new TestContext();
        var machine = CreateMachine(context);
        var failure = new TestLifecycleException("Exit failed");
        var current = new TestState("A")
        {
            ExitAction = _ => throw failure,
        };
        var next = new TestState("B");
        machine.Change(current);

        TestLifecycleException thrown = AssertThrows<TestLifecycleException>(
            () => machine.Change(next),
            "Exit 异常没有原样传播");

        Assert(ReferenceEquals(thrown, failure), "Exit 没有保留原始异常实例");
        Assert(machine.IsFaulted, "Exit 异常后状态机没有终止");
        Assert(ReferenceEquals(machine.Failure, failure), "Failure 不是 Exit 原始异常");
        Assert(ReferenceEquals(machine.CurrentState, current), "Exit 异常后 CurrentState 不再是旧状态");
        Assert(next.EnterCount == 0, "Exit 异常后仍进入了目标状态");

        machine.Dispose();
        Assert(current.ExitCount == 1, "故障 Dispose 重试了失败的 Exit");
    }

    private static void VerifyDisposeFailure()
    {
        var context = new TestContext();
        var machine = CreateMachine(context);
        var failure = new TestLifecycleException("Shutdown failed");
        var current = new TestState("A")
        {
            ExitAction = _ => throw failure,
        };
        machine.Change(current);

        TestLifecycleException thrown = AssertThrows<TestLifecycleException>(
            machine.Dispose,
            "Dispose Exit 异常没有原样传播");

        Assert(ReferenceEquals(thrown, failure), "Dispose 没有保留原始异常实例");
        Assert(machine.IsDisposed, "Dispose Exit 异常后没有保持关闭状态");
        Assert(machine.IsFaulted, "Dispose Exit 异常后没有记录故障");
        Assert(machine.CurrentState is null, "Dispose Exit 异常后没有清空 CurrentState");
        machine.Dispose();
        Assert(current.ExitCount == 1, "重复 Dispose 重试了失败的 Exit");
    }

    private static void VerifyUpdateFailure()
    {
        var context = new TestContext();
        using var machine = CreateMachine(context);
        var failure = new TestLifecycleException("Update failed");
        var state = new UpdatableTestState("A")
        {
            UpdateAction = (_, _) => throw failure,
        };
        machine.Change(state);

        TestLifecycleException thrown = AssertThrows<TestLifecycleException>(
            () => machine.Tick(0.1),
            "Update 异常没有原样传播");

        Assert(ReferenceEquals(thrown, failure), "Update 没有保留原始异常实例");
        Assert(!machine.IsFaulted, "Update 异常错误地终止了生命周期状态机");
        Assert(ReferenceEquals(machine.CurrentState, state), "Update 异常改变了 CurrentState");
    }

    private static void VerifyTransitionLimit()
    {
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ = new TestStateMachine(new TestContext(), 0),
            "状态机接受了小于 1 的切换上限");

        var context = new TestContext();
        var machine = new TestStateMachine(context, 3);
        context.Machine = machine;
        var first = new TestState("A");
        var second = new TestState("B");
        first.EnterAction = stateContext => stateContext.Machine.Change(second);
        second.EnterAction = stateContext => stateContext.Machine.Change(first);

        StateMachineTransitionLimitException thrown =
            AssertThrows<StateMachineTransitionLimitException>(
                () => machine.Change(first),
                "无限切换链没有触发切换上限");

        Assert(thrown.MaximumTransitions == 3, "切换上限异常没有保留配置值");
        Assert(machine.MaxTransitionsPerChange == 3, "状态机没有公开实际切换上限");
        Assert(machine.IsFaulted, "超过切换上限后状态机没有终止");
        Assert(ReferenceEquals(machine.Failure, thrown), "Failure 不是切换上限原始异常");
        Assert(ReferenceEquals(machine.CurrentState, first), "上限触发前的最后完整状态没有保留");
        Assert(first.EnterCount == 2, "切换上限没有按实际完成的切换计数");
        Assert(first.ExitCount == 1, "上限触发前额外退出了最后完整状态");
        Assert(second.EnterCount == 1, "切换上限的边界执行数量不正确");
        Assert(second.ExitCount == 1, "切换上限的边界退出数量不正确");

        machine.Dispose();
        Assert(machine.CurrentState is null, "上限故障 Dispose 后 CurrentState 没有清空");
        Assert(first.ExitCount == 2, "上限故障 Dispose 没有退出最后完整进入的状态一次");
    }

    private static TestStateMachine CreateMachine(TestContext context)
    {
        var machine = new TestStateMachine(context);
        context.Machine = machine;
        return machine;
    }

    private static TException AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    private static void AssertSequence(IReadOnlyList<string> actual, params string[] expected)
    {
        Assert(actual.Count == expected.Length,
            $"调用数量不正确，期望 {expected.Length}，实际 {actual.Count}: {string.Join(", ", actual)}");
        for (int index = 0; index < expected.Length; index++)
        {
            Assert(actual[index] == expected[index],
                $"调用顺序在索引 {index} 不正确，期望 {expected[index]}，实际 {actual[index]}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class TestContext
    {
        public List<string> Log { get; } = new();

        public TestStateMachine Machine { get; set; } = null!;
    }

    private sealed class TestStateMachine : StateMachine<TestContext, TestState>
    {
        public TestStateMachine(TestContext context) : base(context)
        {
        }

        public TestStateMachine(TestContext context, int maxTransitionsPerChange)
            : base(context, maxTransitionsPerChange)
        {
        }
    }

    private class TestState : IState<TestContext>
    {
        public TestState(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public int EnterCount { get; private set; }

        public int ExitCount { get; private set; }

        public Action<TestContext>? EnterAction { get; set; }

        public Action<TestContext>? ExitAction { get; set; }

        public void Enter(TestContext context)
        {
            EnterCount++;
            context.Log.Add($"Enter:{Name}");
            EnterAction?.Invoke(context);
        }

        public void Exit(TestContext context)
        {
            ExitCount++;
            context.Log.Add($"Exit:{Name}");
            ExitAction?.Invoke(context);
        }
    }

    private sealed class UpdatableTestState : TestState, IUpdatableState<TestContext>
    {
        public UpdatableTestState(string name) : base(name)
        {
        }

        public int UpdateCount { get; private set; }

        public double LastDeltaSeconds { get; private set; }

        public TestContext? LastContext { get; private set; }

        public Action<TestContext, double>? UpdateAction { get; set; }

        public void Update(TestContext context, double deltaSeconds)
        {
            UpdateCount++;
            LastContext = context;
            LastDeltaSeconds = deltaSeconds;
            UpdateAction?.Invoke(context, deltaSeconds);
        }
    }

    private sealed class TestLifecycleException : Exception
    {
        public TestLifecycleException(string message) : base(message)
        {
        }
    }
}
