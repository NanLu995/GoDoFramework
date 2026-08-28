using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>InputActionRouter 的优先级、消费、生命周期、门禁与分配回归入口。</summary>
public sealed partial class InputActionRouterRegression : Node
{
    private static readonly InputActionId Back = InputActionId.Create("ui.back");
    private static readonly InputActionId Confirm = InputActionId.Create("ui.confirm");
    private static readonly InputContextId Gameplay = InputContextId.Create("gameplay");
    private static readonly InputContextId Pause = InputContextId.Create("pause");

    private int _passed;
    private InputService _input = null!;
    private InputActionRouter _router = null!;
    private FakeBackend _backend = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("顶层优先、Pass 与 Handled", VerifyPriorityAndConsumption);
            Run("同 Scope 重叠与多 Transition 顺序", VerifyBindingValidationAndOrder);
            Run("同一 Sequence 只派发一次", VerifySingleDispatchPerSequence);
            Run("派发中创建与释放延迟提交", VerifyMutationDuringDispatch);
            Run("处理器异常报告并消费", VerifyHandlerFailure);
            Run("Scope 与 Binding 乱序幂等释放", VerifyLifetimeHandles);
            Run("Route Revision 释放重触发门禁", VerifyRouteRevisionGate);
            Run("Context Revision 与 Action 独立门禁", VerifyContextRevisionGate);
            Run("Router 稳态零托管分配", VerifyAllocations);
            Run("Shutdown 后句柄安全", VerifyShutdown);

            GD.Print($"[InputActionRouterRegression] PASS ({_passed}/10)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            _router?.Shutdown();
            _input?.Shutdown();
            GD.PushError($"[InputActionRouterRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        _router?.Shutdown();
        _input?.Shutdown();
        _input = new InputService();
        _backend = new FakeBackend();
        _input.InstallBackend(_backend);
        _router = new InputActionRouter(_input);
        _input.SetBaseContext(Gameplay);
        verification();
        _passed++;
        GD.Print($"[InputActionRouterRegression] PASS: {name}");
    }

    private void VerifyPriorityAndConsumption()
    {
        int lower = 0;
        int upper = 0;
        InputRouteScope lowerScope = _router.PushScope("Lower");
        lowerScope.Bind(Back, InputActionTransitions.Performed, (_, _) =>
        {
            lower++;
            return InputRouteResult.Handled;
        });
        InputRouteScope upperScope = _router.PushScope("Upper");
        InputRouteBinding upperBinding = upperScope.Bind(
            Back,
            InputActionTransitions.Performed,
            (_, _) =>
            {
                upper++;
                return InputRouteResult.Pass;
            });
        DispatchIdle();

        Dispatch(Back, Performed());
        AssertEqual(1, upper, "顶层 Scope 未先调用");
        AssertEqual(1, lower, "Pass 没有传播到低层 Scope");

        upperBinding.Dispose();
        upperScope.Bind(Back, InputActionTransitions.Performed, (_, _) =>
        {
            upper++;
            return InputRouteResult.Handled;
        });
        DispatchIdle();
        Dispatch(Back, Performed());
        AssertEqual(2, upper, "顶层 Handled 未调用");
        AssertEqual(1, lower, "Handled 后仍传播到低层 Scope");
    }

    private void VerifyBindingValidationAndOrder()
    {
        var order = new List<string>();
        InputRouteScope scope = _router.PushScope("Order");
        scope.Bind(Back, InputActionTransitions.Started, (_, matched) =>
        {
            order.Add(matched.ToString());
            return InputRouteResult.Pass;
        });
        scope.Bind(Back, InputActionTransitions.Performed, (_, matched) =>
        {
            order.Add(matched.ToString());
            return InputRouteResult.Pass;
        });
        InputRouteScope upperScope = _router.PushScope("UpperOrder");
        upperScope.Bind(Back, InputActionTransitions.Completed, (_, matched) =>
        {
            order.Add(matched.ToString());
            return InputRouteResult.Pass;
        });
        AssertThrows<InputOperationException>(
            () => scope.Bind(Back, InputActionTransitions.Started, (_, _) => InputRouteResult.Pass),
            "同 Scope 接受了重叠 Transition");
        AssertThrows<ArgumentException>(
            () => scope.Bind(Confirm, InputActionTransitions.None, (_, _) => InputRouteResult.Pass),
            "接受了空 Transition");
        DispatchIdle();

        Dispatch(Back, new InputActionSample(
            Vector3.One,
            true,
            InputActionStatus.Performed,
            InputActionTransitions.Started |
                InputActionTransitions.Performed |
                InputActionTransitions.Completed,
            0.1f,
            1f));
        AssertEqual(3, order.Count, "多 Transition 没有调用跨 Scope 的不重叠 Binding");
        AssertEqual("Started", order[0], "Started 没有先于 Performed");
        AssertEqual("Performed", order[1], "Performed 派发顺序错误");
        AssertEqual("Completed", order[2], "跨 Scope 的 Completed 早于低层迁移");
    }

    private void VerifySingleDispatchPerSequence()
    {
        int calls = 0;
        _router.PushScope("Single").Bind(
            Back,
            InputActionTransitions.Performed,
            (_, _) => { calls++; return InputRouteResult.Pass; });
        DispatchIdle();
        _backend.Set(Back, Performed());
        _input.Update();
        InputFrame frame = _input.Frame;
        _router.Dispatch(frame);
        _router.Dispatch(frame);
        AssertEqual(1, calls, "同一 Sequence 重复派发");
    }

    private void VerifyMutationDuringDispatch()
    {
        int lowerCalls = 0;
        int addedCalls = 0;
        InputRouteScope lower = _router.PushScope("Lower");
        InputRouteBinding lowerBinding = lower.Bind(
            Back,
            InputActionTransitions.Performed,
            (_, _) => { lowerCalls++; return InputRouteResult.Pass; });
        InputRouteScope upper = _router.PushScope("Upper");
        upper.Bind(Back, InputActionTransitions.Performed, (_, _) =>
        {
            InputRouteScope added = _router.PushScope("Added");
            added.Bind(Back, InputActionTransitions.Performed, (_, _) =>
            {
                addedCalls++;
                return InputRouteResult.Handled;
            });
            lowerBinding.Dispose();
            return InputRouteResult.Pass;
        });
        DispatchIdle();
        Dispatch(Back, Performed());
        AssertEqual(1, lowerCalls, "派发中释放错误跳过了当前遍历项");
        AssertEqual(0, addedCalls, "派发中新建 Scope 在同帧生效");

        DispatchIdle();
        Dispatch(Back, Performed());
        AssertEqual(1, addedCalls, "延迟创建的顶层 Scope 未在后续按下生效");
        AssertEqual(1, lowerCalls, "已释放 Binding 在后续帧仍生效");
    }

    private void VerifyHandlerFailure()
    {
        int lowerCalls = 0;
        ErrorReport? report = null;
        void OnError(ErrorReport value) => report = value;
        ErrorHub.OnError += OnError;
        try
        {
            _router.PushScope("Lower").Bind(
                Back,
                InputActionTransitions.Performed,
                (_, _) => { lowerCalls++; return InputRouteResult.Pass; });
            _router.PushScope("Throwing").Bind(
                Back,
                InputActionTransitions.Performed,
                (_, _) => throw new InvalidOperationException("route failure"));
            DispatchIdle();
            Dispatch(Back, Performed());
            Assert(report.HasValue, "处理器异常没有上报 ErrorHub");
            ErrorReport captured = report.GetValueOrDefault();
            Assert(captured.Context?.Contains("Scope=Throwing", StringComparison.Ordinal) == true,
                "异常报告缺少 Scope 名称");
            AssertEqual(0, lowerCalls, "异常后仍传播到低层 Scope");

            DispatchIdle();
            Assert(_input.Frame.Sequence > 0, "处理器异常后 Router 未继续工作");
        }
        finally
        {
            ErrorHub.OnError -= OnError;
        }
    }

    private void VerifyLifetimeHandles()
    {
        InputRouteScope first = _router.PushScope("First");
        InputRouteBinding binding = first.Bind(
            Back,
            InputActionTransitions.Performed,
            (_, _) => InputRouteResult.Pass);
        InputRouteScope second = _router.PushScope("Second");
        second.Bind(Confirm, InputActionTransitions.Performed, (_, _) => InputRouteResult.Pass);
        first.Dispose();
        first.Dispose();
        binding.Dispose();
        second.Dispose();
        AssertEqual<ulong>(6, _router.RouteRevision, "Scope/Binding 释放 Revision 错误");
    }

    private void VerifyRouteRevisionGate()
    {
        int calls = 0;
        InputRouteScope scope = _router.PushScope("Base");
        scope.Bind(Back, InputActionTransitions.Performed, (_, _) =>
        {
            calls++;
            return InputRouteResult.Pass;
        });
        DispatchIdle();
        Dispatch(Back, Performed());
        AssertEqual(1, calls, "首次按下未派发");

        _router.PushScope("Pause").Bind(
            Back,
            InputActionTransitions.Performed,
            (_, _) => { calls++; return InputRouteResult.Pass; });
        Dispatch(Back, Performed());
        AssertEqual(1, calls, "按住期间 Route 变化发生重触发");
        Assert(_router.IsRetriggerGated(Back), "Route 变化未建立门禁");
        DispatchIdle();
        Assert(!_router.IsRetriggerGated(Back), "Idle 后门禁未解除");
        Dispatch(Back, Performed());
        Assert(calls > 1, "释放后重新按下未恢复派发");
    }

    private void VerifyContextRevisionGate()
    {
        int backCalls = 0;
        int confirmCalls = 0;
        InputRouteScope scope = _router.PushScope("Context");
        scope.Bind(Back, InputActionTransitions.Performed, (_, _) =>
        {
            backCalls++;
            return InputRouteResult.Pass;
        });
        scope.Bind(Confirm, InputActionTransitions.Performed, (_, _) =>
        {
            confirmCalls++;
            return InputRouteResult.Pass;
        });
        DispatchIdle();
        Dispatch(Back, Performed());
        _input.SetBaseContext(Pause);
        Dispatch(Back, Performed());
        AssertEqual(1, backCalls, "Context 切换后按住 Action 重触发");
        Assert(_router.IsRetriggerGated(Back), "Context 变化未门禁按住 Action");
        Assert(!_router.IsRetriggerGated(Confirm), "不同 Idle Action 被错误门禁");

        Dispatch(Confirm, Performed());
        AssertEqual(1, confirmCalls, "不同 Action 被门禁互相影响");

        _input.SetBaseContext(Pause);
        Dispatch(Confirm, Performed());
        AssertEqual(2, confirmCalls, "相同 Base Context 的无变化设置错误推进修订号");
    }

    private void VerifyAllocations()
    {
        InputRouteScope scope = _router.PushScope("Alloc");
        scope.Bind(Back, InputActionTransitions.Performed, StaticPass);
        DispatchIdle();
        for (int index = 0; index < 100; index++)
            Dispatch(Back, Idle());

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1_000; index++)
            Dispatch(Back, Idle());
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        AssertEqual(0L, allocated, $"Router 无 Transition 稳态分配: {allocated} bytes");

        for (int index = 0; index < 100; index++)
            Dispatch(Back, Performed());
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1_000; index++)
            Dispatch(Back, Performed());
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        AssertEqual(0L, allocated, $"Router 固定 Handler 稳态分配: {allocated} bytes");
    }

    private void VerifyShutdown()
    {
        InputRouteScope scope = _router.PushScope("Shutdown");
        InputRouteBinding binding = scope.Bind(
            Back,
            InputActionTransitions.Performed,
            StaticPass);
        _router.Shutdown();
        _router.Shutdown();
        binding.Dispose();
        scope.Dispose();
        AssertThrows<InputOperationException>(() => _router.PushScope("Late"),
            "Shutdown 后仍能创建 Scope");
    }

    private void DispatchIdle()
    {
        _backend.Set(Back, Idle());
        _backend.Set(Confirm, Idle());
        _input.Update();
        _router.Dispatch(_input.Frame);
    }

    private void Dispatch(InputActionId action, InputActionSample sample)
    {
        _backend.Set(action, sample);
        _input.Update();
        _router.Dispatch(_input.Frame);
    }

    private static InputActionSample Idle() => new(
        Vector3.Zero,
        false,
        InputActionStatus.Idle,
        InputActionTransitions.None,
        0f,
        0f);

    private static InputActionSample Performed() => new(
        Vector3.One,
        true,
        InputActionStatus.Performed,
        InputActionTransitions.Performed,
        0f,
        1f);

    private static InputRouteResult StaticPass(
        InputActionFrameState _,
        InputActionTransitions __) => InputRouteResult.Pass;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}；期望 {expected}，实际 {actual}");
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class FakeBackend : IInputBackend
    {
        private static readonly InputActionDescriptor[] ActionDescriptors =
        {
            new(Back, InputActionValueType.Bool),
            new(Confirm, InputActionValueType.Bool),
        };
        private static readonly InputContextId[] ContextDescriptors = { Gameplay, Pause };
        private readonly InputActionSample[] _samples = { Idle(), Idle() };

        public InputBackendCapabilities Capabilities => InputBackendCapabilities.None;
        public InputDeviceKind ActiveDevice => InputDeviceKind.KeyboardMouse;
        public IReadOnlyList<InputActionDescriptor> Actions => ActionDescriptors;
        public IReadOnlyList<InputContextId> Contexts => ContextDescriptors;

        public void Initialize()
        {
        }

        public void ApplyContexts(ReadOnlySpan<InputContextId> contexts)
        {
        }

        public void Sample(Span<InputActionSample> destination) => _samples.CopyTo(destination);

        public void Shutdown()
        {
        }

        public void Set(InputActionId action, InputActionSample sample) =>
            _samples[action == Back ? 0 : 1] = sample;
    }
}
