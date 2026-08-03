using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>UiService 层级、返回栈、失败语义与场景清理的无交互回归入口。</summary>
public sealed partial class UiServiceRegression : Node
{
    private static readonly ResourceKey ControlAKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiControlA.tscn");
    private static readonly ResourceKey ControlBKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiControlB.tscn");
    private static readonly ResourceKey InvalidRootKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiInvalidRoot.tscn");
    private static readonly ResourceKey MissingKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/UI/Missing.tscn");
    private static readonly ResourceKey ValidUiConfigKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiConfigValid.tres");
    private static readonly ResourceKey InvalidUiConfigKey =
        ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiConfigInvalid.tres");
    private static readonly UiId ConfigViewId = UiId.Create("settings");
    private static readonly UiId ConfigModalId = UiId.Create("confirm");
    private static readonly UiId ConfiguredViewId = UiId.Create("configured");
    private static readonly UiId ConfiguredSceneId = UiId.Create("configured_scene");
    private static readonly UiId ReusableViewId = UiId.Create("reusable");
    private static readonly UiId ReusableSceneId = UiId.Create("reusable_scene");

    private IUiService _ui = null!;
    private int _passed;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            _ui = Services.Get<IUiService>();

            Run("UiId 值语义", VerifyUiId);
            Run("UiConfig 内容校验", VerifyUiConfigValidation);
            Run("空返回栈", VerifyEmptyBackStack);
            await RunAsync("Scene 层并行界面", VerifySceneLayer);
            await RunAsync("View 返回栈", VerifyViewStack);
            await RunAsync("Modal 优先级与 Host", VerifyModalStack);
            await RunAsync("Overlay 并行层与输入语义", VerifyOverlayLayer);
            await RunAsync("失败后状态保持", VerifyFailureSemantics);
            await RunAsync("主场景变更清理 Scene 层", VerifySceneChangeCleanup);
            await RunAsync("外部释放后恢复托管状态", VerifyExternalReleaseRecovery);
            await RunAsync("UiConfig 配置与打开", VerifyUiConfig);
            await RunAsync("类型安全打开与挂载前配置", VerifyGenericOpen);
            await RunAsync("Single UI 实例复用", VerifyReusableUi);
            await RunAsync("异步打开与加载中并发保护", VerifyAsyncOpen);
            await RunAsync("异步打开取消", VerifyAsyncOpenCancellation);
            await RunAsync("按标识与层取消异步打开", VerifyManagedOpenCancellation);
            await RunAsync("UI 查询", VerifyQueries);
            await RunAsync("指定与批量关闭", VerifySpecifiedAndBatchClose);
            await RunAsync("关闭并返回到目标界面", VerifyCloseTo);
            await RunAsync("键盘与手柄焦点行为", VerifyFocusBehavior);

            GD.Print($"[UiServiceRegression] PASS ({_passed}/20)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[UiServiceRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[UiServiceRegression] PASS: {name}");
    }

    private async Task RunAsync(string name, Func<Task> verification)
    {
        await verification();
        _passed++;
        GD.Print($"[UiServiceRegression] PASS: {name}");
    }

    private void VerifyEmptyBackStack()
    {
        Assert(!_ui.TryGoBack(), "空 UI 返回栈错误地返回 true");
    }

    private static void VerifyUiId()
    {
        UiId id = UiId.Create(" settings ");
        Assert(id.Value == "settings", "UiId 没有去除首尾空白");
        Assert(id == UiId.Create("settings"), "相同 UiId 没有值相等");
        Assert(id != UiId.Create("Settings"), "UiId 错误地忽略了大小写");
        AssertThrows<ArgumentException>(
            () => UiId.Create(" "),
            "空 UiId 没有被拒绝");
    }

    private static void VerifyUiConfigValidation()
    {
        var emptyIdConfig = new UiConfig();
        emptyIdConfig.Entries.Add(new UiConfigEntry
        {
            Id = " ",
            Locator = ControlAKey.Value
        });
        AssertThrows<InvalidOperationException>(
            emptyIdConfig.Validate,
            "UiConfig 中的空 Id 没有被拒绝");

        var invalidLocatorConfig = new UiConfig();
        invalidLocatorConfig.Entries.Add(new UiConfigEntry
        {
            Id = "invalid_locator",
            Locator = "UI/Invalid.tscn"
        });
        AssertThrows<InvalidOperationException>(
            invalidLocatorConfig.Validate,
            "UiConfig 中的非法资源定位没有被拒绝");

        var invalidReuseConfig = new UiConfig();
        invalidReuseConfig.Entries.Add(new UiConfigEntry
        {
            Id = "invalid_reuse",
            Locator = ControlAKey.Value,
            InstanceMode = UiInstanceMode.Multiple,
            ReuseInstance = true
        });
        AssertThrows<InvalidOperationException>(
            invalidReuseConfig.Validate,
            "Multiple UI 启用实例复用没有被拒绝");
    }

    private async Task VerifySceneLayer()
    {
        Control first = _ui.Open(ControlAKey, UiLayer.Scene);
        Control second = _ui.Open(ControlBKey, UiLayer.Scene);

        Assert(first.Visible && second.Visible, "Scene 层界面没有并行显示");
        Assert(first.GetParent().Name == "SceneRoot", "Scene 界面没有挂载到 SceneRoot");
        Assert(second.GetParent() == first.GetParent(), "Scene 界面没有挂载到同一显示根");
        Assert(!_ui.TryGoBack(), "Scene 界面错误地进入返回栈");

        _ui.Close(first);
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(first), "指定 Scene 界面没有在帧末释放");
        Assert(GodotObject.IsInstanceValid(second), "关闭一个 Scene 界面影响了其他实例");

        _ui.Close(second);
        await NextFrame();
    }

    private async Task VerifyViewStack()
    {
        Control first = _ui.Open(ControlAKey, UiLayer.View);
        Control second = _ui.Open(ControlBKey, UiLayer.View);

        Assert(!first.Visible, "打开新 View 后前一个 View 仍可见");
        Assert(second.Visible, "顶部 View 不可见");
        _ui.Close(first);
        Assert(second.Visible, "关闭非顶部 View 影响了当前顶部 View");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(first), "非顶部 View 没有释放");

        _ui.Close(second);
        await NextFrame();

        first = _ui.Open(ControlAKey, UiLayer.View);
        second = _ui.Open(ControlBKey, UiLayer.View);
        _ui.Close(second);
        Assert(first.Visible, "关闭顶部 View 后前一个 View 没有恢复");
        Assert(_ui.TryGoBack(), "存在 View 时 TryGoBack 返回 false");
        await NextFrame();

        Assert(!GodotObject.IsInstanceValid(first), "TryGoBack 没有释放顶部 View");
        Assert(!_ui.TryGoBack(), "View 栈清空后 TryGoBack 仍返回 true");
    }

    private async Task VerifyModalStack()
    {
        Control view = _ui.Open(ControlAKey, UiLayer.View);
        Control firstModal = _ui.Open(ControlAKey, UiLayer.Modal);
        Control secondModal = _ui.Open(ControlBKey, UiLayer.Modal);
        Control host = firstModal.GetParent<Control>();

        Assert(view.Visible, "打开 Modal 错误地隐藏了当前 View");
        Assert(host.Name == "ModalHost", "Modal 没有使用独立 Host");
        Assert(host.MouseFilter == Control.MouseFilterEnum.Stop, "Modal Host 没有阻止 GUI 指针穿透");
        Assert(host.GetParent().Name == "ModalRoot", "Modal Host 没有挂载到 ModalRoot");
        _ui.Close(firstModal);
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(host), "关闭下层 Modal 没有释放对应 Host");
        Assert(GodotObject.IsInstanceValid(secondModal), "关闭下层 Modal 影响了顶部 Modal");

        Assert(_ui.TryGoBack(), "顶部 Modal 没有优先响应 TryGoBack");
        Assert(GodotObject.IsInstanceValid(view) && view.Visible, "关闭 Modal 影响了当前 View");
        Assert(_ui.TryGoBack(), "Modal 清空后没有返回当前 View");
        await NextFrame();

        Assert(!GodotObject.IsInstanceValid(secondModal), "顶部 Modal 没有释放");
        Assert(!GodotObject.IsInstanceValid(firstModal), "下层 Modal 没有释放");
        Assert(!GodotObject.IsInstanceValid(view), "View 没有释放");
    }

    private async Task VerifyOverlayLayer()
    {
        Control first = _ui.Open(ControlAKey, UiLayer.Overlay);
        Control second = _ui.Open(ControlBKey, UiLayer.Overlay);
        Assert(first.GetParent().Name == "OverlayRoot", "Overlay 没有挂载到 OverlayRoot");
        Assert(
            first.GetParent<Control>().MouseFilter == Control.MouseFilterEnum.Ignore,
            "OverlayRoot 默认阻止了 GUI 指针输入");
        Assert(first.Visible && second.Visible, "打开 Overlay 错误地隐藏了已有 Overlay");
        Assert(
            _ui.TryGetTop(UiLayer.Overlay, out Control? top) && top == second,
            "Overlay 顶部查询没有返回最后打开实例");
        Assert(!_ui.TryGoBack(), "返回操作错误地关闭了 Overlay");

        EventChannel.Emit<FrameworkMainSceneChangedEvent>();
        await NextFrame();
        Assert(
            GodotObject.IsInstanceValid(first) && GodotObject.IsInstanceValid(second),
            "主场景切换错误地清理了 Overlay");

        Assert(_ui.CloseTo(first) == 1, "CloseTo(Overlay) 没有只关闭目标上方 Overlay");
        await NextFrame();
        Assert(
            GodotObject.IsInstanceValid(first) && !GodotObject.IsInstanceValid(second),
            "CloseTo(Overlay) 没有保留目标或没有关闭上方实例");
        _ui.Close(first);
        await NextFrame();

        Control released = _ui.Open(ControlAKey, UiLayer.Overlay);
        released.QueueFree();
        await NextFrame();
        Control replacement = _ui.Open(ControlBKey, UiLayer.Overlay);
        Assert(
            _ui.TryGetTop(UiLayer.Overlay, out Control? recovered) && recovered == replacement,
            "外部释放 Overlay 后没有恢复托管状态");
        _ui.Close(replacement);
        await NextFrame();

        _ui.Open(ControlAKey, UiLayer.Overlay);
        _ui.Open(ControlBKey, UiLayer.Overlay);
        Assert(_ui.CloseAll(UiLayer.Overlay) == 2, "CloseAll(Overlay) 返回了错误关闭数量");
        await NextFrame();
    }

    private async Task VerifyFailureSemantics()
    {
        Control current = _ui.Open(ControlAKey, UiLayer.View);
        var unmanaged = new Control();

        UiOpenException missing = AssertThrows<UiOpenException>(
            () => _ui.Open(MissingKey, UiLayer.View),
            "缺失资源没有抛出 UiOpenException");
        Assert(missing.Key == MissingKey, "UiOpenException 没有保留缺失资源键");
        UiOpenException invalidRoot = AssertThrows<UiOpenException>(
            () => _ui.Open(InvalidRootKey, UiLayer.Modal),
            "非 Control 根节点没有抛出 UiOpenException");
        Assert(invalidRoot.Key == InvalidRootKey, "UiOpenException 没有保留错误根资源键");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ui.Open(ControlBKey, (UiLayer)999),
            "未知 UiLayer 没有抛出 ArgumentOutOfRangeException");
        try
        {
            AssertThrows<InvalidOperationException>(
                () => _ui.Close(unmanaged),
                "非托管 Control 可以被关闭");
        }
        finally
        {
            unmanaged.Free();
        }

        Assert(current.Visible, "打开失败后当前 View 被隐藏");
        Assert(_ui.TryGoBack(), "打开失败破坏了现有 View 返回栈");
        await NextFrame();
        Assert(!_ui.TryGoBack(), "打开失败向返回栈写入了残留项");
    }

    private async Task VerifySceneChangeCleanup()
    {
        Control scene = _ui.Open(ControlAKey, UiLayer.Scene);
        Control view = _ui.Open(ControlBKey, UiLayer.View);

        EventChannel.Emit<FrameworkMainSceneChangedEvent>();
        await NextFrame();

        Assert(!GodotObject.IsInstanceValid(scene), "主场景变更事件没有清理 Scene 层");
        Assert(GodotObject.IsInstanceValid(view) && view.Visible, "主场景变更错误地清理了 View 层");
        Assert(_ui.TryGoBack(), "Scene 层清理破坏了 View 返回栈");
        await NextFrame();
    }

    private async Task VerifyExternalReleaseRecovery()
    {
        Control releasedScene = _ui.Open(ControlAKey, UiLayer.Scene);
        releasedScene.QueueFree();
        await NextFrame();

        Control activeScene = _ui.Open(ControlBKey, UiLayer.Scene);
#if DEBUG
        UiDebugSnapshot snapshot = ((UiService)_ui).GetDebugSnapshot();
        Assert(CountEntries(snapshot, UiLayer.Scene) == 1, "外部释放的 Scene 界面仍保留在托管记录中");
#endif
        _ui.Close(activeScene);
        await NextFrame();

        Control firstView = _ui.Open(ControlAKey, UiLayer.View);
        Control releasedView = _ui.Open(ControlBKey, UiLayer.View);
        releasedView.QueueFree();
        await NextFrame();

        Control replacementView = _ui.Open(ControlBKey, UiLayer.View);
        Assert(!firstView.Visible && replacementView.Visible, "失效 View 栈顶阻断了后续界面打开");
        _ui.Close(replacementView);
        Assert(firstView.Visible, "清理失效 View 后没有恢复前一个有效界面");
        _ui.Close(firstView);
        await NextFrame();

        Control releasedModal = _ui.Open(ControlAKey, UiLayer.Modal);
        Control releasedHost = releasedModal.GetParent<Control>();
        releasedModal.QueueFree();
        await NextFrame();

        Control replacementModal = _ui.Open(ControlBKey, UiLayer.Modal);
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(releasedHost), "外部释放 Modal 后遗留了空的 ModalHost");
        _ui.Close(replacementModal);
        await NextFrame();
    }

    private async Task VerifyUiConfig()
    {
        AssertThrows<InvalidOperationException>(
            () => _ui.Open(ConfigViewId),
            "未加载 UiConfig 时可以按 UiId 打开界面");
        AssertThrows<ConfigValidationException>(
            () => _ui.LoadUiConfig(InvalidUiConfigKey),
            "重复 UiId 没有导致目录校验失败");

        _ui.LoadUiConfig(ValidUiConfigKey);
        AssertThrows<System.Collections.Generic.KeyNotFoundException>(
            () => _ui.Open(UiId.Create("missing")),
            "未注册 UiId 没有导致可见失败");

        Control view = _ui.Open(ConfigViewId);
        Assert(view.GetParent().Name == "ViewRoot", "UiConfig 默认层级没有生效");
        AssertThrows<InvalidOperationException>(
            () => _ui.Open(ConfigViewId),
            "Single UI 可以重复打开");
        AssertThrows<InvalidOperationException>(
            () => _ui.LoadUiConfig(ValidUiConfigKey),
            "存在托管界面时可以替换 UiConfig");
        _ui.Close(view);
        await NextFrame();

        _ui.LoadUiConfig(ValidUiConfigKey);
        Control firstModal = _ui.Open(ConfigModalId);
        Control secondModal = _ui.Open(ConfigModalId);
        Assert(firstModal != secondModal, "Multiple UI 没有创建独立实例");
        Assert(_ui.TryGoBack() && _ui.TryGoBack(), "Multiple UI 没有进入 Modal 返回栈");
        await NextFrame();
    }

    private async Task VerifyGenericOpen()
    {
        UiConfigurableControl configured = _ui.Open<UiConfigurableControl>(
            ConfiguredViewId,
            view => view.ConfiguredValue = "configured");
        Assert(configured.WasConfiguredBeforeReady, "配置回调没有在 _Ready 前执行");
        Assert(
            _ui.TryGetTop<UiConfigurableControl>(ConfiguredViewId, out UiConfigurableControl? queried) &&
            queried == configured,
            "强类型查询没有返回已打开的 UiId 实例");
        AssertThrows<InvalidCastException>(
            () => _ui.TryGetTop<Button>(ConfiguredViewId, out _),
            "强类型查询静默接受了错误的根节点类型");
        _ui.Close(configured);
        await NextFrame();
        Assert(
            !_ui.TryGetTop<UiConfigurableControl>(ConfiguredViewId, out UiConfigurableControl? closed) &&
            closed is null,
            "强类型查询错误地返回了已关闭实例");

        AssertThrows<UiOpenException>(
            () => _ui.Open<Button>(ConfiguredViewId),
            "UI 根节点类型不匹配没有导致打开失败");
        Assert(!_ui.IsOpen(ConfiguredViewId), "类型不匹配后污染了 UiId 打开状态");

        InvalidOperationException configureFailure = AssertThrows<InvalidOperationException>(
            () => _ui.Open<UiConfigurableControl>(
                ConfiguredViewId,
                _ => throw new InvalidOperationException("configure failed")),
            "配置回调异常没有透传给调用方");
        Assert(configureFailure.Message == "configure failed", "配置回调异常被错误包装");
        Assert(!_ui.IsOpen(ConfiguredViewId), "配置回调失败后污染了 UiId 打开状态");

        UiConfigurableControl raw = _ui.Open<UiConfigurableControl>(
            ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiConfigurableControl.tscn"),
            UiLayer.Scene,
            view => view.ConfiguredValue = "configured");
        Assert(raw.WasConfiguredBeforeReady, "按 ResourceKey 打开时配置回调没有在 _Ready 前执行");
        _ui.Close(raw);
        await NextFrame();
    }

    private async Task VerifyAsyncOpen()
    {
        float progress = 0f;
        void OnProgress(float value) => progress = value;
        Task<UiConfigurableControl> opening =
            _ui.OpenAsync<UiConfigurableControl>(
                ConfiguredViewId,
                view => view.ConfiguredValue = "configured",
                OnProgress);
        Assert(_ui.IsOpening(ConfiguredViewId), "异步打开期间 IsOpening 返回 false");
        Assert(_ui.GetOpeningCount(ConfiguredViewId) == 1, "Single UI 加载中数量错误");
        AssertThrows<InvalidOperationException>(
            () => _ui.Open(ConfiguredViewId),
            "Single UI 异步加载期间仍可同步重复打开");
        AssertThrows<InvalidOperationException>(
            () => _ui.OpenAsync<UiConfigurableControl>(ConfiguredViewId),
            "Single UI 异步加载期间仍可异步重复打开");
        AssertThrows<InvalidOperationException>(
            () => _ui.LoadUiConfig(ValidUiConfigKey),
            "异步打开期间可以替换 UiConfig");

        UiConfigurableControl view = await opening;
        Assert(view.WasConfiguredBeforeReady, "异步打开的配置回调没有在 _Ready 前执行");
        Assert(progress == 1f, "异步打开没有发布最终加载进度");
        Assert(_ui.IsOpen(ConfiguredViewId), "异步打开完成后没有登记 UiId");
        Assert(!_ui.IsOpening(ConfiguredViewId), "异步打开完成后仍处于加载中状态");
        Assert(_ui.GetOpeningCount(ConfiguredViewId) == 0, "异步打开完成后加载中数量没有归零");
        _ui.Close(view);
        await NextFrame();

        Task<Button> mismatch = _ui.OpenAsync<Button>(ConfiguredViewId);
        await AssertThrowsAsync<UiOpenException>(
            () => mismatch,
            "异步打开根节点类型不匹配没有导致失败");
        Assert(!_ui.IsOpen(ConfiguredViewId), "异步类型不匹配后污染了 UiId 打开状态");
        Assert(!_ui.IsOpening(ConfiguredViewId), "异步类型不匹配后没有清理加载中状态");

        Task<UiConfigurableControl> configureFailure =
            _ui.OpenAsync<UiConfigurableControl>(
                ConfiguredViewId,
                _ => throw new InvalidOperationException("async configure failed"));
        InvalidOperationException configureException =
            await AssertThrowsAsync<InvalidOperationException>(
                () => configureFailure,
                "异步配置回调异常没有透传给调用方");
        Assert(configureException.Message == "async configure failed", "异步配置回调异常被错误包装");
        Assert(!_ui.IsOpen(ConfiguredViewId), "异步配置回调失败后污染了 UiId 打开状态");

        Task<UiConfigurableControl> recovered =
            _ui.OpenAsync<UiConfigurableControl>(
                ConfiguredViewId,
                view => view.ConfiguredValue = "configured");
        UiConfigurableControl recoveredView = await recovered;
        Assert(recoveredView.WasConfiguredBeforeReady, "异步失败后 Single UI 占位没有正确释放");
        _ui.Close(recoveredView);
        await NextFrame();
        _ui.LoadUiConfig(ValidUiConfigKey);

        Task<Control> firstModal = _ui.OpenAsync<Control>(ConfigModalId);
        Task<Control> secondModal = _ui.OpenAsync<Control>(ConfigModalId);
        Assert(_ui.GetOpeningCount(ConfigModalId) == 2, "Multiple UI 加载中数量错误");
#if DEBUG
        UiDebugSnapshot openingSnapshot = ((UiService)_ui).GetDebugSnapshot();
        Assert(
            openingSnapshot.Openings.Length == 1 &&
            openingSnapshot.Openings[0].Id == ConfigModalId &&
            openingSnapshot.Openings[0].Layer == UiLayer.Modal &&
            openingSnapshot.Openings[0].RequestCount == 2,
            "Debug 快照没有按 UiId 聚合异步打开请求");
#endif
        Control[] modalViews = await Task.WhenAll(firstModal, secondModal);
#if DEBUG
        Assert(
            ((UiService)_ui).GetDebugSnapshot().Openings.Length == 0,
            "异步打开完成后 Debug 快照仍保留加载中请求");
#endif
        Assert(_ui.GetOpeningCount(ConfigModalId) == 0, "Multiple UI 完成后加载中数量没有归零");
        Assert(modalViews[0] != modalViews[1], "并发打开 Multiple UI 没有创建独立实例");
        Assert(_ui.CloseAll(ConfigModalId) == 2, "并发异步打开的 Multiple UI 没有完整登记");
        await NextFrame();

        Task<UiConfigurableControl> staleScene =
            _ui.OpenAsync<UiConfigurableControl>(ConfiguredSceneId);
        Task<UiConfigurableControl> survivingView =
            _ui.OpenAsync<UiConfigurableControl>(ConfiguredViewId);
        Assert(_ui.IsOpening(ConfiguredSceneId), "异步 Scene UI 没有进入加载中状态");
        EventChannel.Emit<FrameworkMainSceneChangedEvent>();
        await AssertThrowsAsync<OperationCanceledException>(
            () => staleScene,
            "主场景切换后过期的异步 Scene UI 仍然完成打开");
        UiConfigurableControl survivingViewControl = await survivingView;
        Assert(!_ui.IsOpening(ConfiguredSceneId), "过期 Scene UI 请求没有清理加载中状态");
        Assert(!_ui.IsOpen(ConfiguredSceneId), "过期 Scene UI 请求污染了打开状态");
        Assert(!_ui.TryGetTop(UiLayer.Scene, out _), "过期 Scene UI 请求在新主场景中完成挂载");
        Assert(
            _ui.IsOpen(ConfiguredViewId),
            "主场景切换错误取消了共享资源加载中的 View 请求");
        _ui.Close(survivingViewControl);
        await NextFrame();

        Task<UiConfigurableControl> staleDirectScene =
            _ui.OpenAsync<UiConfigurableControl>(
                ResourceKey.Create(
                    "res://Verification/Automated/Fixtures/UI/UiConfigurableControl.tscn"),
                UiLayer.Scene);
        EventChannel.Emit<FrameworkMainSceneChangedEvent>();
        await AssertThrowsAsync<OperationCanceledException>(
            () => staleDirectScene,
            "主场景切换没有取消 ResourceKey 直接 Scene 请求");
        Assert(
            !_ui.TryGetTop(UiLayer.Scene, out _),
            "取消的 ResourceKey 直接 Scene 请求仍然完成挂载");

        Task<UiConfigurableControl> raw =
            _ui.OpenAsync<UiConfigurableControl>(
                ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiConfigurableControl.tscn"),
                UiLayer.Scene,
                view => view.ConfiguredValue = "configured");
        UiConfigurableControl rawView = await raw;
        Assert(rawView.WasConfiguredBeforeReady, "按 ResourceKey 异步打开时配置回调时机错误");
        _ui.Close(rawView);
        await NextFrame();
    }

    private async Task VerifyReusableUi()
    {
        UiConfigurableControl first = _ui.Open<UiConfigurableControl>(
            ReusableViewId,
            view => view.ConfiguredValue = "first");
        Assert(first.ReadyCount == 1, "复用 UI 首次打开没有执行一次 _Ready");
        Assert(first.EnterTreeCount == 1, "复用 UI 首次打开没有进入场景树");
        Assert(first.AcquireCount == 1, "复用 UI 首次打开没有执行 OnAcquire");
        Assert(first.ConfiguredValueAtLastEnterTree == "first", "首次挂载前没有完成配置");
        Assert(first.ConfiguredValueAtLastAcquire == "first", "OnAcquire 没有观察到配置结果");
        Assert(!_ui.HasCachedInstance(ReusableViewId), "打开中的复用 UI 被错误视为缓存实例");
        Assert(_ui.ClearCachedInstances() == 0, "清理缓存错误影响了打开中的复用 UI");
        Assert(GodotObject.IsInstanceValid(first), "清理缓存释放了打开中的复用 UI");

        _ui.Close(first);
        Assert(GodotObject.IsInstanceValid(first), "启用复用的 UI 在关闭时被释放");
        Assert(first.GetParent() is null, "关闭后的复用 UI 仍留在场景树");
        Assert(first.ReleaseCount == 1, "关闭复用 UI 没有执行 OnRelease");
        Assert(!_ui.IsOpen(ReusableViewId), "关闭后的缓存实例仍被视为打开");
        Assert(_ui.HasCachedInstance(ReusableViewId), "关闭后的复用 UI 没有登记为缓存实例");

        UiConfigurableControl second = _ui.Open<UiConfigurableControl>(
            ReusableViewId,
            view => view.ConfiguredValue = "second");
        Assert(ReferenceEquals(first, second), "再次打开没有复用同一 UI 实例");
        Assert(second.ReadyCount == 1, "复用时错误地再次执行 _Ready");
        Assert(second.EnterTreeCount == 2, "复用时没有重新进入场景树");
        Assert(second.AcquireCount == 2, "复用时没有再次执行 OnAcquire");
        Assert(second.ConfiguredValueAtLastEnterTree == "second", "复用实例没有在挂载前配置");
        Assert(second.ConfiguredValueAtLastAcquire == "second", "复用 OnAcquire 时机早于配置");
        Assert(!_ui.HasCachedInstance(ReusableViewId), "重新打开后仍保留缓存登记");

        _ui.Close(second);
        AssertThrows<UiOpenException>(
            () => _ui.Open<Button>(ReusableViewId),
            "同步复用静默接受了错误的根节点类型");
        Assert(
            _ui.HasCachedInstance(ReusableViewId) &&
            GodotObject.IsInstanceValid(second),
            "同步复用类型不匹配破坏了有效缓存实例");

        Task<Button> cachedTypeMismatch = _ui.OpenAsync<Button>(ReusableViewId);
        await AssertThrowsAsync<UiOpenException>(
            () => cachedTypeMismatch,
            "异步复用静默接受了错误的根节点类型");
        Assert(cachedTypeMismatch.IsFaulted, "缓存类型不匹配任务没有进入 Faulted 状态");
        Assert(
            _ui.HasCachedInstance(ReusableViewId) &&
            GodotObject.IsInstanceValid(second),
            "异步复用类型不匹配破坏了有效缓存实例");

        bool canceledConfigureCalled = false;
        Task<UiConfigurableControl> canceledCachedOpen =
            _ui.OpenAsync<UiConfigurableControl>(
                ReusableViewId,
                _ => canceledConfigureCalled = true,
                _ => Assert(
                    _ui.CancelOpenRequests(ReusableViewId) == 1,
                    "回调内取消缓存复用请求失败"));
        Assert(canceledCachedOpen.IsCanceled, "缓存复用取消任务没有进入 Canceled 状态");
        await AssertThrowsAsync<OperationCanceledException>(
            () => canceledCachedOpen,
            "缓存复用请求没有响应回调内取消");
        Assert(!canceledConfigureCalled, "缓存复用取消后仍执行了配置回调");
        Assert(_ui.HasCachedInstance(ReusableViewId), "缓存复用取消后丢失了原实例");
        Assert(!_ui.IsOpen(ReusableViewId), "缓存复用取消后仍然挂载了实例");

        float cachedProgress = 0f;
        UiConfigurableControl asyncReused = await _ui.OpenAsync<UiConfigurableControl>(
            ReusableViewId,
            view => view.ConfiguredValue = "async",
            progress =>
            {
                cachedProgress = progress;
                Assert(_ui.IsOpening(ReusableViewId), "异步复用进度回调期间没有加载中占位");
                AssertThrows<InvalidOperationException>(
                    () => _ui.Open(ReusableViewId),
                    "异步复用进度回调可以重入打开同一 Single UI");
            });
        Assert(ReferenceEquals(second, asyncReused), "异步打开没有复用缓存实例");
        Assert(cachedProgress == 1f, "异步复用没有发布完成进度");
        Assert(asyncReused.ConfiguredValueAtLastEnterTree == "async", "异步复用没有在挂载前配置");
        _ui.Close(asyncReused);

        InvalidOperationException configureFailure = AssertThrows<InvalidOperationException>(
            () => _ui.Open<UiConfigurableControl>(
                ReusableViewId,
                _ => throw new InvalidOperationException("reusable configure failed")),
            "复用实例配置失败没有透传");
        Assert(
            configureFailure.Message == "reusable configure failed",
            "复用实例配置失败被错误包装");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(second), "配置失败的复用实例仍被缓存");

        UiConfigurableControl replacement = _ui.Open<UiConfigurableControl>(
            ReusableViewId,
            view => view.ConfiguredValue = "replacement");
        Assert(!ReferenceEquals(second, replacement), "配置失败后仍复用了脏实例");
        replacement.ThrowOnRelease = true;
        InvalidOperationException releaseFailure = AssertThrows<InvalidOperationException>(
            () => _ui.Close(replacement),
            "OnRelease 失败没有导致关闭失败");
        Assert(
            releaseFailure.InnerException?.Message == "release failed",
            "OnRelease 失败没有保留原始异常");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(replacement), "OnRelease 失败的实例仍被缓存");

        UiConfigurableControl acquireFailureCandidate =
            _ui.Open<UiConfigurableControl>(ReusableViewId);
        _ui.Close(acquireFailureCandidate);
        Control lowerView = _ui.Open(ControlAKey, UiLayer.View);
        Button lowerButton = AddFocusButton(lowerView, "AcquireFailureFocus");
        lowerButton.GrabFocus();
        UiOpenException acquireFailure = AssertThrows<UiOpenException>(
            () => _ui.Open<UiConfigurableControl>(
                ReusableViewId,
                view => view.ThrowOnAcquire = true),
            "OnAcquire 失败没有导致打开失败");
        Assert(
            acquireFailure.InnerException?.Message == "acquire failed",
            "OnAcquire 失败没有保留原始异常");
        Assert(
            lowerView.Visible &&
            _ui.TryGetTop(UiLayer.View, out Control? restoredView) &&
            restoredView == lowerView,
            "OnAcquire 失败没有恢复前一个 View");
        Assert(
            GetViewport().GuiGetFocusOwner() == lowerButton,
            "OnAcquire 失败没有恢复前一个 View 的焦点");
        Assert(
            !_ui.IsOpen(ReusableViewId) &&
            !_ui.HasCachedInstance(ReusableViewId),
            "OnAcquire 失败在托管栈或缓存中遗留了实例");
        await NextFrame();
        Assert(
            !GodotObject.IsInstanceValid(acquireFailureCandidate),
            "OnAcquire 失败实例没有释放");
        _ui.Close(lowerView);
        await NextFrame();

        UiConfigurableControl finalReplacement = _ui.Open<UiConfigurableControl>(
            ReusableViewId,
            view => view.ConfiguredValue = "final");
        _ui.Close(finalReplacement);
        _ui.LoadUiConfig(ValidUiConfigKey);
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(finalReplacement), "替换 UiConfig 时没有释放缓存实例");

        UiConfigurableControl reusableScene = _ui.Open<UiConfigurableControl>(
            ReusableSceneId,
            view => view.ConfiguredValue = "scene");
        _ui.Close(reusableScene);
        Assert(GodotObject.IsInstanceValid(reusableScene), "Scene UI 关闭后没有进入实例缓存");
        EventChannel.Emit<FrameworkMainSceneChangedEvent>();
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(reusableScene), "主场景变更没有释放 Scene UI 缓存");

        UiConfigurableControl singleClear = _ui.Open<UiConfigurableControl>(ReusableViewId);
        _ui.Close(singleClear);
        Assert(_ui.HasCachedInstance(ReusableViewId), "单项清理前没有缓存实例");
        Assert(_ui.ClearCachedInstance(ReusableViewId), "单项缓存清理没有报告成功");
        Assert(!_ui.HasCachedInstance(ReusableViewId), "单项缓存清理后仍保留登记");
        Assert(!_ui.ClearCachedInstance(ReusableViewId), "重复单项缓存清理没有返回 false");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(singleClear), "单项缓存清理没有释放实例");

        UiConfigurableControl clearAllView = _ui.Open<UiConfigurableControl>(ReusableViewId);
        UiConfigurableControl clearAllScene = _ui.Open<UiConfigurableControl>(ReusableSceneId);
        _ui.Close(clearAllView);
        _ui.Close(clearAllScene);
        Assert(_ui.ClearCachedInstances() == 2, "全量缓存清理返回数量错误");
        Assert(!_ui.HasCachedInstance(ReusableViewId), "全量缓存清理后仍保留 View 登记");
        Assert(!_ui.HasCachedInstance(ReusableSceneId), "全量缓存清理后仍保留 Scene 登记");
        Assert(_ui.ClearCachedInstances() == 0, "空缓存全量清理没有返回 0");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(clearAllView), "全量缓存清理没有释放 View 实例");
        Assert(!GodotObject.IsInstanceValid(clearAllScene), "全量缓存清理没有释放 Scene 实例");
    }

    private async Task VerifyAsyncOpenCancellation()
    {
        using var preCanceled = new CancellationTokenSource();
        preCanceled.Cancel();
        Task<UiConfigurableControl> preCanceledOpen =
            _ui.OpenAsync<UiConfigurableControl>(
                ConfiguredViewId,
                cancellationToken: preCanceled.Token);
        OperationCanceledException preCanceledException =
            await AssertThrowsAsync<OperationCanceledException>(
                () => preCanceledOpen,
                "预取消令牌仍然启动了 UI 打开");
        Assert(
            preCanceledException.CancellationToken == preCanceled.Token,
            "预取消异常没有保留调用方令牌");
        Assert(!_ui.IsOpening(ConfiguredViewId), "预取消请求污染了加载中状态");
        Assert(!_ui.IsOpen(ConfiguredViewId), "预取消请求打开了 UI");

        using var inFlightCancellation = new CancellationTokenSource();
        Task<UiConfigurableControl> inFlightOpen =
            _ui.OpenAsync<UiConfigurableControl>(
                ConfiguredViewId,
                cancellationToken: inFlightCancellation.Token);
        Assert(_ui.IsOpening(ConfiguredViewId), "加载中取消用例没有进入加载中状态");
        inFlightCancellation.Cancel();
        OperationCanceledException inFlightException =
            await AssertThrowsAsync<OperationCanceledException>(
                () => inFlightOpen,
                "加载中的 UI 请求没有响应取消");
        Assert(
            inFlightException.CancellationToken == inFlightCancellation.Token,
            "加载中取消异常没有保留调用方令牌");
        Assert(!_ui.IsOpening(ConfiguredViewId), "取消后没有清理加载中状态");
        Assert(!_ui.IsOpen(ConfiguredViewId), "取消后仍然打开了 UI");

        using var sharedCancellation = new CancellationTokenSource();
        Task<Control> canceledModal =
            _ui.OpenAsync<Control>(
                ConfigModalId,
                cancellationToken: sharedCancellation.Token);
        Task<Control> survivingModal = _ui.OpenAsync<Control>(ConfigModalId);
        Assert(_ui.GetOpeningCount(ConfigModalId) == 2, "共享加载请求数量错误");
        await Task.Run(sharedCancellation.Cancel);
        await AssertThrowsAsync<OperationCanceledException>(
            () => canceledModal,
            "后台线程没有取消指定 UI 请求");
        Control survivor = await survivingModal;
        Assert(
            _ui.GetOpeningCount(ConfigModalId) == 0,
            "共享加载完成后请求数量没有归零");
        Assert(_ui.GetOpenCount(ConfigModalId) == 1, "取消影响了共享加载中的其他请求");
        _ui.Close(survivor);
        await NextFrame();

        using var rawCancellation = new CancellationTokenSource();
        Task<UiConfigurableControl> rawOpen =
            _ui.OpenAsync<UiConfigurableControl>(
                ResourceKey.Create("res://Verification/Automated/Fixtures/UI/UiConfigurableControl.tscn"),
                UiLayer.Overlay,
                cancellationToken: rawCancellation.Token);
        rawCancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(
            () => rawOpen,
            "ResourceKey 异步打开没有响应取消");
        Assert(!_ui.TryGetTop(UiLayer.Overlay, out _), "取消的 ResourceKey 请求仍然挂载了 UI");
        await NextFrame();
    }

    private async Task VerifyManagedOpenCancellation()
    {
        Task<Control> firstModal = _ui.OpenAsync<Control>(ConfigModalId);
        Task<Control> secondModal = _ui.OpenAsync<Control>(ConfigModalId);
        Assert(
            _ui.CancelOpenRequests(ConfigModalId) == 2,
            "按 UiId 取消没有返回全部未完成请求数");
        Assert(
            _ui.CancelOpenRequests(ConfigModalId) == 0,
            "重复按 UiId 取消仍然返回请求数");
        await AssertThrowsAsync<OperationCanceledException>(
            () => firstModal,
            "按 UiId 取消后第一个请求仍然完成");
        await AssertThrowsAsync<OperationCanceledException>(
            () => secondModal,
            "按 UiId 取消后第二个请求仍然完成");
        Assert(!_ui.IsOpening(ConfigModalId), "按 UiId 取消后仍保留加载中状态");
        Assert(!_ui.IsOpen(ConfigModalId), "按 UiId 取消后仍然挂载了界面");

        Task<UiConfigurableControl> canceledView =
            _ui.OpenAsync<UiConfigurableControl>(ConfiguredViewId);
        Task<UiConfigurableControl> survivingScene =
            _ui.OpenAsync<UiConfigurableControl>(ConfiguredSceneId);
        Assert(
            _ui.CancelOpenRequests(UiLayer.View) == 1,
            "按层取消返回了错误的请求数");
        Assert(
            _ui.CancelOpenRequests(UiLayer.View) == 0,
            "重复按层取消仍然返回请求数");
        await AssertThrowsAsync<OperationCanceledException>(
            () => canceledView,
            "按层取消后目标层请求仍然完成");
        UiConfigurableControl sceneView = await survivingScene;
        Assert(
            _ui.IsOpen(ConfiguredSceneId),
            "按层取消影响了共享资源加载中的其他层请求");
        _ui.Close(sceneView);
        await NextFrame();

        Task<Control> directOverlay =
            _ui.OpenAsync<Control>(ControlAKey, UiLayer.Overlay);
#if DEBUG
        UiDebugSnapshot directOpeningSnapshot = ((UiService)_ui).GetDebugSnapshot();
        Assert(
            directOpeningSnapshot.Openings.Length == 1 &&
            !directOpeningSnapshot.Openings[0].Id.IsValid &&
            directOpeningSnapshot.Openings[0].Layer == UiLayer.Overlay &&
            directOpeningSnapshot.Openings[0].Key == ControlAKey &&
            directOpeningSnapshot.Openings[0].RequestCount == 1,
            "Debug 快照没有记录 ResourceKey 直接打开请求");
#endif
        Assert(
            _ui.CancelOpenRequests(UiLayer.Overlay) == 1,
            "按层取消遗漏了 ResourceKey 直接打开请求");
        await AssertThrowsAsync<OperationCanceledException>(
            () => directOverlay,
            "ResourceKey 直接打开请求没有响应按层取消");
        Assert(
            !_ui.TryGetTop(UiLayer.Overlay, out _),
            "按层取消后 ResourceKey 直接请求仍然挂载了界面");
#if DEBUG
        Assert(
            ((UiService)_ui).GetDebugSnapshot().Openings.Length == 0,
            "ResourceKey 直接请求取消后仍保留 Debug 快照");
#endif

        AssertThrows<System.Collections.Generic.KeyNotFoundException>(
            () => _ui.CancelOpenRequests(UiId.Create("missing")),
            "取消异步打开静默接受了未注册标识");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ui.CancelOpenRequests((UiLayer)999),
            "取消异步打开静默接受了未知层");
    }

    private async Task VerifySpecifiedAndBatchClose()
    {
        var unmanaged = new Control();
        try
        {
            Assert(!_ui.TryClose(unmanaged), "TryClose 错误地关闭了非托管界面");
        }
        finally
        {
            unmanaged.Free();
        }

        Control configuredView = _ui.Open(ConfigViewId);
        Assert(_ui.TryClose(ConfigViewId), "TryClose(UiId) 没有关闭已打开界面");
        Assert(!_ui.TryClose(ConfigViewId), "TryClose(UiId) 重复关闭返回 true");
        Assert(!_ui.TryClose(configuredView), "TryClose(Control) 重复关闭返回 true");
        Assert(_ui.CloseAll(ConfigViewId) == 0, "CloseAll(UiId) 在没有实例时返回非零");
        AssertThrows<System.Collections.Generic.KeyNotFoundException>(
            () => _ui.TryClose(UiId.Create("missing")),
            "TryClose(UiId) 静默接受了未注册标识");
        await NextFrame();

        Control firstModal = _ui.Open(ConfigModalId);
        Control secondModal = _ui.Open(ConfigModalId);
        Control thirdModal = _ui.Open(ConfigModalId);
        Assert(_ui.TryClose(ConfigModalId), "TryClose(UiId) 没有关闭最上层同标识实例");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(thirdModal), "TryClose(UiId) 没有选择最上层实例");
        Assert(GodotObject.IsInstanceValid(firstModal) && GodotObject.IsInstanceValid(secondModal),
            "TryClose(UiId) 影响了其他同标识实例");
        Assert(_ui.CloseAll(ConfigModalId) == 2, "CloseAll(UiId) 返回了错误关闭数量");
        await NextFrame();

        Control firstScene = _ui.Open(ControlAKey, UiLayer.Scene);
        Control secondScene = _ui.Open(ControlBKey, UiLayer.Scene);
        Control firstView = _ui.Open(ControlAKey, UiLayer.View);
        Control secondView = _ui.Open(ControlBKey, UiLayer.View);
        Control rawModal = _ui.Open(ControlAKey, UiLayer.Modal);

        Assert(_ui.CloseAll(UiLayer.View) == 2, "CloseAll(View) 返回了错误关闭数量");
        Assert(GodotObject.IsInstanceValid(rawModal), "CloseAll(View) 错误地关闭了 Modal");
        Assert(_ui.CloseAll(UiLayer.Modal) == 1, "CloseAll(Modal) 返回了错误关闭数量");
        Assert(_ui.CloseAll(UiLayer.Scene) == 2, "CloseAll(Scene) 返回了错误关闭数量");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ui.CloseAll((UiLayer)999),
            "CloseAll(UiLayer) 静默接受了未知层");
        await NextFrame();

        Assert(!GodotObject.IsInstanceValid(firstScene) &&
               !GodotObject.IsInstanceValid(secondScene) &&
               !GodotObject.IsInstanceValid(firstView) &&
               !GodotObject.IsInstanceValid(secondView) &&
               !GodotObject.IsInstanceValid(rawModal),
            "CloseAll(UiLayer) 遗留了界面");
        Assert(!_ui.TryGoBack(), "批量关闭后返回栈没有清空");
    }

    private async Task VerifyQueries()
    {
        Assert(!_ui.IsOpen(ConfigModalId), "未打开 UiId 错误地返回 IsOpen=true");
        Assert(_ui.GetOpenCount(ConfigModalId) == 0, "未打开 UiId 的计数不是 0");
        Assert(!_ui.TryGetTop(ConfigModalId, out Control? missing) && missing is null,
            "未打开 UiId 错误地返回了顶部实例");
        AssertThrows<System.Collections.Generic.KeyNotFoundException>(
            () => _ui.IsOpen(UiId.Create("missing")),
            "IsOpen 静默接受了未注册 UiId");

        Control firstModal = _ui.Open(ConfigModalId);
        Control secondModal = _ui.Open(ConfigModalId);
        Assert(_ui.IsOpen(ConfigModalId), "已打开 UiId 返回 IsOpen=false");
        Assert(_ui.GetOpenCount(ConfigModalId) == 2, "UiId 打开数量查询错误");
        Assert(_ui.TryGetTop(ConfigModalId, out Control? topById) && topById == secondModal,
            "TryGetTop(UiId) 没有返回最上层同标识实例");
        Assert(_ui.TryGetTop(UiLayer.Modal, out Control? topModal) && topModal == secondModal,
            "TryGetTop(Modal) 没有返回顶部 Modal");

        Control firstScene = _ui.Open(ControlAKey, UiLayer.Scene);
        Control secondScene = _ui.Open(ControlBKey, UiLayer.Scene);
        Assert(_ui.TryGetTop(UiLayer.Scene, out Control? topScene) && topScene == secondScene,
            "TryGetTop(Scene) 没有返回最后打开的 Scene 界面");
        Assert(!_ui.TryGetTop(UiLayer.View, out Control? topView) && topView is null,
            "空 View 层错误地返回了顶部实例");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ui.TryGetTop((UiLayer)999, out _),
            "TryGetTop(UiLayer) 静默接受了未知层");

        Assert(_ui.TryClose(secondModal), "查询后无法关闭顶部实例");
        Assert(_ui.TryGetTop(ConfigModalId, out topById) && topById == firstModal,
            "关闭顶部后 UiId 查询没有回退到前一实例");
        Assert(_ui.CloseAll(ConfigModalId) == 1, "查询后批量关闭 UiId 失败");
        Assert(_ui.CloseAll(UiLayer.Scene) == 2, "查询后批量关闭 Scene 失败");
        Assert(
            firstScene.IsQueuedForDeletion() && secondScene.IsQueuedForDeletion(),
            "查询用 Scene 界面没有进入释放队列");
        await NextFrame();
        await NextFrame();

        Assert(!GodotObject.IsInstanceValid(firstScene) &&
               !GodotObject.IsInstanceValid(secondScene),
            "查询用 Scene 界面没有清理");
    }

    private async Task VerifyCloseTo()
    {
        AssertThrows<InvalidOperationException>(
            () => _ui.CloseTo(ConfigViewId),
            "CloseTo(UiId) 静默接受了未打开目标");
        var unmanaged = new Control();
        try
        {
            AssertThrows<InvalidOperationException>(
                () => _ui.CloseTo(unmanaged),
                "CloseTo(Control) 静默接受了非托管目标");
        }
        finally
        {
            unmanaged.Free();
        }

        Control targetView = _ui.Open(ConfigViewId);
        Control upperView = _ui.Open(ControlBKey, UiLayer.View);
        Control upperModal = _ui.Open(ControlAKey, UiLayer.Modal);
        Control upperOverlay = _ui.Open(ControlBKey, UiLayer.Overlay);

        Assert(_ui.CloseTo(ConfigViewId) == 3, "CloseTo(UiId) 没有关闭目标上方 View、Modal 和 Overlay");
        Assert(GodotObject.IsInstanceValid(targetView) && targetView.Visible,
            "CloseTo(UiId) 没有保留并恢复目标 View");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(upperView) &&
               !GodotObject.IsInstanceValid(upperModal) &&
               !GodotObject.IsInstanceValid(upperOverlay),
            "CloseTo(UiId) 遗留了上层界面");
        _ui.Close(targetView);
        await NextFrame();

        Control lowerModal = _ui.Open(ControlAKey, UiLayer.Modal);
        Control higherModal = _ui.Open(ControlBKey, UiLayer.Modal);
        Control modalOverlay = _ui.Open(ControlAKey, UiLayer.Overlay);
        Assert(_ui.CloseTo(lowerModal) == 2, "CloseTo(Modal) 返回了错误关闭数量");
        Assert(GodotObject.IsInstanceValid(lowerModal), "CloseTo(Modal) 错误地关闭了目标");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(higherModal) &&
               !GodotObject.IsInstanceValid(modalOverlay),
            "CloseTo(Modal) 没有关闭更高层 Modal 和 Overlay");
        _ui.Close(lowerModal);
        await NextFrame();

        Control targetScene = _ui.Open(ControlAKey, UiLayer.Scene);
        Control sceneView = _ui.Open(ControlAKey, UiLayer.View);
        Control sceneModal = _ui.Open(ControlBKey, UiLayer.Modal);
        Control sceneOverlay = _ui.Open(ControlAKey, UiLayer.Overlay);
        Assert(_ui.CloseTo(targetScene) == 3, "CloseTo(Scene) 没有关闭 View、Modal 和 Overlay");
        Assert(GodotObject.IsInstanceValid(targetScene), "CloseTo(Scene) 错误地关闭了目标");
        await NextFrame();
        Assert(!GodotObject.IsInstanceValid(sceneView) &&
               !GodotObject.IsInstanceValid(sceneModal) &&
               !GodotObject.IsInstanceValid(sceneOverlay),
            "CloseTo(Scene) 遗留了上层界面");
        _ui.Close(targetScene);
        await NextFrame();
    }

    private async Task VerifyFocusBehavior()
    {
        Viewport viewport = GetViewport();
        viewport.GuiReleaseFocus();

        Button firstViewButton = null!;
        Control firstView = _ui.Open<Control>(
            ControlAKey,
            UiLayer.View,
            view => firstViewButton = AddFocusButton(view, "FirstViewFocus"));
        firstViewButton.GrabFocus();
        Assert(viewport.GuiGetFocusOwner() == firstViewButton,
            "View 按钮无法获得键盘/手柄焦点");

        Button secondViewButton = null!;
        Control secondView = _ui.Open<Control>(
            ControlBKey,
            UiLayer.View,
            view => secondViewButton = AddFocusButton(view, "SecondViewFocus"));
        Assert(viewport.GuiGetFocusOwner() is null,
            "隐藏前一个 View 后仍保留其键盘/手柄焦点");

        secondViewButton.GrabFocus();
        _ui.Close(secondView);
        await NextFrame();
        bool viewFocusRestored = viewport.GuiGetFocusOwner() == firstViewButton;
        Assert(viewFocusRestored,
            "关闭顶部 View 后没有恢复下层 View 焦点");
        _ui.Close(firstView);
        await NextFrame();

        Button lowerViewButton = null!;
        Control lowerView = _ui.Open<Control>(
            ControlAKey,
            UiLayer.View,
            view => lowerViewButton = AddFocusButton(view, "LowerViewFocus"));
        lowerViewButton.GrabFocus();
        Button topViewButton = null!;
        Control topView = _ui.Open<Control>(
            ControlBKey,
            UiLayer.View,
            view => topViewButton = AddFocusButton(view, "TopViewFocus"));
        topViewButton.GrabFocus();
        _ui.Close(lowerView);
        Assert(viewport.GuiGetFocusOwner() == topViewButton,
            "关闭非顶部 View 干扰了当前顶部 View 焦点");
        _ui.Close(topView);
        await NextFrame();

        Button invalidFocusButton = null!;
        Control invalidFocusView = _ui.Open<Control>(
            ControlAKey,
            UiLayer.View,
            view => invalidFocusButton = AddFocusButton(view, "InvalidRestoreFocus"));
        invalidFocusButton.GrabFocus();
        Control invalidFocusCover = _ui.Open(ControlBKey, UiLayer.View);
        invalidFocusButton.QueueFree();
        await NextFrame();
        _ui.Close(invalidFocusCover);
        Assert(viewport.GuiGetFocusOwner() is null,
            "关闭顶部 View 时尝试恢复了已释放的焦点控件");
        _ui.Close(invalidFocusView);
        await NextFrame();

        Button disabledFocusButton = null!;
        Control disabledFocusView = _ui.Open<Control>(
            ControlAKey,
            UiLayer.View,
            view => disabledFocusButton = AddFocusButton(view, "DisabledRestoreFocus"));
        disabledFocusButton.GrabFocus();
        Control disabledFocusCover = _ui.Open(ControlBKey, UiLayer.View);
        disabledFocusButton.FocusMode = Control.FocusModeEnum.None;
        _ui.Close(disabledFocusCover);
        Assert(viewport.GuiGetFocusOwner() is null,
            "关闭顶部 View 时恢复了不可聚焦控件");
        _ui.Close(disabledFocusView);
        await NextFrame();

        Button coveredCachedButton = null!;
        UiConfigurableControl coveredCached = _ui.Open<UiConfigurableControl>(
            ReusableViewId,
            view => coveredCachedButton = AddFocusButton(view, "CoveredCachedFocus"));
        coveredCachedButton.GrabFocus();
        _ui.Close(coveredCached);
        Assert(_ui.HasCachedInstance(ReusableViewId),
            "Modal 下层焦点实验没有准备好缓存 View");

        Button backgroundButton = null!;
        Control backgroundView = _ui.Open<Control>(
            ControlAKey,
            UiLayer.View,
            view => backgroundButton = AddFocusButton(view, "BackgroundFocus"));
        backgroundButton.GrabFocus();

        Button modalButton = null!;
        Control modal = _ui.Open<Control>(
            ControlBKey,
            UiLayer.Modal,
            view => modalButton = AddFocusButton(view, "ModalFocus"));
        bool modalKeptBackgroundFocus = viewport.GuiGetFocusOwner() == backgroundButton;
        Assert(!modalKeptBackgroundFocus && viewport.GuiGetFocusOwner() is null,
            "打开 Modal 后没有隔离背景 View 焦点");

        modalButton.GrabFocus();
        Control coveredView = _ui.Open(ControlAKey, UiLayer.View);
        Assert(viewport.GuiGetFocusOwner() == modalButton,
            "Modal 下方打开 View 改变了顶部 Modal 焦点");
        _ui.Close(coveredView);
        Assert(viewport.GuiGetFocusOwner() == modalButton,
            "Modal 下方关闭 View 改变了顶部 Modal 焦点");

        UiConfigurableControl reopenedCoveredCache =
            _ui.Open<UiConfigurableControl>(ReusableViewId);
        Assert(viewport.GuiGetFocusOwner() == modalButton,
            "Modal 下方重开缓存 View 抢走了顶部 Modal 焦点");
        _ui.Close(reopenedCoveredCache);
        Assert(viewport.GuiGetFocusOwner() == modalButton,
            "Modal 下方关闭缓存 View 改变了顶部 Modal 焦点");
        Assert(_ui.ClearCachedInstance(ReusableViewId),
            "Modal 下层焦点实验没有清理缓存 View");

        _ui.Close(modal);
        await NextFrame();
        bool modalFocusRestored = viewport.GuiGetFocusOwner() == backgroundButton;
        Assert(modalFocusRestored,
            "关闭 Modal 后没有恢复背景 View 焦点");
        _ui.Close(backgroundView);
        await NextFrame();

        Button unmanagedButton = AddFocusButton(this, "UnmanagedFocus");
        unmanagedButton.GrabFocus();
        Control unmanagedFocusModal = _ui.Open(ControlAKey, UiLayer.Modal);
        Assert(viewport.GuiGetFocusOwner() == unmanagedButton,
            "打开 Modal 错误抢走了非 UiService 管理的焦点");
        _ui.Close(unmanagedFocusModal);
        Assert(viewport.GuiGetFocusOwner() == unmanagedButton,
            "关闭 Modal 错误改变了非 UiService 管理的焦点");
        unmanagedButton.QueueFree();
        await NextFrame();

        Button cachedButton = null!;
        UiConfigurableControl cached = _ui.Open<UiConfigurableControl>(
            ReusableViewId,
            view => cachedButton = AddFocusButton(view, "CachedFocus"));
        cachedButton.GrabFocus();
        _ui.Close(cached);
        Assert(viewport.GuiGetFocusOwner() is null,
            "复用 UI 脱树缓存后仍保留焦点");

        UiConfigurableControl reopened = _ui.Open<UiConfigurableControl>(ReusableViewId);
        bool cachedFocusRestored = viewport.GuiGetFocusOwner() == cachedButton;
        Assert(cachedFocusRestored,
            "缓存 UI 重开时没有恢复关闭前焦点");
        _ui.Close(reopened);
        Assert(_ui.ClearCachedInstance(ReusableViewId), "焦点实验没有清理复用缓存");
        await NextFrame();

        Button releasedButton = null!;
        Control externallyReleased = _ui.Open<Control>(
            ControlAKey,
            UiLayer.View,
            view => releasedButton = AddFocusButton(view, "ReleasedFocus"));
        releasedButton.GrabFocus();
        externallyReleased.QueueFree();
        await NextFrame();
        Assert(viewport.GuiGetFocusOwner() is null,
            "外部释放 UI 后仍保留失效焦点");

        GD.Print(
            $"[UiServiceRegression] Focus behavior: " +
            $"ViewRestore={viewFocusRestored}; " +
            $"ModalKeptBackground={modalKeptBackgroundFocus}; " +
            $"ModalRestore={modalFocusRestored}; " +
            $"CachedRestore={cachedFocusRestored}");
        viewport.GuiReleaseFocus();
    }

    private static Button AddFocusButton(Node parent, string name)
    {
        var button = new Button
        {
            Name = name,
            Text = name,
            FocusMode = Control.FocusModeEnum.All
        };
        parent.AddChild(button);
        return button;
    }

#if DEBUG
    private static int CountEntries(UiDebugSnapshot snapshot, UiLayer layer)
    {
        int count = 0;
        for (int i = 0; i < snapshot.Entries.Length; i++)
        {
            if (snapshot.Entries[i].Layer == layer)
                count++;
        }

        return count;
    }
#endif

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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

    private static async Task<TException> AssertThrowsAsync<TException>(
        Func<Task> action,
        string message)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }
}
