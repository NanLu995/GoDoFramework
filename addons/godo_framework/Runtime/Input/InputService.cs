using System;
using System.Collections.Generic;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>集中采样单个输入后端并维护当前帧快照与 Context 栈。</summary>
public sealed class InputService : IInputService
{
    private readonly Dictionary<InputActionId, int> _actionIndices = new();
    private readonly HashSet<InputContextId> _knownContexts = new();
    private readonly List<ContextEntry> _contextStack = new();
    private IInputBackend? _backend;
    private InputActionSample[] _sampleBuffer = Array.Empty<InputActionSample>();
    private InputActionState[] _states = Array.Empty<InputActionState>();
    private InputDeviceKind _observedDevice;
    private ulong _sequence;
    private bool _hasSample;

    /// <inheritdoc />
    public bool IsReady
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            return _backend != null;
        }
    }

    /// <inheritdoc />
    public InputFrame Frame
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            VerifyReady();
            if (!_hasSample)
                throw new InputOperationException("输入后端尚未完成首次采样。");

            return new InputFrame(this, _sequence);
        }
    }

    /// <inheritdoc />
    public InputDeviceKind ActiveDevice
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            return _backend?.ActiveDevice ?? InputDeviceKind.Unknown;
        }
    }

    /// <inheritdoc />
    public InputBackendCapabilities Capabilities
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            return _backend?.Capabilities ?? InputBackendCapabilities.None;
        }
    }

    /// <inheritdoc />
    public bool TryGetRebinding(out IInputRebinding? rebinding)
    {
        MainThreadGuard.VerifyAccess();
        if (_backend is IInputRebindingBackend rebindingBackend &&
            (_backend.Capabilities & InputBackendCapabilities.Rebinding) != 0)
        {
            rebinding = rebindingBackend.Rebinding;
            return true;
        }

        rebinding = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetRebindingPersistence(out IInputRebindingPersistence? persistence)
    {
        MainThreadGuard.VerifyAccess();
        if (_backend is IInputRebindingPersistenceBackend persistenceBackend &&
            (_backend.Capabilities & InputBackendCapabilities.RebindingPersistence) != 0)
        {
            persistence = persistenceBackend.RebindingPersistence;
            return true;
        }

        persistence = null;
        return false;
    }

    /// <inheritdoc />
    public void SetBaseContext(InputContextId context)
    {
        MainThreadGuard.VerifyAccess();
        VerifyKnownContext(context);

        var proposed = new ContextEntry[] { new(context, InputContextMode.Overlay) };
        ApplyProposedStack(proposed);
    }

    /// <inheritdoc />
    public void PushContext(InputContextId context, InputContextMode mode = InputContextMode.Exclusive)
    {
        MainThreadGuard.VerifyAccess();
        VerifyKnownContext(context);
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (_contextStack.Count == 0)
            throw new InputOperationException("设置 Base Context 后才能压入临时 Context。");

        for (int index = 0; index < _contextStack.Count; index++)
        {
            if (_contextStack[index].Context == context)
                throw new InputOperationException($"输入 Context 已位于栈中: {context.Value}");
        }

        var proposed = new ContextEntry[_contextStack.Count + 1];
        _contextStack.CopyTo(proposed, 0);
        proposed[^1] = new ContextEntry(context, mode);
        ApplyProposedStack(proposed);
    }

    /// <inheritdoc />
    public void PopContext(InputContextId expectedContext)
    {
        MainThreadGuard.VerifyAccess();
        VerifyKnownContext(expectedContext);
        if (_contextStack.Count <= 1)
            throw new InputOperationException("输入 Context 栈中没有可弹出的临时 Context。");

        InputContextId actual = _contextStack[^1].Context;
        if (actual != expectedContext)
        {
            throw new InputOperationException(
                $"输入 Context 弹出顺序错误；期望 {expectedContext.Value}，栈顶为 {actual.Value}");
        }

        var proposed = new ContextEntry[_contextStack.Count - 1];
        _contextStack.CopyTo(0, proposed, 0, proposed.Length);
        ApplyProposedStack(proposed);
    }

    /// <inheritdoc />
    public bool IsContextActive(InputContextId context)
    {
        MainThreadGuard.VerifyAccess();
        VerifyKnownContext(context);
        int start = FindEffectiveStart(_contextStack);
        for (int index = start; index < _contextStack.Count; index++)
        {
            if (_contextStack[index].Context == context)
                return true;
        }

        return false;
    }

    internal void InstallBackend(IInputBackend backend)
    {
        MainThreadGuard.VerifyAccess();
        ArgumentNullException.ThrowIfNull(backend);
        if (_backend != null)
            throw new InputOperationException("输入后端已经安装，第一版不支持运行时替换。");

        try
        {
            backend.Initialize();
            BuildBackendLayout(backend, out Dictionary<InputActionId, int> indices,
                out HashSet<InputContextId> contexts, out InputActionState[] states);

            _actionIndices.Clear();
            foreach ((InputActionId id, int index) in indices)
                _actionIndices.Add(id, index);

            _knownContexts.Clear();
            _knownContexts.UnionWith(contexts);
            _states = states;
            _sampleBuffer = new InputActionSample[states.Length];
            _backend = backend;
            _observedDevice = InputDeviceKind.Unknown;
            _sequence = 0;
            _hasSample = false;
        }
        catch (Exception exception) when (exception is not InputOperationException)
        {
            TryShutdown(backend);
            throw new InputOperationException("输入后端初始化失败。", exception);
        }
        catch
        {
            TryShutdown(backend);
            throw;
        }
    }

    internal void Update()
    {
        MainThreadGuard.VerifyAccess();
        IInputBackend backend = VerifyReady();
        InputDeviceKind activeDevice;

        try
        {
            backend.Sample(_sampleBuffer);
            ValidateSamples(_sampleBuffer);
            activeDevice = backend.ActiveDevice;
            if (!Enum.IsDefined(activeDevice))
                throw new InvalidOperationException($"输入后端返回了无效设备类别: {activeDevice}");
        }
        catch (Exception exception)
        {
            throw new InputOperationException("输入后端采样失败，上一帧状态保持不变。", exception);
        }

        for (int index = 0; index < _states.Length; index++)
        {
            InputActionSample sample = _sampleBuffer[index];
            InputActionState previous = _states[index];
            _states[index] = new InputActionState(
                previous.ValueType,
                sample.Value,
                sample.Pressed,
                justPressed: sample.Pressed && !previous.Pressed,
                justReleased: !sample.Pressed && previous.Pressed);
        }

        _sequence++;
        _hasSample = true;
        if (activeDevice != _observedDevice)
        {
            InputDeviceKind previous = _observedDevice;
            _observedDevice = activeDevice;
            EventChannel.Emit(new InputDeviceChangedEvent(previous, activeDevice));
        }
    }

    internal void Shutdown()
    {
        MainThreadGuard.VerifyAccess();
        if (_backend != null)
            TryShutdown(_backend);

        _backend = null;
        _actionIndices.Clear();
        _knownContexts.Clear();
        _contextStack.Clear();
        _sampleBuffer = Array.Empty<InputActionSample>();
        _states = Array.Empty<InputActionState>();
        _observedDevice = InputDeviceKind.Unknown;
        _sequence = 0;
        _hasSample = false;
    }

#if DEBUG
    /// <summary>返回当前输入后端、Context 栈和 Action 状态的 Debug-only 快照。</summary>
    internal InputDebugSnapshot GetDebugSnapshot()
    {
        MainThreadGuard.VerifyAccess();

        var contexts = new InputDebugContextEntry[_contextStack.Count];
        int effectiveStart = FindEffectiveStart(_contextStack);
        for (int index = 0; index < _contextStack.Count; index++)
        {
            ContextEntry entry = _contextStack[index];
            contexts[index] = new InputDebugContextEntry(
                entry.Context,
                entry.Mode,
                index >= effectiveStart);
        }

        var actions = new InputDebugActionEntry[_states.Length];
        foreach (KeyValuePair<InputActionId, int> item in _actionIndices)
        {
            InputActionState state = _states[item.Value];
            actions[item.Value] = new InputDebugActionEntry(
                item.Key,
                state.ValueType,
                state.Value,
                state.Pressed,
                state.JustPressed,
                state.JustReleased);
        }

        return new InputDebugSnapshot(
            _backend != null,
            _backend?.GetType().Name ?? string.Empty,
            _observedDevice,
            _backend?.Capabilities ?? InputBackendCapabilities.None,
            _hasSample,
            _sequence,
            contexts,
            actions);
    }
#endif

    internal ref readonly InputActionState Resolve(InputActionId action, ulong sequence)
    {
        MainThreadGuard.VerifyAccess();
        if (sequence != _sequence || !_hasSample)
            throw new InputOperationException("InputFrame 已过期，不能跨帧读取。");
        if (action.IsEmpty)
            throw new ArgumentException("输入 Action ID 不能是默认值。", nameof(action));
        if (!_actionIndices.TryGetValue(action, out int index))
            throw new InputOperationException($"输入 Action 未注册: {action.Value}");

        return ref _states[index];
    }

    private void ApplyProposedStack(ReadOnlySpan<ContextEntry> proposed)
    {
        IInputBackend backend = VerifyReady();
        int start = FindEffectiveStart(proposed);
        var effective = new InputContextId[proposed.Length - start];
        for (int index = start; index < proposed.Length; index++)
            effective[index - start] = proposed[index].Context;

        try
        {
            backend.ApplyContexts(effective);
        }
        catch (Exception exception)
        {
            throw new InputOperationException("输入 Context 应用失败，Context 栈保持不变。", exception);
        }

        _contextStack.Clear();
        for (int index = 0; index < proposed.Length; index++)
            _contextStack.Add(proposed[index]);
    }

    private IInputBackend VerifyReady() =>
        _backend ?? throw new InputOperationException("尚未安装输入后端。");

    private void VerifyKnownContext(InputContextId context)
    {
        VerifyReady();
        if (context.IsEmpty)
            throw new ArgumentException("输入 Context ID 不能是默认值。", nameof(context));
        if (!_knownContexts.Contains(context))
            throw new InputOperationException($"输入 Context 未注册: {context.Value}");
    }

    private static int FindEffectiveStart(IReadOnlyList<ContextEntry> stack)
    {
        int start = 0;
        for (int index = 1; index < stack.Count; index++)
        {
            if (stack[index].Mode == InputContextMode.Exclusive)
                start = index;
        }

        return start;
    }

    private static int FindEffectiveStart(ReadOnlySpan<ContextEntry> stack)
    {
        int start = 0;
        for (int index = 1; index < stack.Length; index++)
        {
            if (stack[index].Mode == InputContextMode.Exclusive)
                start = index;
        }

        return start;
    }

    private static void BuildBackendLayout(
        IInputBackend backend,
        out Dictionary<InputActionId, int> indices,
        out HashSet<InputContextId> contexts,
        out InputActionState[] states)
    {
        bool declaresRebinding = (backend.Capabilities & InputBackendCapabilities.Rebinding) != 0;
        bool providesRebinding = backend is IInputRebindingBackend;
        if (declaresRebinding != providesRebinding)
        {
            throw new InputOperationException(
                "输入后端的 Rebinding 能力标志与 IInputRebindingBackend 实现不一致。");
        }

        bool declaresPersistence =
            (backend.Capabilities & InputBackendCapabilities.RebindingPersistence) != 0;
        bool providesPersistence = backend is IInputRebindingPersistenceBackend;
        if (declaresPersistence && !providesPersistence)
        {
            throw new InputOperationException(
                "输入后端声明 RebindingPersistence 时必须实现 IInputRebindingPersistenceBackend。");
        }
        if (declaresPersistence && !declaresRebinding)
        {
            throw new InputOperationException(
                "输入后端声明 RebindingPersistence 时必须同时支持 Rebinding。");
        }

        IReadOnlyList<InputActionDescriptor> actions = backend.Actions ??
            throw new InputOperationException("输入后端返回了 null Action 集合。");
        IReadOnlyList<InputContextId> backendContexts = backend.Contexts ??
            throw new InputOperationException("输入后端返回了 null Context 集合。");

        indices = new Dictionary<InputActionId, int>(actions.Count);
        states = new InputActionState[actions.Count];
        for (int index = 0; index < actions.Count; index++)
        {
            InputActionDescriptor descriptor = actions[index];
            if (descriptor.ActionId.IsEmpty)
                throw new InputOperationException($"输入后端包含默认 Action ID，位置: {index}");
            if (!Enum.IsDefined(descriptor.ValueType))
                throw new InputOperationException($"输入 Action 类型无效: {descriptor.ActionId.Value}");
            if (!indices.TryAdd(descriptor.ActionId, index))
                throw new InputOperationException($"输入后端包含重复 Action: {descriptor.ActionId.Value}");

            states[index] = new InputActionState(descriptor.ValueType, Vector3.Zero, false, false, false);
        }

        contexts = new HashSet<InputContextId>();
        for (int index = 0; index < backendContexts.Count; index++)
        {
            InputContextId context = backendContexts[index];
            if (context.IsEmpty)
                throw new InputOperationException($"输入后端包含默认 Context ID，位置: {index}");
            if (!contexts.Add(context))
                throw new InputOperationException($"输入后端包含重复 Context: {context.Value}");
        }
    }

    private static void ValidateSamples(ReadOnlySpan<InputActionSample> samples)
    {
        for (int index = 0; index < samples.Length; index++)
        {
            Vector3 value = samples[index].Value;
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                throw new InvalidOperationException($"输入后端返回了非有限值，位置: {index}");
        }
    }

    private static void TryShutdown(IInputBackend backend)
    {
        try
        {
            backend.Shutdown();
        }
        catch (Exception exception)
        {
            ErrorHub.Warn($"输入后端关闭失败: {exception}", "Input");
        }
    }

    private readonly struct ContextEntry
    {
        public InputContextId Context { get; }
        public InputContextMode Mode { get; }

        public ContextEntry(InputContextId context, InputContextMode mode)
        {
            Context = context;
            Mode = mode;
        }
    }
}

internal readonly struct InputActionState
{
    public InputActionValueType ValueType { get; }
    public Vector3 Value { get; }
    public bool Pressed { get; }
    public bool JustPressed { get; }
    public bool JustReleased { get; }

    public InputActionState(
        InputActionValueType valueType,
        Vector3 value,
        bool pressed,
        bool justPressed,
        bool justReleased)
    {
        ValueType = valueType;
        Value = value;
        Pressed = pressed;
        JustPressed = justPressed;
        JustReleased = justReleased;
    }
}
