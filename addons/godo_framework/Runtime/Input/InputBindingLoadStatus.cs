namespace GoDo;

/// <summary>已保存输入绑定的加载结果。</summary>
public enum InputBindingLoadStatus
{
    /// <summary>没有已保存配置，已应用默认绑定。</summary>
    DefaultsApplied,

    /// <summary>已从正式配置加载并应用。</summary>
    Loaded,

    /// <summary>正式配置不可用，已从备份恢复并应用。</summary>
    RecoveredFromBackup,
}
