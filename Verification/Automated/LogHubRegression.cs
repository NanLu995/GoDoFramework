using System;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>LogHub 的无交互回归验证入口。</summary>
public sealed partial class LogHubRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("Debug 格式", VerifyDebugFormat);
            Run("Info 格式", VerifyInfoFormat);
            Run("空参数拒绝", VerifyInvalidArguments);
            Run("控制台输出", VerifyConsoleOutput);
#if DEBUG
            Run("环形历史", VerifyDebugHistory);
#endif

            GD.Print($"[LogHubRegression] PASS ({_passed})");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[LogHubRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[LogHubRegression] PASS: {name}");
    }

    private static void VerifyDebugFormat()
    {
        string formatted = LogHub.FormatForConsole(LogLevel.Debug, "状态已更新", "Gameplay", "score=3");
        AssertEqual("[Gameplay] [DEBUG] (score=3) 状态已更新", formatted, "Debug 格式不符合约定");
    }

    private static void VerifyInfoFormat()
    {
        string formatted = LogHub.FormatForConsole(LogLevel.Info, "进入主菜单", "Procedure");
        AssertEqual("[Procedure] [INFO] 进入主菜单", formatted, "Info 格式不符合约定");
    }

    private static void VerifyInvalidArguments()
    {
        AssertThrows<ArgumentException>(
            static () => LogHub.FormatForConsole(LogLevel.Info, string.Empty, "Procedure"),
            "空消息没有被拒绝");
        AssertThrows<ArgumentException>(
            static () => LogHub.FormatForConsole(LogLevel.Info, "进入主菜单", " "),
            "空模块没有被拒绝");
    }

    private static void VerifyConsoleOutput()
    {
        LogHub.Debug("调试输出", "LogHubRegression");
        LogHub.Info("信息输出", "LogHubRegression");
    }

#if DEBUG
    private static void VerifyDebugHistory()
    {
        LogHub.Initialize();
        for (int i = 0; i <= LogHub.DebugHistoryCapacity; i++)
            LogHub.Info($"entry={i}", "LogHubRegression");

        LogEntry[] snapshot = LogHub.GetDebugSnapshot();
        AssertEqual(LogHub.DebugHistoryCapacity, snapshot.Length, "日志历史容量错误");
        AssertEqual("entry=1", snapshot[0].Message, "环形历史没有淘汰最早条目");
        AssertEqual(
            $"entry={LogHub.DebugHistoryCapacity}",
            snapshot[^1].Message,
            "环形历史没有保留最新条目");
    }
#endif

    private static void AssertEqual(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidOperationException($"{message}；期望 {expected}，实际 {actual}");
    }

    private static void AssertEqual(int expected, int actual, string message)
    {
        if (expected != actual)
            throw new InvalidOperationException($"{message}；期望 {expected}，实际 {actual}");
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
