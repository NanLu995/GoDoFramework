using System;
using Godot;

namespace GoDoFramework.Verification;

/// <summary>验证 Debugger 二阶段折叠状态与两层页面导航。</summary>
public sealed partial class DebuggerOverlayRegression : Node
{
    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
#if DEBUG
            CanvasLayer overlay = GetNode<CanvasLayer>("/root/GoDoRuntime/GoDoDebugger");
            Button toggle = overlay.GetNode<Button>("Panel/Margin/VBox/Header/FpsButton");
            Label title = overlay.GetNode<Label>("Panel/Margin/VBox/Header/TitleLabel");
            TabBar categories = overlay.GetNode<TabBar>("Panel/Margin/VBox/CategoryTabs");
            TabBar pages = overlay.GetNode<TabBar>("Panel/Margin/VBox/PageTabs");
            ScrollContainer content = overlay.GetNode<ScrollContainer>("Panel/Margin/VBox/Content");
            Label debuggerLabel = overlay.GetNode<Label>("Panel/Margin/VBox/Content/DebuggerLabel");

            Assert(toggle.FocusMode == Control.FocusModeEnum.None &&
                categories.FocusMode == Control.FocusModeEnum.None &&
                pages.FocusMode == Control.FocusModeEnum.None &&
                content.FocusMode == Control.FocusModeEnum.None,
                "Debugger 可交互节点仍会抢占键盘或手柄焦点");
            Assert(!content.Visible && !categories.Visible && !title.Visible, "Debugger 默认未折叠");
            toggle.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(content.Visible && categories.Visible && title.Visible, "Debugger 点击后未展开");
            Assert(categories.TabCount == 4, "Debugger 一级分类数量错误");
            Assert(categories.GetTabTitle(0) == "概览", "Debugger 默认分类错误");
            Assert(!debuggerLabel.Text.Contains('\r'), "Debugger 文本仍包含 Windows CR 换行");
            Assert(debuggerLabel.Text.Contains("【运行时】\n主线程", StringComparison.Ordinal),
                "同模块标题与信息之间仍有空行");
            Assert(debuggerLabel.Text.Contains("\n\n【场景】", StringComparison.Ordinal),
                "模块之间没有保留一行间距");
            Assert(!debuggerLabel.Text.Contains("\n\n\n", StringComparison.Ordinal),
                "模块之间出现了多余空行");

            categories.CurrentTab = 1;
            Assert(pages.Visible && pages.TabCount == 2, "运行时二级页面错误");
            Assert(title.Text == "Input", "运行时默认页面错误");
            pages.CurrentTab = 1;
            Assert(title.Text == "Scheduler", "Scheduler 页面切换失败");
            Assert(debuggerLabel.Text.Contains("【任务】", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("【Process】", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("【Physics】", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("【累计统计】", StringComparison.Ordinal),
                "Scheduler 快照内容没有完整渲染");

            categories.CurrentTab = 2;
            Assert(pages.Visible && pages.TabCount == 2, "框架二级页面错误");
            Assert(title.Text == "Services" &&
                debuggerLabel.Text.Contains("【已注册服务】", StringComparison.Ordinal) &&
                debuggerLabel.Text.Contains("ISchedulerService", StringComparison.Ordinal),
                "Services 快照内容没有完整渲染");
            pages.CurrentTab = 1;
            Assert(title.Text == "Events" &&
                debuggerLabel.Text.Contains("【事件通道】", StringComparison.Ordinal),
                "Events 快照内容没有完整渲染");

            categories.CurrentTab = 3;
            Assert(pages.Visible && pages.TabCount == 5, "控制台过滤页面错误");
            Assert(title.Text == "全部", "控制台默认页面错误");

            toggle.EmitSignal(BaseButton.SignalName.Pressed);
            Assert(!content.Visible && !categories.Visible && !title.Visible, "Debugger 点击后未折叠");

            GD.Print("[DebuggerOverlayRegression] PASS: Debug 节点与快照页面");
#else
            Assert(GetNodeOrNull<Node>("/root/GoDoRuntime/GoDoDebugger") is null,
                "Release 构建仍创建了 Debugger 节点");
            GD.Print("[DebuggerOverlayRegression] PASS: Release 未创建 Debugger");
#endif
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[DebuggerOverlayRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
