using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    private static readonly InputBindingId JumpKeyboard = InputBindingId.Create("gameplay.jump.keyboard");

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
            Run("重绑定可选能力", VerifyRebindingCapability);
            Run("输入提示查询可选能力", VerifyPromptQueryCapability);
            Run("活动设备变化通知", VerifyDeviceChangeNotification);
            Run("Action 状态与各类轴值", VerifyActionStates);
#if DEBUG
            Run("Debug-only 输入快照", VerifyDebugSnapshot);
#endif
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
#if DEBUG
            GD.Print($"[InputServiceRegression] PASS ({_passed}/17)");
#else
            GD.Print($"[InputServiceRegression] PASS ({_passed}/16)");
#endif
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
        Assert(!_service.TryGetRebinding(out IInputRebinding? rebinding), "未安装后端时返回了重绑定能力");
        Assert(rebinding == null, "未安装后端时返回了重绑定实例");
        Assert(
            !_service.TryGetRebindingPersistence(out IInputRebindingPersistence? persistence),
            "未安装后端时返回了绑定持久化能力");
        Assert(persistence == null, "未安装后端时返回了绑定持久化实例");
        Assert(!_service.TryGetPromptQuery(out IInputPromptQuery? prompts), "未安装后端时返回了提示查询能力");
        Assert(prompts == null, "未安装后端时返回了提示查询实例");
        AssertThrows<InputOperationException>(() => _ = _service.Frame, "未安装后端仍能读取 Frame");
        AssertThrows<InputOperationException>(() => _service.SetBaseContext(Gameplay), "未安装后端仍能设置 Context");
    }

    private void VerifyPromptQueryCapability()
    {
        FakeInputBackend backend = InstallDefaultBackend();
        Assert(_service.TryGetPromptQuery(out IInputPromptQuery? prompts), "支持提示查询的后端未暴露能力");
        Assert(ReferenceEquals(backend.PromptQuery, prompts), "InputService 没有返回后端提示查询实例");

        IReadOnlyList<InputPromptInfo> gamepad = prompts!.GetPrompts(Gameplay, Jump, InputDeviceKind.Gamepad);
        AssertEqual(1, gamepad.Count, "Gamepad Jump 提示数量错误");
        AssertEqual("Gamepad A", gamepad[0].DisplayText, "Gamepad Jump 提示文本错误");
        Assert(gamepad[0].IsBound, "已绑定提示错误标记为未绑定");
        AssertThrows<ArgumentOutOfRangeException>(
            () => prompts.GetPrompts(Gameplay, Jump, InputDeviceKind.Unknown),
            "提示查询接受了 Unknown 设备");
        AssertThrows<InputOperationException>(
            () => prompts.GetPrompts(Gameplay, InputActionId.Create("missing"), InputDeviceKind.Gamepad),
            "提示查询接受了未知 Action");
        AssertThrows<ArgumentException>(
            () => new InputPromptInfo(
                JumpKeyboard,
                Gameplay,
                Jump,
                InputDeviceKind.KeyboardMouse,
                "Space",
                isBound: false),
            "未绑定提示接受了非空显示文本");
    }

    private void VerifyRebindingCapability()
    {
        FakeInputBackend backend = InstallDefaultBackend();
        Assert(_service.TryGetRebinding(out IInputRebinding? rebinding), "支持改键的后端未暴露重绑定能力");
        Assert(ReferenceEquals(backend.Rebinding, rebinding), "InputService 没有返回后端重绑定实例");

        InputBindingInfo info = rebinding!.GetBinding(JumpKeyboard);
        AssertEqual(JumpKeyboard, info.BindingId, "重绑定查询返回了错误 ID");
        AssertEqual("Space", info.CurrentDisplayText, "重绑定查询返回了错误显示文本");

        InputBindingCandidate? candidate = rebinding.CaptureAsync(JumpKeyboard).GetAwaiter().GetResult();
        Assert(candidate != null, "重绑定捕获错误返回 null");
        AssertEqual("J", candidate!.DisplayText, "重绑定捕获返回了错误候选");
        AssertEqual(0, rebinding.FindConflicts(JumpKeyboard, candidate).Count, "无冲突候选返回了冲突");
        rebinding.Apply(JumpKeyboard, candidate);
        AssertEqual(1, backend.Rebinding.ApplyCount, "重绑定没有应用到后端");
        rebinding.RestoreDefault(JumpKeyboard);
        AssertEqual(1, backend.Rebinding.RestoreCount, "重绑定没有恢复默认值");

        Assert(
            _service.TryGetRebindingPersistence(out IInputRebindingPersistence? persistence),
            "支持持久化的后端未暴露绑定持久化能力");
        Assert(ReferenceEquals(backend.Persistence, persistence), "InputService 没有返回后端持久化实例");
        AssertEqual(InputBindingLoadStatus.Loaded, persistence!.LoadAndApply(), "持久化加载状态错误");
        persistence.Save();
        AssertEqual(1, backend.Persistence.LoadCount, "持久化没有调用后端加载");
        AssertEqual(1, backend.Persistence.SaveCount, "持久化没有调用后端保存");
    }

    private void VerifyInstallAndFirstSample()
    {
        FakeInputBackend backend = InstallDefaultBackend();

        Assert(_service.IsReady, "安装后端后 IsReady 为 false");
        AssertEqual(InputDeviceKind.Gamepad, _service.ActiveDevice, "活动设备没有来自后端");
        AssertEqual(
            InputBackendCapabilities.DeviceTracking |
            InputBackendCapabilities.Rebinding |
            InputBackendCapabilities.RebindingPersistence |
            InputBackendCapabilities.PromptQuery,
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

#if DEBUG
    private void VerifyDebugSnapshot()
    {
        FakeInputBackend backend = InstallDefaultBackend();
        backend.SetSample(2, new InputActionSample(new Vector3(0.75f, -0.25f, 0f), pressed: true));
        _service.SetBaseContext(Gameplay);
        _service.PushContext(Pause, InputContextMode.Exclusive);
        _service.PushContext(Debug, InputContextMode.Overlay);
        _service.Update();

        InputDebugSnapshot snapshot = _service.GetDebugSnapshot();
        Assert(snapshot.IsReady, "Debug 快照未标记后端就绪");
        AssertEqual(nameof(FakeInputBackend), snapshot.BackendName, "Debug 快照后端名称错误");
        AssertEqual(InputDeviceKind.Gamepad, snapshot.ActiveDevice, "Debug 快照设备错误");
        Assert(snapshot.HasSample, "Debug 快照未标记首次采样");
        AssertEqual<ulong>(1, snapshot.Sequence, "Debug 快照 Frame 序号错误");
        AssertEqual(3, snapshot.Contexts.Length, "Debug 快照 Context 数量错误");
        Assert(!snapshot.Contexts[0].IsEffective, "Exclusive 下方 Context 错误标记为有效");
        Assert(snapshot.Contexts[1].IsEffective, "Exclusive Context 未标记为有效");
        Assert(snapshot.Contexts[2].IsEffective, "Exclusive 上方 Overlay 未标记为有效");
        AssertEqual(4, snapshot.Actions.Length, "Debug 快照 Action 数量错误");
        AssertEqual(Move, snapshot.Actions[2].Action, "Debug 快照 Action 顺序错误");
        Assert(snapshot.Actions[2].Value.IsEqualApprox(new Vector3(0.75f, -0.25f, 0f)),
            "Debug 快照 Action 值错误");
    }
#endif

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
        var inconsistentCapabilities = FakeInputBackend.CreateDefault();
        inconsistentCapabilities.Capabilities = InputBackendCapabilities.DeviceTracking;
        AssertThrows<InputOperationException>(
            () => _service.InstallBackend(inconsistentCapabilities),
            "允许安装能力标志与接口不一致的后端");
        AssertEqual(1, inconsistentCapabilities.ShutdownCount, "能力不一致的后端没有清理");

        var missingPersistence = new MissingPersistenceBackend();
        AssertThrows<InputOperationException>(
            () => _service.InstallBackend(missingPersistence),
            "允许安装声明持久化能力但没有实现接口的后端");
        AssertEqual(1, missingPersistence.ShutdownCount, "缺少持久化接口的后端没有清理");

        var missingPromptQuery = new MissingPromptQueryBackend();
        AssertThrows<InputOperationException>(
            () => _service.InstallBackend(missingPromptQuery),
            "允许安装声明提示查询能力但没有实现接口的后端");
        AssertEqual(1, missingPromptQuery.ShutdownCount, "缺少提示查询接口的后端没有清理");

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

    private sealed class FakeInputBackend :
        IInputBackend,
        IInputRebindingBackend,
        IInputRebindingPersistenceBackend,
        IInputPromptBackend
    {
        private readonly InputActionDescriptor[] _actions;
        private readonly InputContextId[] _contexts;
        private readonly InputActionSample[] _samples;

        public InputBackendCapabilities Capabilities { get; set; } =
            InputBackendCapabilities.DeviceTracking |
            InputBackendCapabilities.Rebinding |
            InputBackendCapabilities.RebindingPersistence |
            InputBackendCapabilities.PromptQuery;
        public InputDeviceKind ActiveDevice { get; set; } = InputDeviceKind.Gamepad;
        public IReadOnlyList<InputActionDescriptor> Actions => _actions;
        public IReadOnlyList<InputContextId> Contexts => _contexts;
        public List<InputContextId> ActiveContexts { get; } = new();
        public FakeInputRebinding Rebinding { get; } = new();
        IInputRebinding IInputRebindingBackend.Rebinding => Rebinding;
        public FakeInputRebindingPersistence Persistence { get; } = new();
        IInputRebindingPersistence IInputRebindingPersistenceBackend.RebindingPersistence => Persistence;
        public FakeInputPromptQuery PromptQuery { get; } = new();
        IInputPromptQuery IInputPromptBackend.PromptQuery => PromptQuery;
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

    private sealed class FakeInputPromptQuery : IInputPromptQuery
    {
        private static readonly InputPromptInfo KeyboardPrompt = new(
            JumpKeyboard,
            Gameplay,
            Jump,
            InputDeviceKind.KeyboardMouse,
            "Space",
            isBound: true);
        private static readonly InputPromptInfo GamepadPrompt = new(
            InputBindingId.Create("gameplay.jump.gamepad"),
            Gameplay,
            Jump,
            InputDeviceKind.Gamepad,
            "Gamepad A",
            isBound: true);

        public IReadOnlyList<InputPromptInfo> GetPrompts(
            InputContextId context,
            InputActionId action,
            InputDeviceKind device)
        {
            if (context.IsEmpty)
                throw new ArgumentException("输入 Context ID 不能为空。", nameof(context));
            if (action.IsEmpty)
                throw new ArgumentException("输入 Action ID 不能为空。", nameof(action));
            if (!Enum.IsDefined(device) || device == InputDeviceKind.Unknown)
                throw new ArgumentOutOfRangeException(nameof(device));
            if (context != Gameplay)
                throw new InputOperationException($"输入 Context 未注册: {context.Value}");
            if (action != Jump)
                throw new InputOperationException($"输入 Action 未注册: {action.Value}");

            return device == InputDeviceKind.KeyboardMouse
                ? new[] { KeyboardPrompt }
                : device == InputDeviceKind.Gamepad
                    ? new[] { GamepadPrompt }
                    : Array.Empty<InputPromptInfo>();
        }
    }

    private sealed class FakeInputRebinding : IInputRebinding
    {
        private static readonly InputBindingInfo JumpInfo = new(
            JumpKeyboard,
            Gameplay,
            Jump,
            "Jump",
            "Gameplay",
            InputDeviceKind.KeyboardMouse,
            "Space",
            "Space",
            isDefault: true);

        public bool IsCapturing => false;
        public int ApplyCount { get; private set; }
        public int RestoreCount { get; private set; }

        public IReadOnlyList<InputBindingInfo> GetBindings(InputContextId context) =>
            context == Gameplay ? new[] { JumpInfo } : Array.Empty<InputBindingInfo>();

        public InputBindingInfo GetBinding(InputBindingId binding)
        {
            if (binding != JumpKeyboard)
                throw new InputOperationException($"输入 Binding 未注册: {binding.Value}");

            return JumpInfo;
        }

        public Task<InputBindingCandidate?> CaptureAsync(InputBindingId binding)
        {
            _ = GetBinding(binding);
            return Task.FromResult<InputBindingCandidate?>(new FakeCandidate());
        }

        public void CancelCapture()
        {
        }

        public IReadOnlyList<InputBindingInfo> FindConflicts(
            InputBindingId binding,
            InputBindingCandidate candidate)
        {
            _ = GetBinding(binding);
            ArgumentNullException.ThrowIfNull(candidate);
            return Array.Empty<InputBindingInfo>();
        }

        public void Apply(InputBindingId binding, InputBindingCandidate candidate)
        {
            _ = GetBinding(binding);
            if (candidate is not FakeCandidate)
                throw new InputOperationException("候选输入不属于当前后端。");

            ApplyCount++;
        }

        public void RestoreDefault(InputBindingId binding)
        {
            _ = GetBinding(binding);
            RestoreCount++;
        }
    }

    private sealed class FakeInputRebindingPersistence : IInputRebindingPersistence
    {
        public int LoadCount { get; private set; }
        public int SaveCount { get; private set; }

        public InputBindingLoadStatus LoadAndApply()
        {
            LoadCount++;
            return InputBindingLoadStatus.Loaded;
        }

        public void Save() => SaveCount++;
    }

    private sealed class MissingPersistenceBackend : IInputBackend, IInputRebindingBackend
    {
        private static readonly InputActionDescriptor[] ActionDescriptors =
        {
            new(Jump, InputActionValueType.Bool),
        };
        private static readonly InputContextId[] ContextDescriptors = { Gameplay };

        public InputBackendCapabilities Capabilities =>
            InputBackendCapabilities.Rebinding | InputBackendCapabilities.RebindingPersistence;
        public InputDeviceKind ActiveDevice => InputDeviceKind.Unknown;
        public IReadOnlyList<InputActionDescriptor> Actions => ActionDescriptors;
        public IReadOnlyList<InputContextId> Contexts => ContextDescriptors;
        public IInputRebinding Rebinding { get; } = new FakeInputRebinding();
        public int ShutdownCount { get; private set; }

        public void Initialize()
        {
        }

        public void ApplyContexts(ReadOnlySpan<InputContextId> contexts)
        {
        }

        public void Sample(Span<InputActionSample> destination) =>
            destination[0] = new InputActionSample(Vector3.Zero, pressed: false);

        public void Shutdown() => ShutdownCount++;
    }

    private sealed class MissingPromptQueryBackend : IInputBackend
    {
        private static readonly InputActionDescriptor[] ActionDescriptors =
        {
            new(Jump, InputActionValueType.Bool),
        };
        private static readonly InputContextId[] ContextDescriptors = { Gameplay };

        public InputBackendCapabilities Capabilities => InputBackendCapabilities.PromptQuery;
        public InputDeviceKind ActiveDevice => InputDeviceKind.Unknown;
        public IReadOnlyList<InputActionDescriptor> Actions => ActionDescriptors;
        public IReadOnlyList<InputContextId> Contexts => ContextDescriptors;
        public int ShutdownCount { get; private set; }

        public void Initialize()
        {
        }

        public void ApplyContexts(ReadOnlySpan<InputContextId> contexts)
        {
        }

        public void Sample(Span<InputActionSample> destination) =>
            destination[0] = new InputActionSample(Vector3.Zero, pressed: false);

        public void Shutdown() => ShutdownCount++;
    }

    private sealed class FakeCandidate : InputBindingCandidate
    {
        public FakeCandidate()
            : base(InputDeviceKind.KeyboardMouse, "J")
        {
        }
    }
}
