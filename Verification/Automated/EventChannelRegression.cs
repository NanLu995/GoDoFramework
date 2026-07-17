using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>EventChannel 的无交互回归验证入口。</summary>
public sealed partial class EventChannelRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        RunAsync();
    }

    private async void RunAsync()
    {
        try
        {
            Run("无数据事件派发", VerifyEmptyEventEmit);
            Run("优先级与同优先级顺序", VerifyPriorityOrder);
            Run("重复监听去重", VerifyDuplicateRegistration);
            Run("Once 与同类型重入", VerifyOnceWithReentrancy);
            Run("派发期间增删监听", VerifyMutationDuringDispatch);
            Run("监听者异常隔离", VerifyExceptionIsolation);
            Run("EventScope 释放", VerifyEventScopeDispose);
            Run("已释放 EventScope 拒绝注册", VerifyDisposedEventScopeRejectsRegistration);
            Run("树外 Node 不注册 Bind 监听", VerifyBindOutsideTree);
            await RunAsync("Bind 跟随 Node 退出树解绑", VerifyNodeBindingAsync);

            GD.Print($"[EventChannelRegression] PASS ({_passed}/10)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[EventChannelRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[EventChannelRegression] PASS: {name}");
    }

    private async Task RunAsync(string name, Func<Task> verification)
    {
        await verification();
        _passed++;
        GD.Print($"[EventChannelRegression] PASS: {name}");
    }

    private static void VerifyPriorityOrder()
    {
        var order = new List<int>();
        void First(TestEvent _) => order.Add(1);
        void Second(TestEvent _) => order.Add(2);
        void Third(TestEvent _) => order.Add(3);

        try
        {
            EventChannel.On<TestEvent>(Second, priority: 0);
            EventChannel.On<TestEvent>(Third, priority: 0);
            EventChannel.On<TestEvent>(First, priority: -10);
            EventChannel.Emit(new TestEvent());

            AssertSequence(order, 1, 2, 3);
        }
        finally
        {
            EventChannel.Off<TestEvent>(First);
            EventChannel.Off<TestEvent>(Second);
            EventChannel.Off<TestEvent>(Third);
        }
    }

    private static void VerifyEmptyEventEmit()
    {
        int calls = 0;
        void Handler(TestEvent _) => calls++;

        try
        {
            EventChannel.On<TestEvent>(Handler);
            EventChannel.Emit<TestEvent>();

            AssertEqual(1, calls, "无数据事件没有派发给监听者");
        }
        finally
        {
            EventChannel.Off<TestEvent>(Handler);
        }
    }

    private static void VerifyDuplicateRegistration()
    {
        int calls = 0;
        void Handler(DuplicateEvent _) => calls++;

        try
        {
            EventChannel.On<DuplicateEvent>(Handler);
            EventChannel.On<DuplicateEvent>(Handler);
            EventChannel.Emit(new DuplicateEvent());

            AssertEqual(1, calls, "重复监听被执行了多次");
        }
        finally
        {
            EventChannel.Off<DuplicateEvent>(Handler);
        }
    }

    private static void VerifyOnceWithReentrancy()
    {
        int calls = 0;
        void Handler(OnceEvent evt)
        {
            calls++;
            if (!evt.IsNested)
                EventChannel.Emit(new OnceEvent(isNested: true));
        }

        try
        {
            EventChannel.Once<OnceEvent>(Handler);
            EventChannel.Emit(new OnceEvent(isNested: false));
            EventChannel.Emit(new OnceEvent(isNested: false));

            AssertEqual(1, calls, "Once 在重入或后续派发中重复执行");
        }
        finally
        {
            EventChannel.Off<OnceEvent>(Handler);
        }
    }

    private static void VerifyMutationDuringDispatch()
    {
        var order = new List<int>();
        bool firstDispatch = true;
        void Added(MutationEvent _) => order.Add(3);
        void Removed(MutationEvent _) => order.Add(2);
        void Mutating(MutationEvent _)
        {
            order.Add(1);
            if (firstDispatch)
            {
                firstDispatch = false;
                EventChannel.Off<MutationEvent>(Removed);
                EventChannel.On<MutationEvent>(Added);
            }
        }

        try
        {
            EventChannel.On<MutationEvent>(Mutating);
            EventChannel.On<MutationEvent>(Removed);
            EventChannel.Emit(new MutationEvent());
            AssertSequence(order, 1);

            order.Clear();
            EventChannel.Emit(new MutationEvent());
            AssertSequence(order, 1, 3);
        }
        finally
        {
            EventChannel.Off<MutationEvent>(Mutating);
            EventChannel.Off<MutationEvent>(Removed);
            EventChannel.Off<MutationEvent>(Added);
        }
    }

    private static void VerifyExceptionIsolation()
    {
        int laterCalls = 0;
        ErrorReport? captured = null;
        void Throwing(ExceptionEvent _) => throw new InvalidOperationException("expected");
        void Later(ExceptionEvent _) => laterCalls++;
        void OnError(ErrorReport report)
        {
            if (report.Module == "EventChannel")
                captured = report;
        }

        ErrorHub.OnError += OnError;
        try
        {
            EventChannel.On<ExceptionEvent>(Throwing);
            EventChannel.On<ExceptionEvent>(Later);
            EventChannel.Emit(new ExceptionEvent());

            AssertEqual(1, laterCalls, "前一个监听者异常阻断了后续监听者");
            Assert(captured?.Exception is InvalidOperationException, "监听者异常未交给 ErrorHub");
        }
        finally
        {
            ErrorHub.OnError -= OnError;
            EventChannel.Off<ExceptionEvent>(Throwing);
            EventChannel.Off<ExceptionEvent>(Later);
        }
    }

    private static void VerifyEventScopeDispose()
    {
        int calls = 0;
        void Handler(ScopeEvent _) => calls++;

        try
        {
            var scope = new EventScope();
            scope.On<ScopeEvent>(Handler);
            EventChannel.Emit(new ScopeEvent());
            scope.Dispose();
            EventChannel.Emit(new ScopeEvent());

            AssertEqual(1, calls, "EventScope.Dispose 后监听仍然存在");
        }
        finally
        {
            EventChannel.Off<ScopeEvent>(Handler);
        }
    }

    private static void VerifyDisposedEventScopeRejectsRegistration()
    {
        var scope = new EventScope();
        scope.Dispose();
        scope.Dispose();

        bool threw = false;
        try
        {
            scope.On<ScopeEvent>(_ => { });
        }
        catch (ObjectDisposedException)
        {
            threw = true;
        }

        Assert(threw, "已释放 EventScope 仍允许注册监听");
    }

    private static void VerifyBindOutsideTree()
    {
        int calls = 0;
        var owner = new Node { Name = "OutsideTreeOwner" };
        void Handler(BoundEvent _) => calls++;

        try
        {
            EventChannel.Bind<BoundEvent>(owner, Handler);
            EventChannel.Emit(new BoundEvent());

            AssertEqual(0, calls, "树外 Node 的 Bind 监听被注册");
        }
        finally
        {
            EventChannel.Off<BoundEvent>(Handler);
            owner.QueueFree();
        }
    }

    private async Task VerifyNodeBindingAsync()
    {
        int calls = 0;
        var owner = new Node { Name = "BoundOwner" };
        void Handler(BoundEvent _) => calls++;

        AddChild(owner);
        try
        {
            EventChannel.Bind<BoundEvent>(owner, Handler);
            EventChannel.Emit(new BoundEvent());
            owner.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            EventChannel.Emit(new BoundEvent());

            AssertEqual(1, calls, "Node 退出树后 Bind 监听仍然存在");
        }
        finally
        {
            EventChannel.Off<BoundEvent>(Handler);
            if (IsInstanceValid(owner))
                owner.QueueFree();
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{message}；期望 {expected}，实际 {actual}");
    }

    private static void AssertSequence(IReadOnlyList<int> actual, params int[] expected)
    {
        AssertEqual(expected.Length, actual.Count, "调用数量不一致");
        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i] != expected[i])
            {
                throw new InvalidOperationException(
                    $"调用顺序不一致；索引 {i} 期望 {expected[i]}，实际 {actual[i]}");
            }
        }
    }

    private readonly struct TestEvent : IEventMessage;
    private readonly struct DuplicateEvent : IEventMessage;
    private readonly struct MutationEvent : IEventMessage;
    private readonly struct ExceptionEvent : IEventMessage;
    private readonly struct ScopeEvent : IEventMessage;
    private readonly struct BoundEvent : IEventMessage;

    private readonly struct OnceEvent : IEventMessage
    {
        public bool IsNested { get; }

        public OnceEvent(bool isNested)
        {
            IsNested = isNested;
        }
    }
}
