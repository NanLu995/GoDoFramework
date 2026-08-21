using System;
using System.Diagnostics;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>纯 C# StateMachine 在正常与配置上限规模下的 Tick、Change 和嵌套切换基准。</summary>
public sealed partial class StateMachineBenchmark : Node
{
    private const int NormalMachineCount = 1_000;
    private const int MaximumMachineCount = 10_000;
    private const int NormalTickIterations = 10_000;
    private const int MaximumTickIterations = 1_000;
    private const int NormalChangeIterations = 1_000;
    private const int MaximumChangeIterations = 100;
    private const int NestedChangeIterations = 100_000;
    private const int WarmUpIterations = 100;

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            WarmUpMeasurementApis();
            BenchmarkConstruction();
            BenchmarkTickScale(NormalMachineCount, NormalTickIterations);
            BenchmarkTickScale(MaximumMachineCount, MaximumTickIterations);
            BenchmarkChangeScale(NormalMachineCount, NormalChangeIterations);
            BenchmarkChangeScale(MaximumMachineCount, MaximumChangeIterations);
            BenchmarkNestedChange();
            GD.Print(
                $"[StateMachineBenchmark] PASS; Build={BuildConfiguration}; " +
                $"Processors={System.Environment.ProcessorCount}; OS={OS.GetName()}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[StateMachineBenchmark] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void WarmUpMeasurementApis()
    {
        for (int index = 0; index < 10; index++)
        {
            _ = GC.GetAllocatedBytesForCurrentThread();
            _ = Stopwatch.GetTimestamp();
        }

        var context = new BenchmarkContext();
        using var machine = new BenchmarkStateMachine(context);
        var first = new BenchmarkState();
        var second = new BenchmarkState();
        machine.Change(first);
        for (int index = 0; index < 1_000; index++)
            machine.Change((index & 1) == 0 ? second : first);
    }

    private static void BenchmarkConstruction()
    {
        var context = new BenchmarkContext();
        var machines = new BenchmarkStateMachine[MaximumMachineCount];

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < machines.Length; index++)
            machines[index] = new BenchmarkStateMachine(context);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert(allocated > 0, "批量构造没有记录到预期的状态机对象分配");
        GD.Print(
            $"[StateMachineBenchmark] Construct: Machines={MaximumMachineCount}; " +
            $"AllocatedBytes={allocated}; BytesPerMachine={(double)allocated / MaximumMachineCount:F2}");

        for (int index = 0; index < machines.Length; index++)
            machines[index].Dispose();
    }

    private static void BenchmarkTickScale(int machineCount, int iterations)
    {
        BenchmarkStateMachine[] machines = new BenchmarkStateMachine[machineCount];
        BenchmarkContext[] contexts = new BenchmarkContext[machineCount];
        for (int index = 0; index < machineCount; index++)
        {
            var context = new BenchmarkContext();
            var machine = new BenchmarkStateMachine(context);
            machine.Change(new UpdatableBenchmarkState());
            contexts[index] = context;
            machines[index] = machine;
        }

        for (int iteration = 0; iteration < WarmUpIterations; iteration++)
            TickAll(machines);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < iterations; iteration++)
            TickAll(machines);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        long started = Stopwatch.GetTimestamp();
        for (int iteration = 0; iteration < iterations; iteration++)
            TickAll(machines);
        long finished = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started, finished);
        long operations = (long)machineCount * iterations;

        Assert(allocated == 0,
            $"{machineCount} 台状态机稳态 Tick 产生托管分配: {allocated} bytes");
        Assert(contexts[0].UpdateCount > 0 && contexts[^1].UpdateCount > 0,
            "规模 Tick 没有更新首尾状态机");
        GD.Print(
            $"[StateMachineBenchmark] Tick: Machines={machineCount}; Iterations={iterations}; " +
            $"Operations={operations}; ElapsedMs={elapsed.TotalMilliseconds:F3}; " +
            $"AverageNs={elapsed.TotalMilliseconds * 1_000_000d / operations:F3}; " +
            $"AllocatedBytes={allocated}");

        DisposeAll(machines);
    }

    private static void BenchmarkChangeScale(int machineCount, int iterations)
    {
        BenchmarkStateMachine[] machines = new BenchmarkStateMachine[machineCount];
        BenchmarkState[] firstStates = new BenchmarkState[machineCount];
        BenchmarkState[] secondStates = new BenchmarkState[machineCount];
        for (int index = 0; index < machineCount; index++)
        {
            var context = new BenchmarkContext();
            var machine = new BenchmarkStateMachine(context);
            var first = new BenchmarkState();
            var second = new BenchmarkState();
            machine.Change(first);
            machines[index] = machine;
            firstStates[index] = first;
            secondStates[index] = second;
        }

        for (int iteration = 0; iteration < WarmUpIterations; iteration++)
            ChangeAll(machines, firstStates, secondStates, iteration);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < iterations; iteration++)
            ChangeAll(machines, firstStates, secondStates, iteration);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        long started = Stopwatch.GetTimestamp();
        for (int iteration = 0; iteration < iterations; iteration++)
            ChangeAll(machines, firstStates, secondStates, iteration);
        long finished = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started, finished);
        long operations = (long)machineCount * iterations;

        Assert(allocated == 0,
            $"{machineCount} 台状态机稳态 Change 产生托管分配: {allocated} bytes");
        GD.Print(
            $"[StateMachineBenchmark] Change: Machines={machineCount}; Iterations={iterations}; " +
            $"Operations={operations}; ElapsedMs={elapsed.TotalMilliseconds:F3}; " +
            $"AverageNs={elapsed.TotalMilliseconds * 1_000_000d / operations:F3}; " +
            $"AllocatedBytes={allocated}");

        DisposeAll(machines);
    }

    private static void BenchmarkNestedChange()
    {
        var context = new BenchmarkContext();
        using var machine = new BenchmarkStateMachine(context);
        var idle = new BenchmarkState();
        var final = new BenchmarkState();
        var nested = new NestedBenchmarkState(machine, final);
        machine.Change(idle);

        long firstAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        machine.Change(nested);
        long firstAllocated = GC.GetAllocatedBytesForCurrentThread() - firstAllocatedBefore;
        Assert(firstAllocated > 0, "首次嵌套 Change 没有记录到延迟创建 FIFO 队列的分配");

        for (int iteration = 0; iteration < WarmUpIterations; iteration++)
        {
            machine.Change(idle);
            machine.Change(nested);
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < NestedChangeIterations; iteration++)
        {
            machine.Change(idle);
            machine.Change(nested);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        long started = Stopwatch.GetTimestamp();
        for (int iteration = 0; iteration < NestedChangeIterations; iteration++)
        {
            machine.Change(idle);
            machine.Change(nested);
        }
        long finished = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started, finished);
        long transitions = NestedChangeIterations * 3L;

        Assert(allocated == 0,
            $"复用 FIFO 队列后的稳态嵌套 Change 产生托管分配: {allocated} bytes");
        Assert(ReferenceEquals(machine.CurrentState, final), "嵌套 Change 基准的最终状态不正确");
        GD.Print(
            $"[StateMachineBenchmark] NestedChange: Iterations={NestedChangeIterations}; " +
            $"Transitions={transitions}; FirstAllocatedBytes={firstAllocated}; " +
            $"ElapsedMs={elapsed.TotalMilliseconds:F3}; " +
            $"AverageNsPerTransition={elapsed.TotalMilliseconds * 1_000_000d / transitions:F3}; " +
            $"SteadyAllocatedBytes={allocated}");
    }

    private static void TickAll(BenchmarkStateMachine[] machines)
    {
        for (int index = 0; index < machines.Length; index++)
            machines[index].Tick(1d / 60d);
    }

    private static void ChangeAll(
        BenchmarkStateMachine[] machines,
        BenchmarkState[] firstStates,
        BenchmarkState[] secondStates,
        int iteration)
    {
        BenchmarkState[] targets = (iteration & 1) == 0 ? secondStates : firstStates;
        for (int index = 0; index < machines.Length; index++)
            machines[index].Change(targets[index]);
    }

    private static void DisposeAll(BenchmarkStateMachine[] machines)
    {
        for (int index = 0; index < machines.Length; index++)
            machines[index].Dispose();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class BenchmarkContext
    {
        public long LifecycleCount;

        public long UpdateCount;
    }

    private sealed class BenchmarkStateMachine : StateMachine<BenchmarkContext, BenchmarkState>
    {
        public BenchmarkStateMachine(BenchmarkContext context) : base(context)
        {
        }
    }

    private class BenchmarkState : IState<BenchmarkContext>
    {
        public virtual void Enter(BenchmarkContext context)
        {
            context.LifecycleCount++;
        }

        public virtual void Exit(BenchmarkContext context)
        {
            context.LifecycleCount++;
        }
    }

    private sealed class UpdatableBenchmarkState : BenchmarkState,
        IUpdatableState<BenchmarkContext>
    {
        public void Update(BenchmarkContext context, double deltaSeconds)
        {
            context.UpdateCount++;
            _ = deltaSeconds;
        }
    }

    private sealed class NestedBenchmarkState : BenchmarkState
    {
        private readonly BenchmarkStateMachine _machine;
        private readonly BenchmarkState _target;

        public NestedBenchmarkState(BenchmarkStateMachine machine, BenchmarkState target)
        {
            _machine = machine;
            _target = target;
        }

        public override void Enter(BenchmarkContext context)
        {
            base.Enter(context);
            StateChangeResult result = _machine.Change(_target);
            if (result != StateChangeResult.Queued)
                throw new InvalidOperationException($"嵌套 Change 返回了意外结果: {result}");
        }
    }
}
