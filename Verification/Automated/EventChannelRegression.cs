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
            await RunAsync("重复 Bind 保持生命周期解绑", VerifyDuplicateBindLifecycleAsync);
            Run("嵌套派发延迟提交新增监听", VerifyNestedMutationCommit);
            await RunAsync("Bind 跟随 Node 退出树解绑", VerifyNodeBindingAsync);
#if DEBUG
            await RunAsync("Debug 监听来源与解绑", VerifyDebugListenerSourcesAsync);
            Run("Debug 监听来源上限", VerifyDebugListenerLimit);
#endif

            GD.Print($"[EventChannelRegression] PASS ({_passed}/{_passed})");
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

    private async Task VerifyDuplicateBindLifecycleAsync()
    {
        int calls = 0;
        var owner = new Node { Name = "DuplicateBoundOwner" };
        void Handler(BoundEvent _) => calls++;

        AddChild(owner);
        try
        {
            EventChannel.Bind<BoundEvent>(owner, Handler);
            EventChannel.Bind<BoundEvent>(owner, Handler);
            EventChannel.Emit(new BoundEvent());
            owner.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            EventChannel.Emit(new BoundEvent());

            AssertEqual(1, calls, "重复 Bind 破坏了首次监听的生命周期解绑");
        }
        finally
        {
            EventChannel.Off<BoundEvent>(Handler);
            if (IsInstanceValid(owner))
                owner.QueueFree();
        }
    }

    private static void VerifyNestedMutationCommit()
    {
        var order = new List<int>();
        void Added(NestedMutationEvent evt) => order.Add(100 + evt.Depth);
        void Mutating(NestedMutationEvent evt)
        {
            order.Add(evt.Depth);
            if (evt.Depth == 0)
            {
                EventChannel.On<NestedMutationEvent>(Added, priority: -10);
                EventChannel.Emit(new NestedMutationEvent(depth: 1));
            }
        }

        try
        {
            EventChannel.On<NestedMutationEvent>(Mutating);
            EventChannel.Emit(new NestedMutationEvent(depth: 0));
            AssertSequence(order, 0, 1);

            order.Clear();
            EventChannel.Emit(new NestedMutationEvent(depth: 2));
            AssertSequence(order, 102, 2);
        }
        finally
        {
            EventChannel.Off<NestedMutationEvent>(Mutating);
            EventChannel.Off<NestedMutationEvent>(Added);
        }
    }

#if DEBUG
    private async Task VerifyDebugListenerSourcesAsync()
    {
        void OnHandler(DebugSourceEvent _) { }
        void OnceHandler(DebugSourceEvent _)
        {
            Assert(!ContainsDebugHandler(nameof(OnceHandler)),
                "Once 回调执行期间仍暴露为 Debug 监听来源");
        }
        void ScopeHandler(DebugSourceEvent _) { }
        void BoundHandler(DebugSourceEvent _) { }

        var owner = new Node { Name = "DebugBoundOwner" };
        var scope = new EventScope();
        AddChild(owner);
        try
        {
            EventChannel.On<DebugSourceEvent>(OnHandler, priority: -3);
            EventChannel.Once<DebugSourceEvent>(OnceHandler);
            scope.On<DebugSourceEvent>(ScopeHandler, priority: 7);
            EventChannel.Bind<DebugSourceEvent>(owner, BoundHandler, priority: 2);

            EventChannel.EventDebugListenerEntry[] listeners =
                EventChannel.GetDebugListenerSnapshot(typeof(DebugSourceEvent));
            AssertEqual(4, listeners.Length, "Debug 监听来源数量错误");

            EventChannel.EventDebugListenerEntry onEntry = FindDebugListener(
                listeners,
                EventChannel.EventDebugRegistrationKind.On);
            EventChannel.EventDebugListenerEntry onceEntry = FindDebugListener(
                listeners,
                EventChannel.EventDebugRegistrationKind.Once);
            EventChannel.EventDebugListenerEntry scopeEntry = FindDebugListener(
                listeners,
                EventChannel.EventDebugRegistrationKind.EventScope);
            EventChannel.EventDebugListenerEntry bindEntry = FindDebugListener(
                listeners,
                EventChannel.EventDebugRegistrationKind.Bind);

            Assert(onEntry.HandlerDisplayName.Contains(nameof(OnHandler), StringComparison.Ordinal) &&
                onEntry.Priority == -3 &&
                onEntry.Age >= TimeSpan.Zero &&
                onEntry.OwnerName == "静态",
                "On 监听来源缺少方法、优先级、注册时长或静态标记");
            Assert(onceEntry.HandlerDisplayName.Contains(nameof(OnceHandler), StringComparison.Ordinal),
                "Once 监听来源方法错误");
            Assert(scopeEntry.HandlerDisplayName.Contains(nameof(ScopeHandler), StringComparison.Ordinal) &&
                scopeEntry.Priority == 7,
                "EventScope 监听来源方法或优先级错误");
            Assert(bindEntry.HandlerDisplayName.Contains(nameof(BoundHandler), StringComparison.Ordinal) &&
                bindEntry.OwnerName == owner.Name.ToString() &&
                bindEntry.OwnerInstanceId == owner.GetInstanceId() &&
                bindEntry.OwnerPath.Contains(owner.Name.ToString(), StringComparison.Ordinal),
                "Bind 监听来源缺少 Node 身份或路径");

            EventChannel.Off<DebugSourceEvent>(OnHandler);
            Assert(!ContainsDebugHandler(nameof(OnHandler)), "Off 后仍残留 Debug 监听来源");

            EventChannel.Emit(new DebugSourceEvent());
            Assert(!ContainsDebugHandler(nameof(OnceHandler)), "Once 派发后仍残留 Debug 监听来源");

            scope.Dispose();
            Assert(!ContainsDebugHandler(nameof(ScopeHandler)),
                "EventScope.Dispose 后仍残留 Debug 监听来源");

            owner.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Assert(!ContainsDebugHandler(nameof(BoundHandler)),
                "Bind Owner 退出树后仍残留 Debug 监听来源");
        }
        finally
        {
            EventChannel.Off<DebugSourceEvent>(OnHandler);
            EventChannel.Off<DebugSourceEvent>(OnceHandler);
            EventChannel.Off<DebugSourceEvent>(ScopeHandler);
            EventChannel.Off<DebugSourceEvent>(BoundHandler);
            scope.Dispose();
            if (IsInstanceValid(owner))
                owner.QueueFree();
        }
    }

    private static void VerifyDebugListenerLimit()
    {
        var handlers = new List<Action<DebugLimitEvent>>(65);
        try
        {
            for (int index = 0; index < 65; index++)
            {
                Action<DebugLimitEvent> handler = CreateDebugLimitHandler(index);
                handlers.Add(handler);
                EventChannel.On(handler);
            }

            AssertEqual(
                EventChannel.MaxDebugListenerEntries,
                EventChannel.GetDebugListenerSnapshot(typeof(DebugLimitEvent)).Length,
                "Debug 监听来源快照没有遵守上限");
        }
        finally
        {
            for (int index = 0; index < handlers.Count; index++)
                EventChannel.Off(handlers[index]);
        }
    }

    private static Action<DebugLimitEvent> CreateDebugLimitHandler(int marker) =>
        _ => GC.KeepAlive(marker);

    private static EventChannel.EventDebugListenerEntry FindDebugListener(
        EventChannel.EventDebugListenerEntry[] listeners,
        EventChannel.EventDebugRegistrationKind kind)
    {
        for (int index = 0; index < listeners.Length; index++)
        {
            if (listeners[index].RegistrationKind == kind)
                return listeners[index];
        }

        throw new InvalidOperationException($"Debug 监听来源缺少注册方式 {kind}");
    }

    private static bool ContainsDebugHandler(string methodName)
    {
        EventChannel.EventDebugListenerEntry[] listeners =
            EventChannel.GetDebugListenerSnapshot(typeof(DebugSourceEvent));
        for (int index = 0; index < listeners.Length; index++)
        {
            if (listeners[index].HandlerDisplayName.Contains(methodName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
#endif

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
#if DEBUG
    private readonly struct DebugSourceEvent : IEventMessage;
    private readonly struct DebugLimitEvent : IEventMessage;
#endif

    private readonly struct NestedMutationEvent : IEventMessage
    {
        public int Depth { get; }

        public NestedMutationEvent(int depth)
        {
            Depth = depth;
        }
    }

    private readonly struct OnceEvent : IEventMessage
    {
        public bool IsNested { get; }

        public OnceEvent(bool isNested)
        {
            IsNested = isNested;
        }
    }
}
