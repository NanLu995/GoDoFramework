using System;
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

    private InputService? _service;
    private Node _guideNode = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            _guideNode = GetNode<Node>("/root/GUIDE");
            _service = Services.Get<IInputService>() as InputService ??
                throw new InvalidOperationException("IInputService 不是 InputService 实例。");

            GuideInputProfile profile = CreateProfile();
            var installer = new GuideInputBackendInstaller { Profile = profile };
            AddChild(installer);

            Assert(_service.IsReady, "Installer 没有安装 GUIDE 后端");
            VerifyGameplayContext();
            VerifyMenuIsolation();
            VerifyLookSampling();
            MeasureBackendAllocations();
            VerifyShutdown();

            GD.Print("[GuideInputBackendRegression] PASS (5/5)");
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
        _service!.Shutdown();
        Assert(!_service.IsReady, "关闭后 InputService 仍处于就绪状态");
        Assert(Guide.GetEnabledMappingContexts().Count == 0, "关闭后 GUIDE Context 没有清空");
    }

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

    private static GuideInputProfile CreateProfile()
    {
        var profile = new GuideInputProfile();
        profile.Actions.Add(CreateAction("gameplay.look", "look.tres"));
        profile.Actions.Add(CreateAction("gameplay.jump", "jump.tres"));
        profile.Actions.Add(CreateAction("ui.confirm", "confirm.tres"));
        profile.Contexts.Add(CreateContext("gameplay", "gameplay_context.tres"));
        profile.Contexts.Add(CreateContext("menu", "menu_context.tres"));
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
