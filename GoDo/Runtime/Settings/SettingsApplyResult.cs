namespace GoDo;

/// <summary>单项设置的应用结果。</summary>
public enum SettingsApplyResult
{
    /// <summary>设置已更新到内存并应用到运行时。</summary>
    Applied,

    /// <summary>当前平台不支持该设置，内存快照保持不变。</summary>
    Unsupported,
}
