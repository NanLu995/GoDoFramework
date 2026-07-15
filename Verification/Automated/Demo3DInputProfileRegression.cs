using System;
using Godot;
using GoDo;
using GoDo.GuideInput;
using GuideCs;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>验证 Demo3D 的真实 GUIDE Profile、语义映射与 Context 隔离。</summary>
public sealed partial class Demo3DInputProfileRegression : Node
{
    private const string ProfilePath = "res://Templates/Demo3D/Input/Demo3DInputProfile.tres";

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

            GuideInputProfile profile = ResourceLoader.Load<GuideInputProfile>(ProfilePath) ??
                throw new InvalidOperationException($"无法加载 Demo3D 输入 Profile: {ProfilePath}");
            AddChild(new GuideInputBackendInstaller { Profile = profile });

            VerifyProfileInstallation();
            VerifyKeyboardMovement();
            VerifyMouseLookAndJump();
            VerifyResultIsolation();

            _service.Shutdown();
            GD.Print("[Demo3DInputProfileRegression] PASS (4/4)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            _service?.Shutdown();
            GD.PushError($"[Demo3DInputProfileRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void VerifyProfileInstallation()
    {
        Assert(_service!.IsReady, "Demo3D Profile 没有安装 GUIDE 后端");
        _service.SetBaseContext(Demo3D.Demo3DInput.Gameplay);
        Assert(_service.IsContextActive(Demo3D.Demo3DInput.Gameplay), "Gameplay Context 没有生效");
    }

    private void VerifyKeyboardMovement()
    {
        InjectKey(Key.W, pressed: true);
        InjectKey(Key.D, pressed: true);
        EvaluateAndSample();

        Assert(
            _service!.Frame.Axis2(Demo3D.Demo3DInput.Move).IsEqualApprox(new Vector2(1f, -1f)),
            "W+D 没有输出预期的 Move Axis2D");

        InjectKey(Key.W, pressed: false);
        InjectKey(Key.D, pressed: false);
        EvaluateAndSample();
    }

    private void VerifyMouseLookAndJump()
    {
        Guide.InjectInput(new InputEventMouseMotion { Relative = new Vector2(20f, -10f) });
        EvaluateAndSample();
        Assert(
            _service!.Frame.Axis2(Demo3D.Demo3DInput.Look).IsEqualApprox(new Vector2(-1f, 0.5f)),
            "鼠标灵敏度映射没有输出预期的 Look Axis2D");

        InjectKey(Key.Space, pressed: true);
        EvaluateAndSample();
        Assert(_service.Frame.JustPressed(Demo3D.Demo3DInput.Jump), "空格没有触发 Jump");
        InjectKey(Key.Space, pressed: false);
        EvaluateAndSample();
    }

    private void VerifyResultIsolation()
    {
        _service!.SetBaseContext(Demo3D.Demo3DInput.Result);
        InjectKey(Key.W, pressed: true);
        EvaluateAndSample();

        Assert(_service.IsContextActive(Demo3D.Demo3DInput.Result), "Result Context 没有生效");
        Assert(_service.Frame.Axis2(Demo3D.Demo3DInput.Move).IsZeroApprox(),
            "Result Context 仍然输出 Gameplay Move");

        InjectKey(Key.W, pressed: false);
        EvaluateAndSample();
    }

    private void EvaluateAndSample()
    {
        _guideNode.Call("_process", 1.0 / 60.0);
        _service!.Update();
    }

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
