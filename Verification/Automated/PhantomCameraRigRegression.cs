using System;
using Godot;
using GoDo;
using PhantomCamera;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>PhantomCameraRig 优先级映射和配置失败语义的无交互回归入口。</summary>
public sealed partial class PhantomCameraRigRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            ICameraService service = Services.Get<ICameraService>();
            PhantomCameraRig gameplay = GetNode<PhantomCameraRig>("GameplayRig");
            PhantomCameraRig intro = GetNode<PhantomCameraRig>("IntroRig");
            PhantomCamera3D gameplayBackend = gameplay.PhantomCameraNode.AsPhantomCamera3D();
            PhantomCamera3D introBackend = intro.PhantomCameraNode.AsPhantomCamera3D();

            Run("初始化为停用优先级", () =>
            {
                Assert(gameplayBackend.Priority == gameplay.InactivePriority, "Gameplay Rig 初始优先级错误");
                Assert(introBackend.Priority == intro.InactivePriority, "Intro Rig 初始优先级错误");
            });
            Run("激活主镜头", () =>
            {
                service.ActivatePrimary(CameraId.Create("gameplay"));
                Assert(gameplayBackend.Priority == gameplay.ActivePriority, "Gameplay Rig 未切换到激活优先级");
            });
            Run("切换主镜头", () =>
            {
                service.ActivatePrimary(CameraId.Create("intro"));
                Assert(gameplayBackend.Priority == gameplay.InactivePriority, "旧 Rig 未切换到停用优先级");
                Assert(introBackend.Priority == intro.ActivePriority, "新 Rig 未切换到激活优先级");
            });
            Run("恢复主镜头", () =>
            {
                Assert(service.RestorePreviousPrimary(), "未恢复到 Gameplay Rig");
                Assert(gameplayBackend.Priority == gameplay.ActivePriority, "恢复后的 Rig 优先级错误");
                Assert(introBackend.Priority == intro.InactivePriority, "被恢复替换的 Rig 优先级错误");
            });
            Run("拒绝无效优先级", VerifyInvalidPriorities);
            Run("拒绝非 Phantom 节点", VerifyIncompatibleNode);

            GD.Print($"[PhantomCameraRigRegression] PASS ({_passed}/6)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[PhantomCameraRigRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[PhantomCameraRigRegression] PASS: {name}");
    }

    private static void VerifyInvalidPriorities()
    {
        var node = new Node3D();
        var rig = new PhantomCameraRig
        {
            RigId = "invalid-priority",
            PhantomCameraNode = node,
            ActivePriority = 0,
            InactivePriority = 0,
        };
        try
        {
            AssertThrows<InvalidOperationException>(rig.InitializeBackend, "相同的激活/停用优先级未被拒绝");
        }
        finally
        {
            rig.Free();
            node.Free();
        }
    }

    private static void VerifyIncompatibleNode()
    {
        var node = new Node3D();
        var rig = new PhantomCameraRig
        {
            RigId = "incompatible-node",
            PhantomCameraNode = node,
        };
        try
        {
            AssertThrows<InvalidOperationException>(rig.InitializeBackend, "非 Phantom Camera 节点未被拒绝");
        }
        finally
        {
            rig.Free();
            node.Free();
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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
}
