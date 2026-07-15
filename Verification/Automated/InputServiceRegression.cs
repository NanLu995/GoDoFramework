using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>InputService 核心快照、Context 栈与失败语义的无交互回归入口。</summary>
public sealed partial class InputServiceRegression : Node
{
    private static readonly InputActionId Jump = InputActionId.Create("gameplay.jump");
    private static readonly InputActionId Throttle = InputActionId.Create("gameplay.throttle");
    private static readonly InputActionId Move = InputActionId.Create("gameplay.move");
    private static readonly InputActionId Aim = InputActionId.Create("gameplay.aim");
    private static readonly InputContextId Gameplay = InputContextId.Create("gameplay");
    private static readonly InputContextId Overlay = InputContextId.Create("overlay");
    private static readonly InputContextId Pause = InputContextId.Create("pause");
    private static readonly InputContextId Debug = InputContextId.Create("debug");

    private int _passed;
    private int _deviceChangeCount;
    private InputDeviceChangedEvent _lastDeviceChange;
    private InputService _service = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("Action 与 Context ID", VerifyIds);
            Run("未安装后端失败", VerifyMissingBackend);
            Run("安装与首次采样", VerifyInstallAndFirstSample);
            Run("活动设备变化通知", VerifyDeviceChangeNotification);
            Run("Action 状态与各类轴值", VerifyActionStates);
            Run("未知 Action 与类型错误", VerifyActionFailures);
            Run("旧 Frame 失效", VerifyStaleFrame);
            Run("Context Overlay 与 Exclusive", VerifyContextComposition);
            Run("Context 栈误用", VerifyContextFailures);
            Run("Context 失败保持原状态", VerifyContextFailureIsAtomic);
            Run("采样失败保持上一帧", VerifySampleFailureIsAtomic);
            Run("拒绝重复后端与重复布局", VerifyBackendRejection);
            Run("热路径零托管分配", VerifyHotReadAllocations);
            Run("关闭幂等", VerifyShutdown);

            _service.Shutdown();
            GD.Print($"[InputServiceRegression] PASS ({_passed}/14)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            _service?.Shutdown();
            GD.PushError($"[InputServiceRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        _service?.Shutdown();
        _service = new InputService();
        verification();
        _passed++;
        GD.Print($"[InputServiceRegression] PASS: {name}");
    }

    private static void VerifyIds()
    {
        InputActionId action = InputActionId.Create("gameplay.move");
        InputContextId context = InputContextId.Create("gameplay");
        var actions = new HashSet<InputActionId> { action, InputActionId.Create("gameplay.move") };
        var contexts = new HashSet<InputContextId> { context, InputContextId.Create("gameplay") };

        AssertEqual("gameplay.move", action.Value, "Action ID 值错误");
        AssertEqual("gameplay", context.Value, "Context ID 值错误");
        AssertEqual(1, actions.Count, "相同 Action ID 没有合并");
        AssertEqual(1, contexts.Count, "相同 Context ID 没有合并");
        Assert(default(InputActionId).IsEmpty, "默认 Action ID 应为空");
        Assert(default(InputContextId).IsEmpty, "默认 Context ID 应为空");
        AssertThrows<ArgumentException>(() => InputActionId.Create(" move"), "Action ID 接受了首尾空白");
        AssertThrows<ArgumentException>(() => InputContextId.Create(" "), "Context ID 接受了空白值");
    }

    private void VerifyMissingBackend()
    {
        Assert(!_service.IsReady, "未安装后端时 IsReady 为 true");
        AssertEqual(InputDeviceKind.Unknown, _service.ActiveDevice, "未安装后端时设备类型错误");
        AssertEqual(InputBackendCapabilities.None, _service.Capabilities, "未安装后端时能力错误");
        AssertThrows<InputOperationException>(() => _ = _service.Frame, "未安装后端仍能读取 Frame");
        AssertThrows<InputOperationException>(() => _service.SetBaseContext(Gameplay), "未安装后端仍能设置 Context");
    }

    private void VerifyInstallAndFirstSample()
    {
        FakeInputBackend backend = InstallDefaultBackend();

        Assert(_service.IsReady, "安装后端后 IsReady 为 false");
        AssertEqual(InputDeviceKind.Gamepad, _service.ActiveDevice, "活动设备没有来自后端");
        AssertEqual(
            InputBackendCapabilities.DeviceTracking | InputBackendCapabilities.Rebinding,
            _service.Capabilities,
            "后端能力没有透传");
        AssertEqual(1, backend.InitializeCount, "后端没有初始化一次");
        AssertThrows<InputOperationException>(() => _ = _service.Frame, "首次采样前仍能读取 Frame");

        _service.Update();

        AssertEqual<ulong>(1, _service.Frame.Sequence, "首次采样序号错误");
        AssertEqual(1, backend.SampleCount, "首次采样没有调用后端一次");
    }

    private void VerifyActionStates()
    {
        FakeInputBackend backend = InstallDefaultBackend();
        backend.SetSample(0, new InputActionSample(Vector3.One, pressed: true));
        backend.SetSample(1, new InputActionSample(new Vector3(0.75f, 0f, 0f), pressed: true));
        backend.SetSample(2, new InputActionSample(new Vector3(0.25f, -0.5f, 0f), pressed: true));
        backend.SetSample(3, new InputActionSample(new Vector3(1f, 2f, 3f), pressed: true));

        _service.Update();
        InputFrame first = _service.Frame;

        Assert(first.Pressed(Jump), "Jump 未处于按下状态");
        Assert(first.JustPressed(Jump), "Jump 首次按下没有 JustPressed");
        Assert(!first.JustReleased(Jump), "Jump 首次按下错误产生 JustReleased");
        AssertApprox(0.75f, first.Axis1(Throttle), "Axis1D 值错误");
        Assert(first.Axis2(Move).IsEqualApprox(new Vector2(0.25f, -0.5f)), "Axis2D 值错误");
        Assert(first.Axis3(Aim).IsEqualApprox(new Vector3(1f, 2f, 3f)), "Axis3D 值错误");

        _service.Update();
        InputFrame held = _service.Frame;
        Assert(held.Pressed(Jump), "持续按下后 Jump 状态丢失");
        Assert(!held.JustPressed(Jump), "持续按下重复产生 JustPressed");

        backend.SetSample(0, new InputActionSample(Vector3.Zero, pressed: false));
        _service.Update();
        InputFrame released = _service.Frame;
        Assert(!released.Pressed(Jump), "释放后 Jump 仍处于按下状态");
        Assert(released.JustReleased(Jump), "释放没有产生 JustReleased");
    }

    private void VerifyDeviceChangeNotification()
    {
        _deviceChangeCount = 0;
        _lastDeviceChange = default;
        EventChannel.On<InputDeviceChangedEvent>(OnInputDeviceChanged);
        try
        {
            FakeInputBackend backend = InstallDefaultBackend();
            AssertEqual(0, _deviceChangeCount, "安装阶段提前发布了设备变化");

            _service.Update();
            AssertEqual(1, _deviceChangeCount, "首次采样没有发布活动设备");
            AssertEqual(InputDeviceKind.Unknown, _lastDeviceChange.Previous, "首次设备变化的 Previous 错误");
            AssertEqual(InputDeviceKind.Gamepad, _lastDeviceChange.Current, "首次设备变化的 Current 错误");

            _service.Update();
            AssertEqual(1, _deviceChangeCount, "相同设备重复发布变化");

            backend.ActiveDevice = InputDeviceKind.KeyboardMouse;
            _service.Update();
            AssertEqual(2, _deviceChangeCount, "设备切换没有发布变化");
            AssertEqual(InputDeviceKind.Gamepad, _lastDeviceChange.Previous, "切换后的 Previous 错误");
            AssertEqual(InputDeviceKind.KeyboardMouse, _lastDeviceChange.Current, "切换后的 Current 错误");
        }
        finally
        {
            EventChannel.Off<InputDeviceChangedEvent>(OnInputDeviceChanged);
        }
    }

    private void OnInputDeviceChanged(InputDeviceChangedEvent evt)
    {
        _deviceChangeCount++;
        _lastDeviceChange = evt;
    }

    private void VerifyActionFailures()
    {
        InstallDefaultBackend();
        _service.Update();
        InputFrame frame = _service.Frame;

        AssertThrows<InputOperationException>(
            () => frame.Pressed(InputActionId.Create("missing")),
            "未知 Action 没有失败");
        AssertThrows<InputOperationException>(() => frame.Axis1(Move), "Axis2D 被当作 Axis1D 读取");
        AssertThrows<ArgumentException>(() => frame.Pressed(default), "默认 Action ID 没有失败");
        AssertThrows<InvalidOperationException>(() => default(InputFrame).Pressed(Jump), "默认 Frame 没有失败");
    }

    private void VerifyStaleFrame()
    {
        InstallDefaultBackend();
        _service.Update();
        InputFrame first = _service.Frame;
        _service.Update();

        AssertThrows<InputOperationException>(() => first.Pressed(Jump), "旧 Frame 仍可读取");
        AssertEqual<ulong>(2, _service.Frame.Sequence, "第二次采样序号错误");
    }

    private void VerifyContextComposition()
    {
        FakeInputBackend backend = InstallDefaultBackend();

        _service.SetBaseContext(Gameplay);
        AssertContexts(backend, Gameplay);
        _service.PushContext(Overlay, InputContextMode.Overlay);
        AssertContexts(backend, Gameplay, Overlay);
        _service.PushContext(Pause, InputContextMode.Exclusive);
        AssertContexts(backend, Pause);
        _service.PushContext(Debug, InputContextMode.Overlay);
        AssertContexts(backend, Pause, Debug);

        Assert(!_service.IsContextActive(Gameplay), "Exclusive 上下文没有屏蔽 Gameplay");
        Assert(_service.IsContextActive(Pause), "Pause 未处于有效集合");
        Assert(_service.IsContextActive(Debug), "Exclusive 上方 Overlay 未处于有效集合");

        _service.PopContext(Debug);
        AssertContexts(backend, Pause);
        _service.PopContext(Pause);
        AssertContexts(backend, Gameplay, Overlay);
        _service.PopContext(Overlay);
        AssertContexts(backend, Gameplay);
    }

    private void VerifyContextFailures()
    {
        InstallDefaultBackend();
        _service.SetBaseContext(Gameplay);

        AssertThrows<InputOperationException>(
            () => _service.PushContext(Gameplay),
            "允许重复 Push 同一 Context");
        AssertThrows<InputOperationException>(
            () => _service.PopContext(Pause),
            "允许从只有 Base 的栈中 Pop");
        AssertThrows<InputOperationException>(
            () => _service.PushContext(InputContextId.Create("missing")),
            "允许 Push 未知 Context");

        _service.PushContext(Overlay);
        AssertThrows<InputOperationException>(
            () => _service.PopContext(Pause),
            "允许按错误顺序 Pop Context");
        Assert(_service.IsContextActive(Overlay), "错误 Pop 改变了 Context 栈");
    }

    private void VerifyContextFailureIsAtomic()
    {
        FakeInputBackend backend = InstallDefaultBackend();
        _service.SetBaseContext(Gameplay);
        backend.FailNextApply = true;

        AssertThrows<InputOperationException>(
            () => _service.PushContext(Pause),
            "后端 Context 失败没有传播");

        Assert(_service.IsContextActive(Gameplay), "Context 失败后 Base 状态丢失");
        Assert(!_service.IsContextActive(Pause), "Context 失败后仍提交了新状态");
        AssertContexts(backend, Gameplay);
    }

    private void VerifySampleFailureIsAtomic()
    {
        FakeInputBackend backend = InstallDefaultBackend();
        backend.SetSample(1, new InputActionSample(new Vector3(0.25f, 0f, 0f), false));
        _service.Update();
        InputFrame stable = _service.Frame;

        backend.SetSample(1, new InputActionSample(new Vector3(0.9f, 0f, 0f), true));
        backend.FailNextSample = true;
        AssertThrows<InputOperationException>(() => _service.Update(), "采样失败没有传播");

        AssertApprox(0.25f, stable.Axis1(Throttle), "采样失败改变了上一帧");
        AssertEqual(stable.Sequence, _service.Frame.Sequence, "采样失败推进了序号");

        _service.Update();
        AssertApprox(0.9f, _service.Frame.Axis1(Throttle), "失败后的下一次采样没有恢复");
    }

    private void VerifyBackendRejection()
    {
        FakeInputBackend installed = InstallDefaultBackend();
        var second = FakeInputBackend.CreateDefault();
        AssertThrows<InputOperationException>(() => _service.InstallBackend(second), "允许重复安装后端");
        AssertEqual(0, second.InitializeCount, "被拒绝的第二后端仍被初始化");
        AssertEqual(0, installed.ShutdownCount, "拒绝第二后端时关闭了现有后端");

        _service.Shutdown();
        var duplicateActions = new FakeInputBackend(
            new[]
            {
                new InputActionDescriptor(Jump, InputActionValueType.Bool),
                new InputActionDescriptor(Jump, InputActionValueType.Bool),
            },
            new[] { Gameplay });
        AssertThrows<InputOperationException>(
            () => _service.InstallBackend(duplicateActions),
            "允许安装重复 Action 布局");
        AssertEqual(1, duplicateActions.ShutdownCount, "无效后端没有清理");
    }

    private void VerifyHotReadAllocations()
    {
        InstallDefaultBackend();
        _service.Update();
        InputFrame frame = _service.Frame;
        Vector2 value = default;
        for (int index = 0; index < 100; index++)
            value = frame.Axis2(Move);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 10_000; index++)
            value = frame.Axis2(Move);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(value);

        AssertEqual(0L, allocated, $"InputFrame 热读取产生托管分配: {allocated} bytes");
    }

    private void VerifyShutdown()
    {
        FakeInputBackend backend = InstallDefaultBackend();
        _service.Update();
        _service.Shutdown();
        _service.Shutdown();

        AssertEqual(1, backend.ShutdownCount, "重复 Shutdown 多次关闭后端");
        Assert(!_service.IsReady, "Shutdown 后 IsReady 仍为 true");
        AssertThrows<InputOperationException>(() => _ = _service.Frame, "Shutdown 后仍能读取 Frame");
    }

    private FakeInputBackend InstallDefaultBackend()
    {
        FakeInputBackend backend = FakeInputBackend.CreateDefault();
        _service.InstallBackend(backend);
        return backend;
    }

    private static void AssertContexts(FakeInputBackend backend, params InputContextId[] expected)
    {
        AssertEqual(expected.Length, backend.ActiveContexts.Count, "后端有效 Context 数量错误");
        for (int index = 0; index < expected.Length; index++)
            AssertEqual(expected[index], backend.ActiveContexts[index], $"后端 Context 顺序错误，位置 {index}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertApprox(float expected, float actual, string message)
    {
        if (!Mathf.IsEqualApprox(expected, actual))
            throw new InvalidOperationException($"{message}；期望 {expected}，实际 {actual}");
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

    private sealed class FakeInputBackend : IInputBackend
    {
        private readonly InputActionDescriptor[] _actions;
        private readonly InputContextId[] _contexts;
        private readonly InputActionSample[] _samples;

        public InputBackendCapabilities Capabilities { get; set; } =
            InputBackendCapabilities.DeviceTracking | InputBackendCapabilities.Rebinding;
        public InputDeviceKind ActiveDevice { get; set; } = InputDeviceKind.Gamepad;
        public IReadOnlyList<InputActionDescriptor> Actions => _actions;
        public IReadOnlyList<InputContextId> Contexts => _contexts;
        public List<InputContextId> ActiveContexts { get; } = new();
        public int InitializeCount { get; private set; }
        public int SampleCount { get; private set; }
        public int ShutdownCount { get; private set; }
        public bool FailNextApply { get; set; }
        public bool FailNextSample { get; set; }

        public FakeInputBackend(InputActionDescriptor[] actions, InputContextId[] contexts)
        {
            _actions = actions;
            _contexts = contexts;
            _samples = new InputActionSample[actions.Length];
        }

        public static FakeInputBackend CreateDefault() => new(
            new[]
            {
                new InputActionDescriptor(Jump, InputActionValueType.Bool),
                new InputActionDescriptor(Throttle, InputActionValueType.Axis1D),
                new InputActionDescriptor(Move, InputActionValueType.Axis2D),
                new InputActionDescriptor(Aim, InputActionValueType.Axis3D),
            },
            new[] { Gameplay, Overlay, Pause, Debug });

        public void Initialize() => InitializeCount++;

        public void ApplyContexts(ReadOnlySpan<InputContextId> contexts)
        {
            if (FailNextApply)
            {
                FailNextApply = false;
                throw new InvalidOperationException("Apply failure");
            }

            ActiveContexts.Clear();
            for (int index = 0; index < contexts.Length; index++)
                ActiveContexts.Add(contexts[index]);
        }

        public void Sample(Span<InputActionSample> destination)
        {
            SampleCount++;
            if (FailNextSample)
            {
                FailNextSample = false;
                throw new InvalidOperationException("Sample failure");
            }
            if (destination.Length != _samples.Length)
                throw new InvalidOperationException("Sample buffer length mismatch");

            _samples.CopyTo(destination);
        }

        public void Shutdown() => ShutdownCount++;

        public void SetSample(int index, InputActionSample sample) => _samples[index] = sample;
    }
}
