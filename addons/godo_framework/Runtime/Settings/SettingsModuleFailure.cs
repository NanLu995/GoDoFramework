using System;

#nullable enable

namespace GoDo;

/// <summary>一次设置模块失败的结构化信息。</summary>
public sealed class SettingsModuleFailure
{
    internal SettingsModuleFailure(
        string moduleId,
        SettingsModuleStage stage,
        SettingsModuleFailurePolicy policy,
        bool usedDefaults,
        Exception exception)
    {
        ModuleId = moduleId;
        Stage = stage;
        Policy = policy;
        UsedDefaults = usedDefaults;
        Exception = exception;
    }

    /// <summary>失败模块的稳定 ID。</summary>
    public string ModuleId { get; }

    /// <summary>发生失败的阶段。</summary>
    public SettingsModuleStage Stage { get; }

    /// <summary>模块声明的失败策略。</summary>
    public SettingsModuleFailurePolicy Policy { get; }

    /// <summary>框架是否因该失败尝试应用模块默认值。</summary>
    public bool UsedDefaults { get; }

    /// <summary>原始失败；SaveService 包装的读取或写入错误会保留在异常链中。</summary>
    public Exception Exception { get; }
}
