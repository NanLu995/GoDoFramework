using System;
using System.Threading.Tasks;
using Godot;
using GoDo;

#nullable enable

namespace GoDoTemplate.Verification;

/// <summary>验证可复制 Starter Template 的菜单、Gameplay 与 UI 生命周期主链路。</summary>
public sealed partial class StarterUiFlowRegression : Node
{
    private IProcedureService _procedures = null!;
    private IUiService _ui = null!;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            if (GetTree().CurrentScene == this)
                GetTree().CurrentScene = null;

            _procedures = Services.Get<IProcedureService>();
            _ui = Services.Get<IUiService>();

            await _procedures.ChangeAsync(new BootstrapProcedure());
            await WaitForProcedure<MainMenuProcedure>();
            await VerifyMainMenuAndSettings();
            await VerifyLoadingContract();
            await VerifyGameplayFlow();
            await _procedures.ChangeAsync(new EmptyProcedure());
            await NextFrame();
            VerifyNoOwnedUiRemains();

            GD.Print("[StarterUiFlowRegression] PASS (7/7)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GetTree().Paused = false;
            GD.PushError($"[StarterUiFlowRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task VerifyMainMenuAndSettings()
    {
        MainMenuView mainMenu = GetTop<MainMenuView>(StarterKeys.MainMenuView);
        await NextFrame();
        Button startButton = mainMenu.GetNode<Button>(mainMenu.StartButtonPath);
        Assert(GetViewport().GuiGetFocusOwner() == startButton, "MainMenu 没有把初始焦点交给 Start");

        EventChannel.Emit<SettingsSelectedEvent>();
        await WaitForOpen(StarterKeys.SettingsView);
        SettingsView settings = GetTop<SettingsView>(StarterKeys.SettingsView);
        await NextFrame();
        Assert(
            GetViewport().GuiGetFocusOwner() == settings.GetNode<HSlider>(settings.MasterSliderPath),
            "Settings 没有把初始焦点交给 Master Slider");

        EventChannel.Emit<SettingsSelectedEvent>();
        await NextFrame();
        Assert(_ui.GetOpenCount(StarterKeys.SettingsView) == 1, "重复设置请求打开了多个 Single View");

        EventChannel.Emit<SettingsCloseSelectedEvent>();
        await WaitForClosed(StarterKeys.SettingsView);
        Assert(_ui.TryGetTop(StarterKeys.MainMenuView, out _), "关闭 Settings 后没有返回 MainMenu");
        Assert(GetViewport().GuiGetFocusOwner() == startButton, "关闭 Settings 后没有恢复 MainMenu 焦点");
    }

    private async Task VerifyLoadingContract()
    {
        int cancellationCount = 0;
        LoadingOverlay loading = _ui.Open<LoadingOverlay>(
            StarterKeys.LoadingOverlay,
            view =>
            {
                view.SetProgress(0.42f);
                view.SetCancelAction(() => cancellationCount++);
            });
        await NextFrame();

        Label progress = loading.GetNode<Label>(loading.ProgressLabelPath);
        Button cancel = loading.GetNode<Button>(loading.CancelButtonPath);
        Assert(progress.Text == "Loading 42%", "Loading 没有显示请求级进度");
        cancel.EmitSignal(BaseButton.SignalName.Pressed);
        cancel.EmitSignal(BaseButton.SignalName.Pressed);
        Assert(cancellationCount == 1 && cancel.Disabled, "Loading 取消没有执行一次性回调并禁用按钮");
        AssertThrows<ArgumentOutOfRangeException>(
            () => loading.SetProgress(float.NaN),
            "Loading 接受了非有限进度");
        _ui.Close(loading);
        await NextFrame();
    }

    private async Task VerifyGameplayFlow()
    {
        EventChannel.Emit<StartGameSelectedEvent>();
        await WaitForProcedure<GameplayProcedure>();
        Assert(_ui.GetOpenCount(StarterKeys.GameplayHud) == 1, "Gameplay 没有打开 HUD Scene UI");

        ToastOverlay firstToast = GetTop<ToastOverlay>(StarterKeys.ToastOverlay);
        int existingToastCount = _ui.GetOpenCount(StarterKeys.ToastOverlay);
        ToastOverlay secondToast = _ui.Open<ToastOverlay>(
            StarterKeys.ToastOverlay,
            toast => toast.Show("Second toast", existingToastCount));
        AssertThrows<ArgumentOutOfRangeException>(
            () => secondToast.Show("Invalid stack", -1),
            "Toast 接受了负堆叠序号");
        await NextFrame();
        Control firstMargin = firstToast.GetNode<Control>(firstToast.StackContainerPath);
        Control secondMargin = secondToast.GetNode<Control>(secondToast.StackContainerPath);
        Assert(secondMargin.OffsetTop < firstMargin.OffsetTop, "多个 Toast 仍重叠在同一位置");

        EventChannel.Emit<PauseSelectedEvent>();
        await WaitForOpen(StarterKeys.PauseModal);
        PauseModal pause = GetTop<PauseModal>(StarterKeys.PauseModal);
        await NextFrame();
        Assert(GetTree().Paused, "Pause Modal 打开后 SceneTree 未暂停");
        Assert(
            GetViewport().GuiGetFocusOwner() == pause.GetNode<Button>(pause.ResumeButtonPath),
            "Pause 没有把初始焦点交给 Resume");

        EventChannel.Emit<PauseSettingsSelectedEvent>();
        await WaitForClosed(StarterKeys.PauseModal);
        await WaitForOpen(StarterKeys.SettingsView);
        Assert(GetTree().Paused, "Pause→Settings 期间错误恢复了 SceneTree");

        EventChannel.Emit<SettingsCloseSelectedEvent>();
        await WaitForClosed(StarterKeys.SettingsView);
        await WaitForOpen(StarterKeys.PauseModal);
        Assert(GetTree().Paused, "Settings→Pause 返回后 SceneTree 未保持暂停");

        EventChannel.Emit<ReturnToMainMenuSelectedEvent>();
        await WaitForOpen(StarterKeys.ConfirmDialog);
        ConfirmDialogModal confirm = GetTop<ConfirmDialogModal>(StarterKeys.ConfirmDialog);
        await NextFrame();
        Assert(
            GetViewport().GuiGetFocusOwner() == confirm.GetNode<Button>(confirm.CancelButtonPath),
            "Confirm 没有把安全默认焦点交给 Cancel");
        EventChannel.Emit<ReturnToMainMenuSelectedEvent>();
        Assert(_ui.GetOpenCount(StarterKeys.ConfirmDialog) == 1, "重复返回请求打开了多个 Confirm");

        EventChannel.Emit<ConfirmCancelledEvent>();
        await WaitForClosed(StarterKeys.ConfirmDialog);
        Assert(_ui.IsOpen(StarterKeys.PauseModal), "取消 Confirm 后 Pause 没有保留");

        EventChannel.Emit<ReturnToMainMenuSelectedEvent>();
        await WaitForOpen(StarterKeys.ConfirmDialog);
        LoadingOverlay externalLoading = _ui.Open<LoadingOverlay>(
            StarterKeys.LoadingOverlay,
            view => view.SetProgress(0.75f));
        EventChannel.Emit<ConfirmAcceptedEvent>();
        await WaitForProcedure<MainMenuProcedure>();
        Assert(!GetTree().Paused, "返回主菜单后 SceneTree 仍暂停");
        AssertUiSettledClosed(StarterKeys.GameplayHud, "Gameplay 退出后遗留 HUD");
        AssertUiSettledClosed(StarterKeys.PauseModal, "Gameplay 退出后遗留 Pause");
        AssertUiSettledClosed(StarterKeys.ConfirmDialog, "Gameplay 退出后遗留 Confirm");
        AssertUiSettledClosed(StarterKeys.ToastOverlay, "Gameplay 退出后遗留 Toast");
        Assert(
            _ui.TryGetTop(StarterKeys.LoadingOverlay, out Control? survivingLoading) &&
            survivingLoading == externalLoading,
            "Gameplay 退出错误关闭了不属于该流程的 Loading");
        _ui.Close(externalLoading);
        await WaitForClosed(StarterKeys.LoadingOverlay);
    }

    private void VerifyNoOwnedUiRemains()
    {
        AssertUiSettledClosed(StarterKeys.MainMenuView, "空流程仍遗留 MainMenu");
        AssertUiSettledClosed(StarterKeys.SettingsView, "空流程仍遗留 Settings");
        AssertUiSettledClosed(StarterKeys.GameplayHud, "空流程仍遗留 HUD");
        AssertUiSettledClosed(StarterKeys.PauseModal, "空流程仍遗留 Pause");
        AssertUiSettledClosed(StarterKeys.ConfirmDialog, "空流程仍遗留 Confirm");
        AssertUiSettledClosed(StarterKeys.LoadingOverlay, "空流程仍遗留 Loading");
        AssertUiSettledClosed(StarterKeys.ToastOverlay, "空流程仍遗留 Toast");
    }

    private void AssertUiSettledClosed(UiId id, string message)
    {
        Assert(
            _ui.GetOpenCount(id) == 0 && _ui.GetOpeningCount(id) == 0,
            $"{message}（open={_ui.GetOpenCount(id)}, opening={_ui.GetOpeningCount(id)}）");
    }

    private TView GetTop<TView>(UiId id) where TView : Control
    {
        Assert(_ui.TryGetTop(id, out Control? view), $"找不到已打开 UI: {id}");
        return view as TView ?? throw new InvalidOperationException($"UI {id} 类型不是 {typeof(TView).Name}");
    }

    private async Task WaitForProcedure<TProcedure>() where TProcedure : IProcedure
    {
        for (int frame = 0; frame < 180; frame++)
        {
            if (!_procedures.IsChanging && _procedures.Current is TProcedure)
                return;
            await NextFrame();
        }

        throw new TimeoutException($"等待 Procedure {typeof(TProcedure).Name} 超时。");
    }

    private async Task WaitForOpen(UiId id)
    {
        for (int frame = 0; frame < 180; frame++)
        {
            if (_ui.IsOpen(id) && !_ui.IsOpening(id))
                return;
            await NextFrame();
        }

        throw new TimeoutException($"等待 UI {id} 打开超时。");
    }

    private async Task WaitForClosed(UiId id)
    {
        for (int frame = 0; frame < 180; frame++)
        {
            if (!_ui.IsOpen(id) && !_ui.IsOpening(id))
                return;
            await NextFrame();
        }

        throw new TimeoutException($"等待 UI {id} 关闭超时。");
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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

    private sealed class EmptyProcedure : IProcedure
    {
        public string Name => "StarterUiRegressionEmpty";

        public Task EnterAsync(ProcedureContext context) => Task.CompletedTask;

        public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;
    }
}
