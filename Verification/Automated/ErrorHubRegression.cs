using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>ErrorHub 的无交互回归验证入口。</summary>
public sealed partial class ErrorHubRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        ErrorLevel originalMinLevel = ErrorHub.MinLevel;
        try
        {
            Run("最低等级过滤", VerifyMinimumLevel);
            Run("结构化异常报告", VerifyStructuredExceptionReport);
            Run("Reporter 引用去重与移除", VerifyReporterLifecycle);
            Run("OnError 监听者异常隔离", VerifyListenerIsolation);
            Run("Reporter 异常隔离", VerifyReporterIsolation);
            Run("Fatal 只上报不退出", VerifyFatalDoesNotQuit);

            GD.Print($"[ErrorHubRegression] PASS ({_passed}/6)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[ErrorHubRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            ErrorHub.MinLevel = originalMinLevel;
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[ErrorHubRegression] PASS: {name}");
    }

    private static void VerifyMinimumLevel()
    {
        int calls = 0;
        void OnError(ErrorReport _) => calls++;

        ErrorHub.OnError += OnError;
        try
        {
            ErrorHub.MinLevel = ErrorLevel.Error;
            ErrorHub.Warn("filtered", "ErrorHubRegression");
            AssertEqual(0, calls, "低于 MinLevel 的报告仍被分发");

            ErrorHub.Report(ErrorLevel.Error, "accepted", "ErrorHubRegression");
            AssertEqual(1, calls, "达到 MinLevel 的报告没有分发");
        }
        finally
        {
            ErrorHub.OnError -= OnError;
            ErrorHub.MinLevel = ErrorLevel.Warning;
        }
    }

    private static void VerifyStructuredExceptionReport()
    {
        ErrorReport? captured = null;
        var expected = new InvalidOperationException("expected exception");
        void OnError(ErrorReport report) => captured = report;

        ErrorHub.OnError += OnError;
        try
        {
            ErrorHub.MinLevel = ErrorLevel.Warning;
            DateTime before = DateTime.UtcNow;
            ErrorHub.Report(expected, "Regression", context: "Structured");

            if (captured is not ErrorReport report)
                throw new InvalidOperationException("异常报告没有触发 OnError");
            AssertEqual(ErrorLevel.Error, report.Level, "异常报告等级错误");
            AssertEqual("Regression", report.Module, "异常报告模块错误");
            AssertEqual("Structured", report.Context, "异常报告上下文错误");
            AssertEqual(expected.Message, report.Message, "异常报告消息错误");
            Assert(ReferenceEquals(expected, report.Exception), "异常报告没有保留原始异常");
            Assert(report.Timestamp >= before && report.Timestamp <= DateTime.UtcNow, "异常报告 UTC 时间错误");
        }
        finally
        {
            ErrorHub.OnError -= OnError;
        }
    }

    private static void VerifyReporterLifecycle()
    {
        var reporter = new CountingReporter();
        try
        {
            ErrorHub.AddReporter(reporter);
            ErrorHub.AddReporter(reporter);
            ErrorHub.Warn("deduplicated", "ErrorHubRegression");
            AssertEqual(1, reporter.Count, "同一 Reporter 实例被重复调用");

            ErrorHub.RemoveReporter(reporter);
            ErrorHub.Warn("removed", "ErrorHubRegression");
            AssertEqual(1, reporter.Count, "移除后的 Reporter 仍被调用");
        }
        finally
        {
            ErrorHub.RemoveReporter(reporter);
        }
    }

    private static void VerifyListenerIsolation()
    {
        int laterCalls = 0;
        void Throwing(ErrorReport _) => throw new InvalidOperationException("expected listener failure");
        void Later(ErrorReport _) => laterCalls++;

        ErrorHub.OnError += Throwing;
        ErrorHub.OnError += Later;
        try
        {
            ErrorHub.Warn("listener isolation", "ErrorHubRegression");
            AssertEqual(1, laterCalls, "OnError 监听者异常阻断了后续监听者");
        }
        finally
        {
            ErrorHub.OnError -= Throwing;
            ErrorHub.OnError -= Later;
        }
    }

    private static void VerifyReporterIsolation()
    {
        var throwing = new ThrowingReporter();
        var later = new CountingReporter();
        ErrorHub.AddReporter(throwing);
        ErrorHub.AddReporter(later);
        try
        {
            ErrorHub.Warn("reporter isolation", "ErrorHubRegression");
            AssertEqual(1, later.Count, "Reporter 异常阻断了后续 Reporter");
        }
        finally
        {
            ErrorHub.RemoveReporter(throwing);
            ErrorHub.RemoveReporter(later);
        }
    }

    private static void VerifyFatalDoesNotQuit()
    {
        var levels = new List<ErrorLevel>();
        void OnError(ErrorReport report) => levels.Add(report.Level);

        ErrorHub.OnError += OnError;
        try
        {
            ErrorHub.Fatal("expected fatal", "ErrorHubRegression");
            AssertEqual(1, levels.Count, "Fatal 没有同步分发");
            AssertEqual(ErrorLevel.Fatal, levels[0], "Fatal 报告等级错误");
        }
        finally
        {
            ErrorHub.OnError -= OnError;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}；期望 {expected}，实际 {actual}");
        }
    }

    private sealed class CountingReporter : IErrorReporter
    {
        public int Count { get; private set; }
        public void Report(in ErrorReport report) => Count++;
    }

    private sealed class ThrowingReporter : IErrorReporter
    {
        public void Report(in ErrorReport report) =>
            throw new InvalidOperationException("expected reporter failure");
    }
}
