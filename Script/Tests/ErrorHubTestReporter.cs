using System;
using GoDo;

namespace GoDoFramework.Tests;

/// <summary>ErrorHub 异常隔离验证使用的 Reporter。</summary>
public sealed class ErrorHubTestReporter : IErrorReporter
{
    private readonly bool _throwOnReport;

    public int ReportCount { get; private set; }

    public ErrorHubTestReporter(bool throwOnReport)
    {
        _throwOnReport = throwOnReport;
    }

    public void Report(in ErrorReport report)
    {
        ReportCount++;

        if (_throwOnReport)
            throw new InvalidOperationException("[ErrorHubTest] 预期的 Reporter 异常。");
    }
}
