using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>CameraService 核心注册、切换与恢复语义的无交互回归入口。</summary>
public sealed partial class CameraServiceRegression : Node
{
    private int _passed;
    private CameraService _service = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            _service = Services.Get<ICameraService>() as CameraService ??
                throw new InvalidOperationException("ICameraService 不是 CameraService 实例。");

            Run("初始状态为空", VerifyInitialState);
            Run("首次激活", VerifyFirstActivation);
            Run("切换与恢复", VerifySwitchAndRestore);
            Run("重复激活无操作", VerifyRepeatedActivation);
            Run("激活失败保持当前镜头", VerifyActivationFailure);
            Run("停用失败回滚目标镜头", VerifyDeactivationFailureRollback);
            Run("同场景重复 ID 拒绝", VerifySameSceneDuplicateRejection);
            Run("跨场景相同 ID 选择新注册实例", VerifyCrossSceneReplacement);
            Run("跨场景同 ID 切换不写入历史", VerifyCrossSceneActiveReplacement);
            Run("失效历史会被跳过", VerifyInvalidHistoryIsSkipped);
            Run("活动 Rig 失效后清理状态", VerifyInvalidActiveRigIsPruned);
            Run("注销活动 Rig 后回退同 ID 实例", VerifyActiveUnregisterFallsBackToOlderRegistration);
            Run("CameraRig 随节点生命周期注册注销", VerifyCameraRigNodeLifecycle);
            Run("关闭服务清空状态", VerifyShutdownClearsState);
            Run("默认镜头 ID 拒绝", VerifyDefaultCameraIdRejection);
            Run("未知镜头失败", VerifyUnknownCameraFailure);

            _service.Shutdown();
            GD.Print($"[CameraServiceRegression] PASS ({_passed}/16)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            _service?.Shutdown();
            GD.PushError($"[CameraServiceRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        _service.Shutdown();
        verification();
        _passed++;
        GD.Print($"[CameraServiceRegression] PASS: {name}");
    }

    private void VerifyInitialState()
    {
        Assert(_service.ActivePrimary is null, "初始 ActivePrimary 不是 null");
        Assert(!_service.RestorePreviousPrimary(), "空历史不应恢复成功");
    }

    private void VerifyFirstActivation()
    {
        using var fixture = new RigFixture("gameplay", "SceneA");
        _service.Register(fixture.Rig, fixture.SceneRoot);

        _service.ActivatePrimary(fixture.Rig.CameraId);

        Assert(_service.ActivePrimary == fixture.Rig.CameraId, "首次激活后的 ActivePrimary 不正确");
        Assert(fixture.Rig.ActivateCount == 1, "首次激活没有调用 Rig.Activate");
        Assert(fixture.Rig.DeactivateCount == 0, "首次激活不应调用 Rig.Deactivate");
    }

    private void VerifySwitchAndRestore()
    {
        using var fixture = new MultiRigFixture();
        _service.Register(fixture.Gameplay, fixture.SceneRoot);
        _service.Register(fixture.Intro, fixture.SceneRoot);

        _service.ActivatePrimary(fixture.Gameplay.CameraId);
        _service.ActivatePrimary(fixture.Intro.CameraId);
        bool restored = _service.RestorePreviousPrimary();

        Assert(restored, "存在有效历史时恢复返回 false");
        Assert(_service.ActivePrimary == fixture.Gameplay.CameraId, "恢复后没有回到 Gameplay 镜头");
        Assert(fixture.Gameplay.ActivateCount == 2, "Gameplay 镜头激活次数不正确");
        Assert(fixture.Gameplay.DeactivateCount == 1, "Gameplay 镜头停用次数不正确");
        Assert(fixture.Intro.ActivateCount == 1, "Intro 镜头激活次数不正确");
        Assert(fixture.Intro.DeactivateCount == 1, "Intro 镜头恢复时没有停用");
    }

    private void VerifyRepeatedActivation()
    {
        using var fixture = new RigFixture("gameplay", "SceneA");
        _service.Register(fixture.Rig, fixture.SceneRoot);

        _service.ActivatePrimary(fixture.Rig.CameraId);
        _service.ActivatePrimary(fixture.Rig.CameraId);

        Assert(fixture.Rig.ActivateCount == 1, "重复激活当前实例不应再次调用 Activate");
        Assert(!_service.RestorePreviousPrimary(), "重复激活不应写入恢复历史");
    }

    private void VerifyActivationFailure()
    {
        using var fixture = new MultiRigFixture();
        fixture.Intro.FailActivate = true;
        _service.Register(fixture.Gameplay, fixture.SceneRoot);
        _service.Register(fixture.Intro, fixture.SceneRoot);
        _service.ActivatePrimary(fixture.Gameplay.CameraId);

        AssertThrows<CameraOperationException>(
            () => _service.ActivatePrimary(fixture.Intro.CameraId),
            "目标激活失败没有抛出 CameraOperationException");

        Assert(_service.ActivePrimary == fixture.Gameplay.CameraId, "激活失败后当前镜头发生变化");
        Assert(fixture.Gameplay.DeactivateCount == 0, "目标激活失败时不应停用当前镜头");
    }

    private void VerifyDeactivationFailureRollback()
    {
        using var fixture = new MultiRigFixture();
        fixture.Gameplay.FailDeactivate = true;
        _service.Register(fixture.Gameplay, fixture.SceneRoot);
        _service.Register(fixture.Intro, fixture.SceneRoot);
        _service.ActivatePrimary(fixture.Gameplay.CameraId);

        CameraOperationException exception = AssertThrows<CameraOperationException>(
            () => _service.ActivatePrimary(fixture.Intro.CameraId),
            "当前镜头停用失败没有抛出 CameraOperationException");

        Assert(_service.ActivePrimary == fixture.Gameplay.CameraId, "停用失败后当前镜头发生变化");
        Assert(exception.CameraId == fixture.Gameplay.CameraId, "停用失败没有保留原镜头 ID");
        Assert(exception.InnerException?.Message == "Deactivate failure", "停用失败没有保留原始异常");
        Assert(fixture.Intro.ActivateCount == 1, "切换前没有尝试激活目标镜头");
        Assert(fixture.Intro.DeactivateCount == 1, "停用失败后没有尝试回滚目标镜头");
    }

    private void VerifySameSceneDuplicateRejection()
    {
        using var first = new RigFixture("gameplay", "SharedScene");
        var secondRig = new RecordingCameraRig("gameplay", new Node());
        try
        {
            _service.Register(first.Rig, first.SceneRoot);
            AssertThrows<CameraOperationException>(
                () => _service.Register(secondRig, first.SceneRoot),
                "同一场景范围的重复 ID 没有被拒绝");
        }
        finally
        {
            secondRig.Dispose();
        }
    }

    private void VerifyCrossSceneReplacement()
    {
        using var oldFixture = new RigFixture("gameplay", "OldScene");
        using var newFixture = new RigFixture("gameplay", "NewScene");
        _service.Register(oldFixture.Rig, oldFixture.SceneRoot);
        _service.Register(newFixture.Rig, newFixture.SceneRoot);

        _service.ActivatePrimary(CameraId.Create("gameplay"));

        Assert(newFixture.Rig.ActivateCount == 1, "跨场景相同 ID 没有选择最后注册的新 Rig");
        Assert(oldFixture.Rig.ActivateCount == 0, "跨场景替换错误激活了旧 Rig");

        _service.Unregister(newFixture.Rig);
        _service.ActivatePrimary(CameraId.Create("gameplay"));
        Assert(oldFixture.Rig.ActivateCount == 1, "新 Rig 注销后旧 Rig 不可解析");
    }

    private void VerifyCrossSceneActiveReplacement()
    {
        using var oldFixture = new RigFixture("gameplay", "OldScene");
        using var newFixture = new RigFixture("gameplay", "NewScene");
        _service.Register(oldFixture.Rig, oldFixture.SceneRoot);
        _service.ActivatePrimary(oldFixture.Rig.CameraId);
        _service.Register(newFixture.Rig, newFixture.SceneRoot);

        _service.ActivatePrimary(newFixture.Rig.CameraId);

        Assert(oldFixture.Rig.DeactivateCount == 1, "同 ID 新 Rig 激活后旧 Rig 未停用");
        Assert(newFixture.Rig.ActivateCount == 1, "同 ID 新 Rig 未激活");
        Assert(!_service.RestorePreviousPrimary(), "同 ID 实例替换不应写入恢复历史");
    }

    private void VerifyInvalidHistoryIsSkipped()
    {
        using var fixture = new MultiRigFixture();
        _service.Register(fixture.Gameplay, fixture.SceneRoot);
        _service.Register(fixture.Intro, fixture.SceneRoot);
        _service.ActivatePrimary(fixture.Gameplay.CameraId);
        _service.ActivatePrimary(fixture.Intro.CameraId);

        fixture.Gameplay.Dispose();

        Assert(!_service.RestorePreviousPrimary(), "失效历史不应恢复成功");
        Assert(_service.ActivePrimary == fixture.Intro.CameraId, "跳过失效历史时当前镜头发生变化");
    }

    private void VerifyInvalidActiveRigIsPruned()
    {
        using var fixture = new RigFixture("gameplay", "SceneA");
        _service.Register(fixture.Rig, fixture.SceneRoot);
        _service.ActivatePrimary(fixture.Rig.CameraId);

        fixture.Rig.Dispose();

        Assert(_service.ActivePrimary is null, "活动 Rig 失效后 ActivePrimary 应为空");
        Assert(!_service.RestorePreviousPrimary(), "活动 Rig 失效后不应出现可恢复历史");
        AssertThrows<CameraOperationException>(
            () => _service.ActivatePrimary(fixture.Rig.CameraId),
            "活动 Rig 失效后仍可被重新激活");
    }

    private void VerifyActiveUnregisterFallsBackToOlderRegistration()
    {
        using var oldFixture = new RigFixture("gameplay", "OldScene");
        using var newFixture = new RigFixture("gameplay", "NewScene");
        _service.Register(oldFixture.Rig, oldFixture.SceneRoot);
        _service.Register(newFixture.Rig, newFixture.SceneRoot);
        _service.ActivatePrimary(newFixture.Rig.CameraId);

        _service.Unregister(newFixture.Rig);

        Assert(_service.ActivePrimary is null, "注销活动 Rig 后 ActivePrimary 应为空");
        _service.ActivatePrimary(oldFixture.Rig.CameraId);
        Assert(oldFixture.Rig.ActivateCount == 1, "注销新 Rig 后旧的同 ID Rig 不可激活");
    }

    private void VerifyCameraRigNodeLifecycle()
    {
        var scope = new Node { Name = "LifecycleScene" };
        var rig = new LifecycleCameraRig { RigId = "lifecycle" };
        try
        {
            AddChild(scope);
            scope.AddChild(rig);

            CameraId id = CameraId.Create("lifecycle");
            _service.ActivatePrimary(id);
            Assert(rig.ActivateCount == 1, "CameraRig 进入树后没有自动注册");

            scope.RemoveChild(rig);
            Assert(_service.ActivePrimary is null, "CameraRig 退出树后没有注销活动实例");
            AssertThrows<CameraOperationException>(
                () => _service.ActivatePrimary(id),
                "CameraRig 退出树后仍可被激活");
        }
        finally
        {
            rig.Free();
            scope.Free();
        }
    }

    private void VerifyShutdownClearsState()
    {
        using var fixture = new MultiRigFixture();
        _service.Register(fixture.Gameplay, fixture.SceneRoot);
        _service.Register(fixture.Intro, fixture.SceneRoot);
        _service.ActivatePrimary(fixture.Gameplay.CameraId);
        _service.ActivatePrimary(fixture.Intro.CameraId);

        _service.Shutdown();

        Assert(_service.ActivePrimary is null, "关闭服务后 ActivePrimary 未清空");
        Assert(!_service.RestorePreviousPrimary(), "关闭服务后恢复历史未清空");
        AssertThrows<CameraOperationException>(
            () => _service.ActivatePrimary(fixture.Gameplay.CameraId),
            "关闭服务后注册表未清空");
    }

    private void VerifyDefaultCameraIdRejection()
    {
        AssertThrows<ArgumentException>(
            () => _service.ActivatePrimary(default),
            "默认 CameraId 没有被拒绝");
    }

    private void VerifyUnknownCameraFailure()
    {
        CameraId missing = CameraId.Create("missing");
        AssertThrows<CameraOperationException>(
            () => _service.ActivatePrimary(missing),
            "未知镜头没有抛出 CameraOperationException");
        Assert(_service.ActivePrimary is null, "未知镜头失败后出现活动镜头");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class RigFixture : IDisposable
    {
        public Node SceneRoot { get; }
        public RecordingCameraRig Rig { get; }

        public RigFixture(string id, string sceneName)
        {
            SceneRoot = new Node { Name = sceneName };
            Rig = new RecordingCameraRig(id, new Node());
        }

        public void Dispose()
        {
            Rig.Dispose();
            SceneRoot.Free();
        }
    }

    private sealed class MultiRigFixture : IDisposable
    {
        public Node SceneRoot { get; } = new() { Name = "SharedScene" };
        public RecordingCameraRig Gameplay { get; } = new("gameplay", new Node());
        public RecordingCameraRig Intro { get; } = new("intro", new Node());

        public void Dispose()
        {
            Gameplay.Dispose();
            Intro.Dispose();
            SceneRoot.Free();
        }
    }

    private sealed class RecordingCameraRig : ICameraRigDriver, IDisposable
    {
        private readonly Node _lifetimeOwner;
        private bool _disposed;

        public CameraId CameraId { get; }
        public GodotObject LifetimeOwner => _lifetimeOwner;
        public int ActivateCount { get; private set; }
        public int DeactivateCount { get; private set; }
        public bool FailActivate { get; set; }
        public bool FailDeactivate { get; set; }

        public RecordingCameraRig(string id, Node lifetimeOwner)
        {
            CameraId = CameraId.Create(id);
            _lifetimeOwner = lifetimeOwner;
        }

        public void Activate()
        {
            ActivateCount++;
            if (FailActivate)
                throw new InvalidOperationException("Activate failure");
        }

        public void Deactivate()
        {
            DeactivateCount++;
            if (FailDeactivate)
                throw new InvalidOperationException("Deactivate failure");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _lifetimeOwner.Free();
        }
    }

    private sealed partial class LifecycleCameraRig : CameraRig
    {
        public int ActivateCount { get; private set; }

        protected override void ActivateRig() => ActivateCount++;

        protected override void DeactivateRig() { }
    }
}
