namespace GoDo;

/// <summary>设置读取与应用结果。</summary>
public enum SettingsLoadStatus
{
    /// <summary>没有持久化设置，已应用默认值。</summary>
    DefaultsApplied,

    /// <summary>成功读取正式设置文件。</summary>
    Loaded,

    /// <summary>正式设置文件不可用，已从备份恢复。</summary>
    RecoveredFromBackup,
}
