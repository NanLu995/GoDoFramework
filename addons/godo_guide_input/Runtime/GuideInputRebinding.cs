using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GuideCs;
using GuideInputValue = GuideCs.GuideInput;

#nullable enable

namespace GoDo.GuideInput;

/// <summary>使用 GUIDE Remapper 与 InputDetector 实现 GoDo 运行时重绑定边界。</summary>
internal sealed class GuideInputRebinding : IInputRebinding
{
    internal const string DetectorNodeName = "GoDoGuideInputDetector";

    private readonly GuideInputProfile _profile;
    private readonly IReadOnlyDictionary<InputActionId, GuideAction> _actions;
    private readonly IReadOnlyDictionary<InputContextId, GuideMappingContext> _contexts;
    private readonly Dictionary<InputBindingId, BindingEntry> _bindings = new();
    private readonly Dictionary<InputContextId, List<BindingEntry>> _bindingsByContext = new();
    private readonly GuideRemapper _remapper = new();
    private readonly List<GuideMappingContext> _guideContexts = new();
    private GuideInputDetector? _detector;
    private TaskCompletionSource<InputBindingCandidate?>? _captureCompletion;
    private bool _detectorConfigured;
    private bool _initialized;

    /// <inheritdoc />
    public bool IsCapturing
    {
        get
        {
            MainThreadGuard.VerifyAccess();
            return _captureCompletion != null;
        }
    }

    internal GuideInputRebinding(
        GuideInputProfile profile,
        IReadOnlyDictionary<InputActionId, GuideAction> actions,
        IReadOnlyDictionary<InputContextId, GuideMappingContext> contexts)
    {
        _profile = profile;
        _actions = actions;
        _contexts = contexts;
    }

    internal void Initialize()
    {
        if (_initialized)
            throw new InvalidOperationException("GuideInputRebinding 已经初始化。");

        try
        {
            _guideContexts.Clear();
            foreach (GuideMappingContext context in _contexts.Values)
                _guideContexts.Add(context);

            _remapper.Initialize(_guideContexts, new GuideRemappingConfig());
            BuildBindings();
            AttachDetector();
            _initialized = true;
        }
        catch
        {
            GuideInputDetector? detector = _detector;
            _detector = null;
            _detectorConfigured = false;
            if (GodotObject.IsInstanceValid(detector))
                detector!.QueueFree();
            _bindings.Clear();
            _bindingsByContext.Clear();
            _guideContexts.Clear();
            throw;
        }
    }

    internal void Shutdown()
    {
        if (!_initialized)
            return;

        CancelCapture();
        Guide.SetRemappingConfig(new GuideRemappingConfig());
        GuideInputDetector? detector = _detector;
        _detector = null;
        _detectorConfigured = false;
        if (GodotObject.IsInstanceValid(detector))
            detector!.QueueFree();

        _bindings.Clear();
        _bindingsByContext.Clear();
        _guideContexts.Clear();
        _initialized = false;
    }

    /// <inheritdoc />
    public IReadOnlyList<InputBindingInfo> GetBindings(InputContextId context)
    {
        VerifyReady();
        if (context.IsEmpty)
            throw new ArgumentException("输入 Context ID 不能是默认值。", nameof(context));
        if (!_contexts.ContainsKey(context))
            throw new InputOperationException($"输入 Context 未注册: {context.Value}");
        if (!_bindingsByContext.TryGetValue(context, out List<BindingEntry>? entries))
            return Array.Empty<InputBindingInfo>();

        var result = new InputBindingInfo[entries.Count];
        for (int index = 0; index < entries.Count; index++)
            result[index] = CreateInfo(entries[index]);

        return result;
    }

    /// <inheritdoc />
    public InputBindingInfo GetBinding(InputBindingId binding) => CreateInfo(Resolve(binding));

    /// <inheritdoc />
    public Task<InputBindingCandidate?> CaptureAsync(InputBindingId binding)
    {
        BindingEntry entry = Resolve(binding);
        if (_captureCompletion != null)
            throw new InputOperationException("已有输入重绑定捕获正在进行。");
        if (!GodotObject.IsInstanceValid(_detector?.BaseGuideDetector))
            throw new InputOperationException("GUIDE 输入捕获节点尚未就绪，请在下一帧重试。");

        ConfigureDetector();
        _captureCompletion = new TaskCompletionSource<InputBindingCandidate?>();
        _detector!.InputDetected += OnInputDetected;
        try
        {
            _detector.Detect(entry.Item.ValueType);
            return _captureCompletion.Task;
        }
        catch (Exception exception)
        {
            TaskCompletionSource<InputBindingCandidate?> completion = _captureCompletion;
            _captureCompletion = null;
            _detector.InputDetected -= OnInputDetected;
            completion.SetException(new InputOperationException("GUIDE 输入捕获启动失败。", exception));
            return completion.Task;
        }
    }

    /// <inheritdoc />
    public void CancelCapture()
    {
        VerifyReady();
        if (_captureCompletion == null)
            return;

        try
        {
            _detector?.AbortDetection();
        }
        finally
        {
            CompleteCapture(null);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<InputBindingInfo> FindConflicts(
        InputBindingId binding,
        InputBindingCandidate candidate)
    {
        BindingEntry entry = Resolve(binding);
        GuideInputCandidate guideCandidate = ResolveCandidate(candidate);
        List<ConfigItem> conflicts = _remapper.GetInputCollisions(entry.Item, guideCandidate.Input);
        if (conflicts.Count == 0)
            return Array.Empty<InputBindingInfo>();

        var result = new List<InputBindingInfo>(conflicts.Count);
        for (int index = 0; index < conflicts.Count; index++)
        {
            if (TryFindEntry(conflicts[index], out BindingEntry conflict))
                result.Add(CreateInfo(conflict));
        }

        return result.ToArray();
    }

    /// <inheritdoc />
    public void Apply(InputBindingId binding, InputBindingCandidate candidate)
    {
        BindingEntry entry = Resolve(binding);
        GuideInputCandidate guideCandidate = ResolveCandidate(candidate);
        ApplyChange(() => _remapper.SetBoundInput(entry.Item, guideCandidate.Input));
    }

    /// <inheritdoc />
    public void RestoreDefault(InputBindingId binding)
    {
        BindingEntry entry = Resolve(binding);
        ApplyChange(() => _remapper.RestoreDefaultFor(entry.Item));
    }

    private void BuildBindings()
    {
        var targets = new HashSet<BindingTarget>();
        for (int index = 0; index < _profile.Bindings.Count; index++)
        {
            GuideInputBindingDefinition definition = _profile.Bindings[index] ??
                throw new InvalidOperationException($"GuideInputProfile 包含空 Binding Definition，位置: {index}");
            InputBindingId bindingId = InputBindingId.Create(definition.BindingId);
            InputContextId contextId = InputContextId.Create(definition.ContextId);
            InputActionId actionId = InputActionId.Create(definition.ActionId);
            if (definition.MappingIndex < 0)
                throw new InvalidOperationException($"GUIDE Binding MappingIndex 不能为负数: {bindingId.Value}");
            if (!_contexts.TryGetValue(contextId, out GuideMappingContext? context))
                throw new InvalidOperationException($"GUIDE Binding 引用了未知 Context: {contextId.Value}");
            if (!_actions.TryGetValue(actionId, out GuideAction? action))
                throw new InvalidOperationException($"GUIDE Binding 引用了未知 Action: {actionId.Value}");
            if (_bindings.ContainsKey(bindingId))
                throw new InvalidOperationException($"GuideInputProfile 包含重复 Binding ID: {bindingId.Value}");

            var target = new BindingTarget(contextId, actionId, definition.MappingIndex);
            if (!targets.Add(target))
                throw new InvalidOperationException($"多个 Binding ID 指向同一个 GUIDE 输入槽位: {bindingId.Value}");

            ConfigItem? item = FindConfigItem(context, action, definition.MappingIndex);
            if (item == null)
            {
                throw new InvalidOperationException(
                    $"GUIDE Binding 目标不存在或不可重绑定: {bindingId.Value}");
            }

            var entry = new BindingEntry(bindingId, contextId, actionId, item);
            _bindings.Add(bindingId, entry);
            if (!_bindingsByContext.TryGetValue(contextId, out List<BindingEntry>? contextBindings))
            {
                contextBindings = new List<BindingEntry>();
                _bindingsByContext.Add(contextId, contextBindings);
            }
            contextBindings.Add(entry);
        }

        List<ConfigItem> remappableItems = _remapper.GetRemappableItems();
        for (int index = 0; index < remappableItems.Count; index++)
        {
            if (!TryFindEntry(remappableItems[index], out _))
            {
                ConfigItem item = remappableItems[index];
                throw new InvalidOperationException(
                    $"GUIDE 可重绑定槽位缺少稳定 Binding ID: {item.Context.DisplayName} / {item.Action.DisplayName} / {item.Index}");
            }
        }
    }

    private ConfigItem? FindConfigItem(GuideMappingContext context, GuideAction action, int mappingIndex)
    {
        List<ConfigItem> items = _remapper.GetRemappableItems(context, action: action);
        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].Index == mappingIndex)
                return items[index];
        }

        return null;
    }

    private InputBindingInfo CreateInfo(BindingEntry entry)
    {
        GuideInputValue? current = _remapper.GetBoundInputOrNull(entry.Item);
        GuideInputValue? defaultInput = _remapper.GetDefaultInput(entry.Item);
        string currentText = current == null ? string.Empty : FormatInput(current);
        string defaultText = defaultInput == null ? string.Empty : FormatInput(defaultInput);
        bool isDefault = current == null
            ? defaultInput == null
            : defaultInput != null && current.IsSameAs(defaultInput);
        string displayName = string.IsNullOrWhiteSpace(entry.Item.DisplayName)
            ? entry.ActionId.Value
            : entry.Item.DisplayName;

        return new InputBindingInfo(
            entry.BindingId,
            entry.ContextId,
            entry.ActionId,
            displayName,
            entry.Item.DisplayCategory ?? string.Empty,
            current == null ? InputDeviceKind.Unknown : ConvertDevice(current.DeviceType),
            currentText,
            defaultText,
            isDefault);
    }

    private void ApplyChange(Action change)
    {
        GuideRemappingConfig previousConfig = _remapper.GetMappingConfig();
        List<GuideMappingContext> activeContexts = Guide.GetEnabledMappingContexts();
        try
        {
            change();
            ApplyGuideConfig(_remapper.GetMappingConfig(), activeContexts);
        }
        catch (Exception applyException)
        {
            try
            {
                _remapper.Initialize(_guideContexts, previousConfig);
                ApplyGuideConfig(previousConfig, activeContexts);
            }
            catch (Exception rollbackException)
            {
                throw new InputOperationException(
                    "GUIDE 输入绑定应用与回滚均失败，当前映射状态不可确认。",
                    new AggregateException(applyException, rollbackException));
            }

            throw new InputOperationException("GUIDE 输入绑定应用失败，已恢复原绑定。", applyException);
        }
    }

    private static void ApplyGuideConfig(
        GuideRemappingConfig config,
        List<GuideMappingContext> activeContexts)
    {
        Guide.SetEnabledMappingContexts(new List<GuideMappingContext>());
        Guide.SetRemappingConfig(config);
        Guide.SetEnabledMappingContexts(activeContexts);
    }

    private BindingEntry Resolve(InputBindingId binding)
    {
        VerifyReady();
        if (binding.IsEmpty)
            throw new ArgumentException("输入 Binding ID 不能是默认值。", nameof(binding));
        if (!_bindings.TryGetValue(binding, out BindingEntry? entry))
            throw new InputOperationException($"输入 Binding 未注册: {binding.Value}");

        return entry;
    }

    private GuideInputCandidate ResolveCandidate(InputBindingCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate is not GuideInputCandidate guideCandidate || !ReferenceEquals(guideCandidate.Owner, this))
            throw new InputOperationException("候选输入不属于当前 GUIDE 后端。");

        return guideCandidate;
    }

    private bool TryFindEntry(ConfigItem item, out BindingEntry entry)
    {
        foreach (BindingEntry candidate in _bindings.Values)
        {
            if (candidate.Item.IsSameAs(item))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null!;
        return false;
    }

    private void AttachDetector()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
            throw new InvalidOperationException("GUIDE 输入捕获需要有效的 SceneTree。");
        Node runtime = tree.Root.GetNodeOrNull<Node>("GoDoRuntime") ??
            throw new InvalidOperationException("GUIDE 输入捕获需要已就绪的 GoDoRuntime。");
        if (GodotObject.IsInstanceValid(runtime.GetNodeOrNull<Node>(DetectorNodeName)))
            throw new InvalidOperationException("GUIDE 输入捕获节点已经存在。");

        var detector = new GuideInputDetector
        {
            Name = DetectorNodeName,
            ProcessMode = Node.ProcessModeEnum.Always,
        };
        runtime.AddChild(detector);
        _detector = detector;
    }

    private void ConfigureDetector()
    {
        if (_detectorConfigured)
            return;

        var escape = new GuideInputKey
        {
            Key = Key.Escape,
            AllowAdditionalModifiers = true,
        };
        _detector!.DetectionCountdownSeconds = 0.2f;
        _detector.MinimumAxisAmplitude = 0.5f;
        _detector.UseJoyIndex = GuideInputDetector.EJoyIndex.ANY;
        _detector.SetAbortDetectionOn(new List<GuideInputValue> { escape });
        _detectorConfigured = true;
    }

    private void OnInputDetected(object? sender, GuideInputValue input)
    {
        InputBindingCandidate? candidate = input == null
            ? null
            : new GuideInputCandidate(this, input, ConvertDevice(input.DeviceType), FormatInput(input));
        CompleteCapture(candidate);
    }

    private void CompleteCapture(InputBindingCandidate? candidate)
    {
        TaskCompletionSource<InputBindingCandidate?>? completion = _captureCompletion;
        if (completion == null)
            return;

        _captureCompletion = null;
        if (GodotObject.IsInstanceValid(_detector))
            _detector!.InputDetected -= OnInputDetected;
        completion.SetResult(candidate);
    }

    private static InputDeviceKind ConvertDevice(GuideInputValue.EDeviceType device)
    {
        int flags = (int)device;
        if ((flags & (int)GuideInputValue.EDeviceType.TOUCH) != 0)
            return InputDeviceKind.Touch;
        if ((flags & (int)GuideInputValue.EDeviceType.JOY) != 0)
            return InputDeviceKind.Gamepad;
        if ((flags & ((int)GuideInputValue.EDeviceType.KEYBOARD | (int)GuideInputValue.EDeviceType.MOUSE)) != 0)
            return InputDeviceKind.KeyboardMouse;

        throw new InputOperationException($"GUIDE 返回了不支持的候选设备类型: {device}");
    }

    private static string FormatInput(GuideInputValue input)
    {
        return input switch
        {
            GuideInputKey key => FormatKey(key),
            GuideInputMouseButton mouse => $"Mouse {mouse.Button}",
            GuideInputJoyButton joy => $"Gamepad {joy.Button}",
            GuideInputJoyAxis1D joyAxis => $"Gamepad Axis {joyAxis.Axis}",
            GuideInputJoyAxis2D joyAxes => $"Gamepad Axes {joyAxes.X}/{joyAxes.Y}",
            GuideInputMouseAxis1D => "Mouse Axis",
            GuideInputMouseAxis2D => "Mouse Motion",
            _ => input.GetType().Name.Replace("GuideInput", string.Empty, StringComparison.Ordinal),
        };
    }

    private static string FormatKey(GuideInputKey key)
    {
        string modifiers = string.Empty;
        if (key.Control)
            modifiers += "Ctrl+";
        if (key.Alt)
            modifiers += "Alt+";
        if (key.Shift)
            modifiers += "Shift+";
        if (key.Meta)
            modifiers += "Meta+";

        return modifiers + key.Key;
    }

    private void VerifyReady()
    {
        MainThreadGuard.VerifyAccess();
        if (!_initialized)
            throw new InputOperationException("GUIDE 重绑定尚未初始化或已经关闭。");
    }

    private sealed class BindingEntry
    {
        public InputBindingId BindingId { get; }
        public InputContextId ContextId { get; }
        public InputActionId ActionId { get; }
        public ConfigItem Item { get; }

        public BindingEntry(
            InputBindingId bindingId,
            InputContextId contextId,
            InputActionId actionId,
            ConfigItem item)
        {
            BindingId = bindingId;
            ContextId = contextId;
            ActionId = actionId;
            Item = item;
        }
    }

    private readonly record struct BindingTarget(
        InputContextId ContextId,
        InputActionId ActionId,
        int MappingIndex);

    private sealed class GuideInputCandidate : InputBindingCandidate
    {
        internal GuideInputRebinding Owner { get; }
        internal GuideInputValue Input { get; }

        internal GuideInputCandidate(
            GuideInputRebinding owner,
            GuideInputValue input,
            InputDeviceKind device,
            string displayText)
            : base(device, displayText)
        {
            Owner = owner;
            Input = input;
        }
    }
}
