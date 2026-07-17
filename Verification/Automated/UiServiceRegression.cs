using System;
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

    private IUiService _ui = null!;
    private int _passed;

    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            _ui = Services.Get<IUiService>();

            Run("空返回栈", VerifyEmptyBackStack);
            await RunAsync("Scene 层并行界面", VerifySceneLayer);
            await RunAsync("View 返回栈", VerifyViewStack);
            await RunAsync("Modal 优先级与 Host", VerifyModalStack);
            await RunAsync("失败后状态保持", VerifyFailureSemantics);
            await RunAsync("主场景变更清理 Scene 层", VerifySceneChangeCleanup);

            GD.Print($"[UiServiceRegression] PASS ({_passed}/6)");
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
        AssertThrows<InvalidOperationException>(
            () => _ui.Close(first),
            "非顶部 View 可以被直接关闭");

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
        AssertThrows<InvalidOperationException>(
            () => _ui.Close(firstModal),
            "存在更高层 Modal 时可以关闭下层 Modal");

        Assert(_ui.TryGoBack(), "顶部 Modal 没有优先响应 TryGoBack");
        Assert(_ui.TryGoBack(), "第二个 Modal 没有响应 TryGoBack");
        Assert(GodotObject.IsInstanceValid(view) && view.Visible, "关闭 Modal 影响了当前 View");
        Assert(_ui.TryGoBack(), "Modal 清空后没有返回当前 View");
        await NextFrame();

        Assert(!GodotObject.IsInstanceValid(secondModal), "顶部 Modal 没有释放");
        Assert(!GodotObject.IsInstanceValid(firstModal), "下层 Modal 没有释放");
        Assert(!GodotObject.IsInstanceValid(view), "View 没有释放");
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
}
