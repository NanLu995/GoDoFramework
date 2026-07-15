using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GoDo;
using GoDo.GuideInput;
using GuideCs;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>GoDo GUIDE 输入适配包的安装、Context 与采样回归入口。</summary>
public sealed partial class GuideInputBackendRegression : Node
{
    private const string FixtureRoot = "res://Verification/Automated/Fixtures/GuideInput";
    private static readonly InputActionId Look = InputActionId.Create("gameplay.look");
    private static readonly InputActionId Jump = InputActionId.Create("gameplay.jump");
    private static readonly InputActionId Confirm = InputActionId.Create("ui.confirm");
    private static readonly InputContextId Gameplay = InputContextId.Create("gameplay");
    private static readonly InputContextId Menu = InputContextId.Create("menu");
    private static readonly InputBindingId JumpKeyboard = InputBindingId.Create("gameplay.jump.keyboard");
    private static readonly InputBindingId ConfirmKeyboard = InputBindingId.Create("ui.confirm.keyboard");

    private InputService? _service;
    private Node _guideNode = null!;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            _guideNode = GetNode<Node>("/root/GUIDE");
            _service = Services.Get<IInputService>() as InputService ??
                throw new InvalidOperationException("IInputService 不是 InputService 实例。");

            GuideInputProfile profile = CreateProfile();
            var installer = new GuideInputBackendInstaller { Profile = profile };
            AddChild(installer);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Assert(_service.IsReady, "Installer 没有安装 GUIDE 后端");
            VerifyGameplayContext();
            await AdvanceInputFrames();
            Assert(!_service.Frame.Pressed(Jump), "Jump 释放后缓存仍处于按下状态");
            VerifyMenuIsolation();
            await AdvanceInputFrames();
            VerifyLookSampling();
            VerifyDeviceTracking();
            await VerifyRebinding();
            MeasureBackendAllocations();
            VerifyShutdown();

            GD.Print("[GuideInputBackendRegression] PASS (7/7)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            _service?.Shutdown();
            GD.PushError($"[GuideInputBackendRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void VerifyGameplayContext()
    {
        _service!.SetBaseContext(Gameplay);
        InjectKey(Key.Space, pressed: true);
        EvaluateAndSample();

        InputFrame frame = _service.Frame;
        Assert(frame.Pressed(Jump), "Gameplay Context 没有输出 Jump");
        Assert(frame.JustPressed(Jump), "Jump 首次触发没有进入 JustPressed");
        Assert(!frame.Pressed(Confirm), "Gameplay Context 错误输出 Confirm");

        InjectKey(Key.Space, pressed: false);
        EvaluateAndSample();
    }

    private void VerifyMenuIsolation()
    {
        _service!.SetBaseContext(Menu);
        InjectKey(Key.Space, pressed: true);
        EvaluateAndSample();

        InputFrame frame = _service.Frame;
        Assert(frame.Pressed(Confirm), "Menu Context 没有输出 Confirm");
        Assert(!frame.Pressed(Jump), "切换 Menu 后 Gameplay Jump 仍被触发");

        InjectKey(Key.Space, pressed: false);
        EvaluateAndSample();
    }

    private void VerifyLookSampling()
    {
        _service!.SetBaseContext(Gameplay);
        Guide.InjectInput(new InputEventMouseMotion { Relative = new Vector2(6f, -3f) });
        EvaluateAndSample();

        Assert(
            _service.Frame.Axis2(Look).IsEqualApprox(new Vector2(6f, -3f)),
            "GUIDE Look 没有通过 InputFrame 输出");
    }

    private void VerifyShutdown()
    {
        GuideInputDeviceTracker tracker = GetNode<GuideInputDeviceTracker>(
            $"/root/GoDoRuntime/{GuideInputDeviceTracker.NodeName}");
        _service!.Shutdown();
        Assert(!_service.IsReady, "关闭后 InputService 仍处于就绪状态");
        Assert(Guide.GetEnabledMappingContexts().Count == 0, "关闭后 GUIDE Context 没有清空");
        Assert(tracker.IsQueuedForDeletion(), "关闭后设备跟踪节点没有进入释放队列");
    }

    private void VerifyDeviceTracking()
    {
        Assert(
            (_service!.Capabilities & InputBackendCapabilities.DeviceTracking) != 0,
            "GUIDE 后端没有声明 DeviceTracking 能力");
        GuideInputDeviceTracker tracker = GetNode<GuideInputDeviceTracker>(
            $"/root/GoDoRuntime/{GuideInputDeviceTracker.NodeName}");

        tracker._Input(new InputEventJoypadMotion { Device = 0, AxisValue = 0.24f });
        _service.Update();
        Assert(_service.ActiveDevice == InputDeviceKind.Unknown, "摇杆噪声错误切换到手柄");

        tracker._Input(new InputEventJoypadMotion { Device = 0, AxisValue = 0.25f });
        _service.Update();
        Assert(_service.ActiveDevice == InputDeviceKind.Gamepad, "有效摇杆输入没有切换到手柄");

        tracker._Input(new InputEventMouseButton { Device = -1, Pressed = true });
        _service.Update();
        Assert(_service.ActiveDevice == InputDeviceKind.Gamepad, "模拟鼠标事件错误覆盖了实体设备");

        tracker._Input(new InputEventKey { Pressed = true, Echo = false });
        _service.Update();
        Assert(_service.ActiveDevice == InputDeviceKind.KeyboardMouse, "键盘输入没有切换设备");

        tracker._Input(new InputEventJoypadButton { Device = -2, Pressed = true });
        _service.Update();
        Assert(_service.ActiveDevice == InputDeviceKind.Touch, "虚拟摇杆没有归类为触摸");
    }

    private async Task VerifyRebinding()
    {
        Assert(
            (_service!.Capabilities & InputBackendCapabilities.Rebinding) != 0,
            "GUIDE 后端没有声明 Rebinding 能力");
        Assert(_service.TryGetRebinding(out IInputRebinding? rebinding), "GUIDE 后端没有提供重绑定接口");

        InputBindingInfo initial = rebinding!.GetBinding(JumpKeyboard);
        Assert(initial.CurrentDisplayText == "Space", "Jump 默认绑定显示错误");
        Assert(initial.IsDefault, "Jump 初始绑定没有标记为默认值");
        Assert(rebinding.GetBindings(Gameplay).Count == 1, "Gameplay 重绑定槽位数量错误");

        InputBindingCandidate space = await CaptureKey(rebinding, JumpKeyboard, Key.Space);
        IReadOnlyList<InputBindingInfo> conflicts = rebinding.FindConflicts(JumpKeyboard, space);
        Assert(conflicts.Count == 1, "Space 冲突数量错误");
        Assert(conflicts[0].BindingId == ConfirmKeyboard, "Space 没有命中 Menu Confirm 冲突");

        InputBindingCandidate keyJ = await CaptureKey(rebinding, JumpKeyboard, Key.J);
        Assert(rebinding.FindConflicts(JumpKeyboard, keyJ).Count == 0, "J 错误报告冲突");
        rebinding.Apply(JumpKeyboard, keyJ);
        Assert(rebinding.GetBinding(JumpKeyboard).CurrentDisplayText == "J", "应用后绑定查询没有更新");

        _service.SetBaseContext(Gameplay);
        _service.Update();
        Assert(!_service.Frame.Pressed(Jump), "改绑测试前 Jump 缓存仍处于按下状态");
        InjectKey(Key.Space, pressed: true);
        EvaluateAndSample();
        Assert(!_service.Frame.Pressed(Jump), "改绑后 Space 仍触发 Jump");
        InjectKey(Key.Space, pressed: false);
        EvaluateAndSample();
        await AdvanceInputFrames();
        InjectKey(Key.J, pressed: true);
        EvaluateAndSample();
        Assert(_service.Frame.Pressed(Jump), "改绑后 J 没有触发 Jump");
        InjectKey(Key.J, pressed: false);
        EvaluateAndSample();
        await AdvanceInputFrames();

        rebinding.RestoreDefault(JumpKeyboard);
        Assert(rebinding.GetBinding(JumpKeyboard).IsDefault, "恢复后 Jump 不是默认绑定");

        Task<InputBindingCandidate?> cancelled = rebinding.CaptureAsync(JumpKeyboard);
        Assert(rebinding.IsCapturing, "捕获开始后 IsCapturing 为 false");
        rebinding.CancelCapture();
        Assert(await cancelled == null, "取消捕获没有返回 null");
        Assert(!rebinding.IsCapturing, "取消后 IsCapturing 仍为 true");
    }

    private Task<InputBindingCandidate> CaptureKey(
        IInputRebinding rebinding,
        InputBindingId binding,
        Key key)
    {
        Task<InputBindingCandidate?> capture = rebinding.CaptureAsync(binding);
        GuideInputDetector detector = GetNode<GuideInputDetector>(
            $"/root/GoDoRuntime/{GuideInputRebinding.DetectorNodeName}");
        detector.AbortDetection();
        var detected = new GuideInputKey
        {
            Key = key,
            AllowAdditionalModifiers = true,
        };
        detector.BaseGuideDetector.EmitSignal("input_detected", detected.BaseGuideObject);

        return AwaitCandidate(capture);
    }

    private static async Task<InputBindingCandidate> AwaitCandidate(Task<InputBindingCandidate?> capture) =>
        await capture ?? throw new InvalidOperationException("模拟输入捕获错误返回 null。");

    private void MeasureBackendAllocations()
    {
        for (int index = 0; index < 10; index++)
            _service!.Update();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 1_000; index++)
            _service!.Update();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GD.Print($"[GuideInputBackendRegression] PERF: 3 Actions x 1000 samples allocated {allocated} bytes");
    }

    private void EvaluateAndSample()
    {
        _guideNode.Call("_process", 1.0 / 60.0);
        _service!.Update();
    }

    private async Task AdvanceInputFrames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static GuideInputProfile CreateProfile()
    {
        var profile = new GuideInputProfile();
        profile.Actions.Add(CreateAction("gameplay.look", "look.tres"));
        profile.Actions.Add(CreateAction("gameplay.jump", "jump.tres"));
        profile.Actions.Add(CreateAction("ui.confirm", "confirm.tres"));
        profile.Contexts.Add(CreateContext("gameplay", "gameplay_context.tres"));
        profile.Contexts.Add(CreateContext("menu", "menu_context.tres"));
        profile.Bindings.Add(CreateBinding("gameplay.jump.keyboard", "gameplay", "gameplay.jump", 0));
        profile.Bindings.Add(CreateBinding("ui.confirm.keyboard", "menu", "ui.confirm", 0));
        return profile;
    }

    private static GuideInputActionBinding CreateAction(string id, string fileName) => new()
    {
        ActionId = id,
        GuideActionResource = LoadResource(fileName),
    };

    private static GuideInputContextBinding CreateContext(string id, string fileName) => new()
    {
        ContextId = id,
        GuideContextResource = LoadResource(fileName),
    };

    private static GuideInputBindingDefinition CreateBinding(
        string bindingId,
        string contextId,
        string actionId,
        int mappingIndex) => new()
    {
        BindingId = bindingId,
        ContextId = contextId,
        ActionId = actionId,
        MappingIndex = mappingIndex,
    };

    private static Resource LoadResource(string fileName) =>
        ResourceLoader.Load<Resource>($"{FixtureRoot}/{fileName}") ??
        throw new InvalidOperationException($"无法加载 GUIDE 回归资源: {fileName}");

    private static void InjectKey(Key key, bool pressed)
    {
        Guide.InjectInput(new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key,
            Pressed = pressed,
        });
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
