using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
            Run("模块绑定与错误委托", VerifyChannelAndErrorDelegation);
            Run("控制台输出", VerifyConsoleOutput);
            Run("滚动文件与退出刷新", VerifyRollingFileAndShutdownFlush);
            Run("文件日志运行中刷新", VerifyPeriodicFileFlush);
            Run("多实例日志文件回退", VerifyOccupiedLogFileFallback);
            Run("文件日志队列满", VerifyFileQueueCapacity);
            Run("文件日志目录不可写", VerifyUnavailableLogDirectory);
#if DEBUG
            Run("重复日志聚合", VerifyDuplicateAggregation);
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
        AssertThrows<ArgumentException>(
            static () => LogHub.For(" "),
            "模块绑定入口接受了空模块");
        AssertThrows<ArgumentException>(
            static () => default(LogChannel).Warn("invalid channel"),
            "默认日志通道没有拒绝缺失模块");
        AssertThrows<ArgumentException>(
            static () => LogHub.Warn(string.Empty, "LogHubRegression"),
            "Warning 接受了空消息");
        AssertThrows<ArgumentNullException>(
            static () => LogHub.Error((Exception)null!, "LogHubRegression"),
            "Error 接受了 null 异常");
    }

    private static void VerifyChannelAndErrorDelegation()
    {
        ErrorLevel previousMinLevel = ErrorHub.MinLevel;
#if DEBUG
        bool previousSuppressOutput = ErrorHub.SuppressGodotOutputForTesting;
        ErrorHub.SuppressGodotOutputForTesting = true;
#endif
        var reports = new List<ErrorReport>();
        void OnError(ErrorReport report) => reports.Add(report);

        ErrorHub.OnError += OnError;
        try
        {
            ErrorHub.MinLevel = ErrorLevel.Warning;
            LogChannel log = LogHub.For("LogHubRegression");
            var exception = new InvalidOperationException("delegated exception");

            LogHub.Warn("fallback applied", "LogHubRegression", "source=static");
            log.Error("operation failed", "source=channel");
            log.Error(exception, "source=exception");
            log.Fatal("cannot continue", "source=fatal");

            AssertEqual(4, reports.Count, "便捷入口没有产生预期数量的错误报告");
            AssertReport(
                reports[0],
                ErrorLevel.Warning,
                "fallback applied",
                "LogHubRegression",
                "source=static",
                exception: null);
            AssertReport(
                reports[1],
                ErrorLevel.Error,
                "operation failed",
                "LogHubRegression",
                "source=channel",
                exception: null);
            AssertReport(
                reports[2],
                ErrorLevel.Error,
                "delegated exception",
                "LogHubRegression",
                "source=exception",
                exception);
            AssertReport(
                reports[3],
                ErrorLevel.Fatal,
                "cannot continue",
                "LogHubRegression",
                "source=fatal",
                exception: null);
        }
        finally
        {
            ErrorHub.OnError -= OnError;
            ErrorHub.MinLevel = previousMinLevel;
#if DEBUG
            ErrorHub.SuppressGodotOutputForTesting = previousSuppressOutput;
#endif
        }
    }

    private static void VerifyConsoleOutput()
    {
        LogHub.Debug("调试输出", "LogHubRegression");
        LogHub.Info("信息输出", "LogHubRegression");
    }

    private static void VerifyRollingFileAndShutdownFlush()
    {
        string directory = CreateArtifactPath("rolling");
        try
        {
            var writer = new RollingFileLogWriter(
                directory,
                maxFileBytes: 256,
                archiveCount: 2,
                queueCapacity: 64);
            for (int i = 0; i < 16; i++)
            {
                writer.Write(
                    DateTime.UtcNow,
                    LogLevel.Info,
                    $"entry={i}; payload=abcdefghijklmnopqrstuvwxyz",
                    "LogHubRegression",
                    context: null);
            }

            string currentPath = Path.Combine(directory, RollingFileLogWriter.CurrentFileName);
            string firstArchivePath =
                Path.Combine(directory, $"{RollingFileLogWriter.FileNamePrefix}.1.log");
            writer.Write(
                DateTime.UtcNow,
                LogLevel.Info,
                "shutdown-entry",
                "LogHubRegression",
                context: null);
            writer.Dispose();
            Assert(
                File.ReadAllText(currentPath).Contains(
                    "shutdown-entry",
                    StringComparison.Ordinal),
                "退出刷新没有写出队列中的最后一条日志");
            Assert(File.Exists(firstArchivePath), "达到容量后没有生成滚动日志文件");
            Assert(
                !File.Exists(Path.Combine(
                    directory,
                    $"{RollingFileLogWriter.FileNamePrefix}.3.log")),
                "保留数量超过配置上限");
        }
        finally
        {
            DeleteArtifactPath(directory);
        }
    }

    private static void VerifyPeriodicFileFlush()
    {
        string directory = CreateArtifactPath("periodic-flush");
        RollingFileLogWriter? writer = null;
        try
        {
            writer = new RollingFileLogWriter(
                directory,
                maxFileBytes: 4096,
                archiveCount: 1,
                queueCapacity: 4);
            writer.Write(
                DateTime.UtcNow,
                LogLevel.Info,
                "periodic-entry",
                "LogHubRegression",
                context: null);

            string currentPath = Path.Combine(directory, RollingFileLogWriter.CurrentFileName);
            Assert(
                SpinWait.SpinUntil(
                    () => writer.GetDebugSnapshot().CurrentFileBytes > 0,
                    TimeSpan.FromSeconds(2)),
                "运行中定期刷新没有写出日志");
            FileLogDebugSnapshot snapshot = writer.GetDebugSnapshot();
            Assert(snapshot.IsReady && !snapshot.HasFailed, "文件日志正常状态快照错误");
            Assert(snapshot.CurrentFileBytes > 0, "文件日志状态没有更新当前文件大小");
            writer.Dispose();
            writer = null;
            Assert(
                File.ReadAllText(currentPath).Contains(
                    "periodic-entry",
                    StringComparison.Ordinal),
                "运行中刷新后的文件缺少日志内容");
        }
        finally
        {
            writer?.Dispose();
            DeleteArtifactPath(directory);
        }
    }

    private static void VerifyFileQueueCapacity()
    {
        using var writer = new RollingFileLogWriter(
            CreateArtifactPath("queue"),
            maxFileBytes: 1024,
            archiveCount: 1,
            queueCapacity: 2,
            startWorker: false);

        writer.Write(DateTime.UtcNow, LogLevel.Info, "first", "LogHubRegression", null);
        writer.Write(DateTime.UtcNow, LogLevel.Info, "second", "LogHubRegression", null);
        writer.Write(DateTime.UtcNow, LogLevel.Info, "third", "LogHubRegression", null);

        AssertEqual(1, writer.DroppedLineCount, "队列满时没有准确统计丢弃数量");
        AssertEqual(
            1,
            writer.GetDebugSnapshot().DroppedLineCount,
            "文件日志状态没有包含累计丢弃数量");
        Assert(
            writer.TryConsumeDroppedLineCount(out int droppedCount) && droppedCount == 1,
            "队列满摘要没有返回本轮丢弃数量");
        Assert(
            !writer.TryConsumeDroppedLineCount(out _),
            "已消费的队列满摘要被重复返回");
    }

    private static void VerifyOccupiedLogFileFallback()
    {
        string directory = CreateArtifactPath("occupied");
        RollingFileLogWriter? writer = null;
        try
        {
            Directory.CreateDirectory(directory);
            string primaryPath =
                Path.Combine(directory, RollingFileLogWriter.CurrentFileName);
            using var primaryLock = new FileStream(
                primaryPath,
                FileMode.OpenOrCreate,
                System.IO.FileAccess.ReadWrite,
                FileShare.Read);

            writer = new RollingFileLogWriter(
                directory,
                maxFileBytes: 256,
                archiveCount: 1,
                queueCapacity: 4);
            writer.Write(
                DateTime.UtcNow,
                LogLevel.Info,
                new string('x', 300),
                "LogHubRegression",
                context: null);
            writer.Write(
                DateTime.UtcNow,
                LogLevel.Info,
                "multi-instance-entry",
                "LogHubRegression",
                context: null);

            Assert(
                SpinWait.SpinUntil(
                    () => writer.GetDebugSnapshot().IsReady || writer.HasFailed,
                    TimeSpan.FromSeconds(2)),
                "主日志被占用时文件线程没有完成路径选择");
            FileLogDebugSnapshot snapshot = writer.GetDebugSnapshot();
            string expectedPath = Path.Combine(
                directory,
                $"{RollingFileLogWriter.FileNamePrefix}.{System.Environment.ProcessId}.log");
            string expectedArchivePath = Path.Combine(
                directory,
                $"{RollingFileLogWriter.FileNamePrefix}.{System.Environment.ProcessId}.1.log");
            Assert(snapshot.IsReady && !snapshot.HasFailed, "主日志被占用时没有启用回退文件");
            AssertEqual(expectedPath, snapshot.Path, "回退日志文件名未包含当前进程号");

            writer.Dispose();
            writer = null;
            Assert(
                File.Exists(expectedArchivePath),
                $"回退日志轮转文件未保留当前进程号；当前文件大小：" +
                $"{new FileInfo(expectedPath).Length} B");
            Assert(
                File.ReadAllText(expectedPath).Contains(
                    "multi-instance-entry",
                    StringComparison.Ordinal),
                "回退日志文件缺少已写入内容");
        }
        finally
        {
            writer?.Dispose();
            DeleteArtifactPath(directory);
        }
    }

    private static void VerifyUnavailableLogDirectory()
    {
        string parentDirectory = CreateArtifactPath("unavailable");
        try
        {
            Directory.CreateDirectory(parentDirectory);
            string filePath = Path.Combine(parentDirectory, "not-a-directory");
            File.WriteAllText(filePath, "block directory creation");

            using var writer = new RollingFileLogWriter(
                filePath,
                maxFileBytes: 1024,
                archiveCount: 1,
                queueCapacity: 4);

            Assert(
                SpinWait.SpinUntil(() => writer.HasFailed, TimeSpan.FromSeconds(2)),
                "目录不可用时文件写入线程没有进入失败状态");
            Assert(
                writer.TryConsumeFailure(out string message) &&
                message.Contains("已停用", StringComparison.Ordinal),
                "目录不可用时没有提供一次性降级信息");
            FileLogDebugSnapshot snapshot = writer.GetDebugSnapshot();
            Assert(snapshot.HasFailed && !snapshot.IsReady, "文件写入失败状态快照错误");
            Assert(!writer.TryConsumeFailure(out _), "文件写入失败被重复上报");
        }
        finally
        {
            DeleteArtifactPath(parentDirectory);
        }
    }

    private static string CreateArtifactPath(string category)
    {
        string root = ProjectSettings.GlobalizePath(
            $"user://verification/rolling-file-log/{category}");
        return Path.Combine(root, Guid.NewGuid().ToString("N"));
    }

    private static void DeleteArtifactPath(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

#if DEBUG
    private static void VerifyDuplicateAggregation()
    {
        LogHub.Initialize();
        for (int i = 0; i < 128; i++)
            LogHub.Debug("repeated-entry", "LogHubRegression");

        LogEntry[] snapshot = LogHub.GetDebugSnapshot();
        AssertEqual(1, snapshot.Length, "连续重复日志没有聚合为单个条目");
        AssertEqual(128, snapshot[0].RepeatCount, "重复日志聚合次数错误");
        AssertEqual("repeated-entry", snapshot[0].Message, "重复日志聚合后消息错误");
        if (snapshot[0].FirstTimestampUtc > snapshot[0].TimestampUtc)
            throw new InvalidOperationException("重复日志首次时间晚于最后时间");
    }

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

    private static void AssertReport(
        ErrorReport report,
        ErrorLevel expectedLevel,
        string expectedMessage,
        string expectedModule,
        string expectedContext,
        Exception? exception)
    {
        Assert(report.Level == expectedLevel, $"报告等级错误：{report.Level}");
        AssertEqual(expectedMessage, report.Message, "报告消息错误");
        AssertEqual(expectedModule, report.Module, "报告模块错误");
        AssertEqual(expectedContext, report.Context ?? string.Empty, "报告上下文错误");
        Assert(
            ReferenceEquals(exception, report.Exception),
            "报告没有保留预期的异常引用");
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
