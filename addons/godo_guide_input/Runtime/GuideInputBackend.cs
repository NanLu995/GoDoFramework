using System;
using System.Collections.Generic;
using Godot;
using GuideCs;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>把 G.U.I.D.E Action 和 Mapping Context 适配到 GoDo IInputBackend。</summary>
public sealed class GuideInputBackend :
    IInputBackend,
    IInputRebindingBackend,
    IInputRebindingPersistenceBackend
{
    private const int EmulatedDeviceId = -1;
    private const float PointerMotionThresholdSquared = 1f;
    private const float GamepadAxisThreshold = 0.25f;

    private readonly GuideInputProfile _profile;
    private readonly ISaveService? _saveService;
    private readonly SaveSlot _persistenceSlot;
    private readonly List<GuideAction> _actions = new();
    private readonly Dictionary<InputActionId, GuideAction> _actionsById = new();
    private readonly Dictionary<InputContextId, GuideMappingContext> _contexts = new();
    private readonly List<GuideMappingContext> _activeContexts = new();
    private readonly List<ActionSubscription> _subscriptions = new();
    private InputActionDescriptor[] _actionDescriptors = Array.Empty<InputActionDescriptor>();
    private InputContextId[] _contextIds = Array.Empty<InputContextId>();
    private InputActionSample[] _cachedSamples = Array.Empty<InputActionSample>();
    private GuideInputDeviceTracker? _deviceTracker;
    private GuideInputRebinding? _rebinding;
    private GuideInputRebindingPersistence? _rebindingPersistence;
    private InputDeviceKind _activeDevice;
    private bool _initialized;

    /// <inheritdoc />
    public InputBackendCapabilities Capabilities =>
        InputBackendCapabilities.DeviceTracking |
        InputBackendCapabilities.Rebinding |
        (_saveService == null ? InputBackendCapabilities.None : InputBackendCapabilities.RebindingPersistence);

    /// <inheritdoc />
    public IInputRebinding Rebinding => _rebinding ??
        throw new InputOperationException("GUIDE 重绑定尚未初始化或已经关闭。");

    /// <inheritdoc />
    public IInputRebindingPersistence RebindingPersistence => _rebindingPersistence ??
        throw new InputOperationException("GUIDE 重绑定持久化尚未初始化、未配置或已经关闭。");

    /// <inheritdoc />
    public InputDeviceKind ActiveDevice => _activeDevice;

    /// <inheritdoc />
    public IReadOnlyList<InputActionDescriptor> Actions => _actionDescriptors;

    /// <inheritdoc />
    public IReadOnlyList<InputContextId> Contexts => _contextIds;

    /// <summary>创建使用固定 Profile 的 GUIDE 后端。</summary>
    public GuideInputBackend(GuideInputProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
    }

    /// <summary>创建使用固定 Profile 与可靠绑定存储的 GUIDE 后端。</summary>
    public GuideInputBackend(
        GuideInputProfile profile,
        ISaveService saveService,
        SaveSlot persistenceSlot)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(saveService);
        if (!persistenceSlot.IsValid)
            throw new ArgumentException("输入绑定持久化槽位不能为空。", nameof(persistenceSlot));
        _profile = profile;
        _saveService = saveService;
        _persistenceSlot = persistenceSlot;
    }

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized)
            throw new InvalidOperationException("GuideInputBackend 已经初始化。");

        VerifyAutoloads();
        try
        {
            BuildActions();
            BuildContexts();
            AttachSubscriptions();
            _initialized = true;
            AttachDeviceTracker();
            AttachRebinding();
            AttachRebindingPersistence();
        }
        catch
        {
            _initialized = false;
            DetachRebindingPersistence();
            DetachRebinding();
            DetachDeviceTracker();
            DetachSubscriptions();
            ClearRuntimeState();
            throw;
        }
    }

    /// <inheritdoc />
    public void ApplyContexts(ReadOnlySpan<InputContextId> contexts)
    {
        VerifyInitialized();
        var proposed = new List<GuideMappingContext>(contexts.Length);
        for (int index = 0; index < contexts.Length; index++)
        {
            InputContextId context = contexts[index];
            if (!_contexts.TryGetValue(context, out GuideMappingContext? guideContext))
                throw new InvalidOperationException($"GUIDE 后端不存在 Context: {context.Value}");

            proposed.Add(guideContext);
        }

        try
        {
            Guide.SetEnabledMappingContexts(proposed);
        }
        catch (Exception applyException)
        {
            try
            {
                Guide.SetEnabledMappingContexts(_activeContexts);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "GUIDE Context 应用与回滚均失败，后端映射状态不可确认。",
                    applyException,
                    rollbackException);
            }

            throw;
        }

        _activeContexts.Clear();
        _activeContexts.AddRange(proposed);
    }

    /// <inheritdoc />
    public void Sample(Span<InputActionSample> destination)
    {
        VerifyInitialized();
        if (destination.Length != _actions.Count)
        {
            throw new ArgumentException(
                $"GUIDE 样本缓冲区长度错误；期望 {_actions.Count}，实际 {destination.Length}",
                nameof(destination));
        }

        _cachedSamples.AsSpan().CopyTo(destination);
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        if (!_initialized)
            return;

        try
        {
            Guide.SetEnabledMappingContexts(new List<GuideMappingContext>());
        }
        finally
        {
            DetachRebindingPersistence();
            DetachRebinding();
            DetachDeviceTracker();
            DetachSubscriptions();
            ClearRuntimeState();
            _initialized = false;
        }
    }

    private void BuildActions()
    {
        int count = _profile.Actions.Count;
        var descriptors = new InputActionDescriptor[count];
        var actionIds = new HashSet<InputActionId>();
        var resourceIds = new HashSet<ulong>();

        for (int index = 0; index < count; index++)
        {
            GuideInputActionBinding binding = _profile.Actions[index] ??
                throw new InvalidOperationException($"GuideInputProfile 包含空 Action Binding，位置: {index}");
            InputActionId actionId = InputActionId.Create(binding.ActionId);
            Resource resource = binding.GuideActionResource;
            VerifyResource(resource, "Action", index);
            if (!resource.HasMethod("is_triggered"))
                throw new InvalidOperationException($"GUIDE Action Resource 类型不正确，位置: {index}");
            if (!actionIds.Add(actionId))
                throw new InvalidOperationException($"GuideInputProfile 包含重复 Action ID: {actionId.Value}");
            if (!resourceIds.Add(resource.GetInstanceId()))
                throw new InvalidOperationException($"同一个 GUIDE Action Resource 被重复映射，位置: {index}");

            GuideAction action = Utility.GetCachedOrNew<GuideAction>(resource) ??
                throw new InvalidOperationException($"无法包装 GUIDE Action Resource，位置: {index}");
            InputActionValueType valueType = ConvertValueType(action.ActionValueType, actionId);
            _actions.Add(action);
            _actionsById.Add(actionId, action);
            descriptors[index] = new InputActionDescriptor(actionId, valueType);
        }

        _actionDescriptors = descriptors;
        _cachedSamples = new InputActionSample[count];
    }

    private void BuildContexts()
    {
        int count = _profile.Contexts.Count;
        var contextIds = new InputContextId[count];
        var resourceIds = new HashSet<ulong>();

        for (int index = 0; index < count; index++)
        {
            GuideInputContextBinding binding = _profile.Contexts[index] ??
                throw new InvalidOperationException($"GuideInputProfile 包含空 Context Binding，位置: {index}");
            InputContextId contextId = InputContextId.Create(binding.ContextId);
            Resource resource = binding.GuideContextResource;
            VerifyResource(resource, "Context", index);
            if (!resource.HasSignal("enabled") || !resource.HasSignal("disabled"))
                throw new InvalidOperationException($"GUIDE Context Resource 类型不正确，位置: {index}");
            if (!resourceIds.Add(resource.GetInstanceId()))
                throw new InvalidOperationException($"同一个 GUIDE Context Resource 被重复映射，位置: {index}");

            GuideMappingContext context = Utility.GetCachedOrNew<GuideMappingContext>(resource) ??
                throw new InvalidOperationException($"无法包装 GUIDE Context Resource，位置: {index}");
            if (!_contexts.TryAdd(contextId, context))
                throw new InvalidOperationException($"GuideInputProfile 包含重复 Context ID: {contextId.Value}");

            contextIds[index] = contextId;
        }

        _contextIds = contextIds;
    }

    private static Vector3 ReadValue(
        GuideAction action,
        InputActionValueType valueType,
        bool pressed)
    {
        if (valueType == InputActionValueType.Bool)
            return new Vector3(pressed ? 1f : 0f, 0f, 0f);

        if (valueType == InputActionValueType.Axis2D)
        {
            Vector2 value = action.ValueAxis2d;
            return new Vector3(value.X, value.Y, 0f);
        }

        return valueType switch
        {
            InputActionValueType.Axis1D => new Vector3(action.ValueAxis1d, 0f, 0f),
            InputActionValueType.Axis3D => action.ValueAxis3d,
            _ => throw new ArgumentOutOfRangeException(nameof(valueType)),
        };
    }

    private static InputActionValueType ConvertValueType(
        GuideAction.EGuideActionValueType valueType,
        InputActionId actionId)
    {
        return valueType switch
        {
            GuideAction.EGuideActionValueType.BOOL => InputActionValueType.Bool,
            GuideAction.EGuideActionValueType.AXIS_1D => InputActionValueType.Axis1D,
            GuideAction.EGuideActionValueType.AXIS_2D => InputActionValueType.Axis2D,
            GuideAction.EGuideActionValueType.AXIS_3D => InputActionValueType.Axis3D,
            _ => throw new InvalidOperationException($"GUIDE Action 类型无效: {actionId.Value}"),
        };
    }

    private static void VerifyResource(Resource? resource, string kind, int index)
    {
        if (!GodotObject.IsInstanceValid(resource))
            throw new InvalidOperationException($"GuideInputProfile 缺少 GUIDE {kind} Resource，位置: {index}");
    }

    private static void VerifyAutoloads()
    {
        if (Engine.GetMainLoop() is not SceneTree tree ||
            !GodotObject.IsInstanceValid(tree.Root.GetNodeOrNull<Node>("GUIDE")) ||
            !GodotObject.IsInstanceValid(tree.Root.GetNodeOrNull<Node>("GuideCs")))
        {
            throw new InvalidOperationException(
                "GuideInputBackend 需要已启用且位于 GoDoRuntime 之前的 GUIDE 与 GuideCs Autoload。");
        }
    }

    private void VerifyInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("GuideInputBackend 尚未初始化。");
    }

    private void AttachSubscriptions()
    {
        for (int index = 0; index < _actions.Count; index++)
        {
            var subscription = new ActionSubscription(this, _actions[index], index);
            subscription.Attach();
            _subscriptions.Add(subscription);
        }
    }

    private void DetachSubscriptions()
    {
        for (int index = _subscriptions.Count - 1; index >= 0; index--)
            _subscriptions[index].Detach();

        _subscriptions.Clear();
    }

    private void ClearRuntimeState()
    {
        _activeContexts.Clear();
        _actions.Clear();
        _actionsById.Clear();
        _contexts.Clear();
        _actionDescriptors = Array.Empty<InputActionDescriptor>();
        _contextIds = Array.Empty<InputContextId>();
        _cachedSamples = Array.Empty<InputActionSample>();
        _activeDevice = InputDeviceKind.Unknown;
    }

    internal void ObserveInputEvent(InputEvent inputEvent)
    {
        if (!_initialized || inputEvent.Device == EmulatedDeviceId)
            return;

        switch (inputEvent)
        {
            case InputEventKey key when key.Pressed && !key.Echo:
            case InputEventMouseButton { Pressed: true }:
                _activeDevice = InputDeviceKind.KeyboardMouse;
                break;
            case InputEventMouseMotion mouseMotion
                when mouseMotion.Relative.LengthSquared() >= PointerMotionThresholdSquared:
                _activeDevice = InputDeviceKind.KeyboardMouse;
                break;
            case InputEventJoypadButton joyButton when joyButton.Pressed:
                _activeDevice = joyButton.Device < EmulatedDeviceId
                    ? InputDeviceKind.Touch
                    : InputDeviceKind.Gamepad;
                break;
            case InputEventJoypadMotion joyMotion
                when Mathf.Abs(joyMotion.AxisValue) >= GamepadAxisThreshold:
                _activeDevice = joyMotion.Device < EmulatedDeviceId
                    ? InputDeviceKind.Touch
                    : InputDeviceKind.Gamepad;
                break;
            case InputEventScreenTouch { Pressed: true }:
                _activeDevice = InputDeviceKind.Touch;
                break;
            case InputEventScreenDrag screenDrag
                when screenDrag.ScreenRelative.LengthSquared() >= PointerMotionThresholdSquared:
                _activeDevice = InputDeviceKind.Touch;
                break;
        }
    }

    private void AttachDeviceTracker()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
            throw new InvalidOperationException("GUIDE 设备跟踪需要有效的 SceneTree。");
        Node runtime = tree.Root.GetNodeOrNull<Node>("GoDoRuntime") ??
            throw new InvalidOperationException("GUIDE 设备跟踪需要已就绪的 GoDoRuntime。");
        if (GodotObject.IsInstanceValid(runtime.GetNodeOrNull<Node>(GuideInputDeviceTracker.NodeName)))
            throw new InvalidOperationException("GUIDE 设备跟踪节点已经存在。");

        var tracker = new GuideInputDeviceTracker();
        tracker.Initialize(this);
        runtime.AddChild(tracker);
        _deviceTracker = tracker;
    }

    private void DetachDeviceTracker()
    {
        GuideInputDeviceTracker? tracker = _deviceTracker;
        _deviceTracker = null;
        if (!GodotObject.IsInstanceValid(tracker))
            return;

        tracker!.Stop();
        tracker.QueueFree();
    }

    private void AttachRebinding()
    {
        var rebinding = new GuideInputRebinding(_profile, _actionsById, _contexts);
        rebinding.Initialize();
        _rebinding = rebinding;
    }

    private void DetachRebinding()
    {
        GuideInputRebinding? rebinding = _rebinding;
        _rebinding = null;
        rebinding?.Shutdown();
    }

    private void AttachRebindingPersistence()
    {
        if (_saveService == null)
            return;

        GuideInputRebinding rebinding = _rebinding ??
            throw new InvalidOperationException("GUIDE 重绑定必须先于持久化初始化。");
        _rebindingPersistence = new GuideInputRebindingPersistence(
            rebinding,
            _saveService,
            _persistenceSlot);
    }

    private void DetachRebindingPersistence() => _rebindingPersistence = null;

    private void OnTriggered(int index)
    {
        GuideAction action = _actions[index];
        Vector3 value = ReadValue(action, _actionDescriptors[index].ValueType, pressed: true);
        _cachedSamples[index] = new InputActionSample(value, pressed: true);
    }

    private void OnOngoing(int index)
    {
        GuideAction action = _actions[index];
        Vector3 value = ReadValue(action, _actionDescriptors[index].ValueType, pressed: false);
        _cachedSamples[index] = new InputActionSample(value, pressed: false);
    }

    private void OnCompleted(int index)
    {
        GuideAction action = _actions[index];
        Vector3 value = ReadValue(action, _actionDescriptors[index].ValueType, pressed: false);
        _cachedSamples[index] = new InputActionSample(value, pressed: false);
    }

    private sealed class ActionSubscription
    {
        private readonly GuideInputBackend _owner;
        private readonly GuideAction _action;
        private readonly int _index;
        private bool _triggeredAttached;
        private bool _ongoingAttached;
        private bool _completedAttached;

        public ActionSubscription(GuideInputBackend owner, GuideAction action, int index)
        {
            _owner = owner;
            _action = action;
            _index = index;
        }

        public void Attach()
        {
            if (_triggeredAttached || _ongoingAttached || _completedAttached)
                return;

            try
            {
                _action.Triggered += OnTriggered;
                _triggeredAttached = true;
                _action.Ongoing += OnOngoing;
                _ongoingAttached = true;
                _action.Completed += OnCompleted;
                _completedAttached = true;
            }
            catch
            {
                Detach();
                throw;
            }
        }

        public void Detach()
        {
            if (_completedAttached)
            {
                _action.Completed -= OnCompleted;
                _completedAttached = false;
            }
            if (_ongoingAttached)
            {
                _action.Ongoing -= OnOngoing;
                _ongoingAttached = false;
            }
            if (_triggeredAttached)
            {
                _action.Triggered -= OnTriggered;
                _triggeredAttached = false;
            }
        }

        private void OnTriggered() => _owner.OnTriggered(_index);

        private void OnOngoing() => _owner.OnOngoing(_index);

        private void OnCompleted() => _owner.OnCompleted(_index);
    }
}
