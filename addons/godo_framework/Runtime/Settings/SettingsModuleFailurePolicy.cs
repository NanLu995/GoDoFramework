namespace GoDo;

/// <summary>设置模块加载、应用或保存失败时采用的策略。</summary>
public enum SettingsModuleFailurePolicy
{
    /// <summary>记录失败并尽可能使用默认值继续；模块不可安全运行时跳过该模块。</summary>
    Optional,

    /// <summary>失败会阻断设置初始化或使保存操作失败。</summary>
    Critical,
}
