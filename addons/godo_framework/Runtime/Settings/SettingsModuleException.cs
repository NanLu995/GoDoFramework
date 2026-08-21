using System;
using System.Collections.Generic;

#nullable enable

namespace GoDo;

/// <summary>一个或多个关键设置模块失败时抛出的异常。</summary>
public sealed class SettingsModuleException : Exception
{
    internal SettingsModuleException(IReadOnlyList<SettingsModuleFailure> failures)
        : base(CreateMessage(failures), CreateInnerException(failures))
    {
        var copy = new SettingsModuleFailure[failures.Count];
        for (int i = 0; i < failures.Count; i++)
            copy[i] = failures[i];
        Failures = Array.AsReadOnly(copy);
    }

    /// <summary>导致当前操作失败的关键模块信息。</summary>
    public IReadOnlyList<SettingsModuleFailure> Failures { get; }

    private static string CreateMessage(IReadOnlyList<SettingsModuleFailure> failures) =>
        failures.Count == 1
            ? $"关键设置模块失败: {failures[0].ModuleId}, stage={failures[0].Stage}。"
            : $"{failures.Count} 个关键设置模块失败。";

    private static Exception? CreateInnerException(IReadOnlyList<SettingsModuleFailure> failures)
    {
        if (failures.Count == 0)
            return null;
        if (failures.Count == 1)
            return failures[0].Exception;

        var exceptions = new Exception[failures.Count];
        for (int i = 0; i < failures.Count; i++)
            exceptions[i] = failures[i].Exception;
        return new AggregateException(exceptions);
    }
}
