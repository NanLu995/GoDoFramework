namespace GoDo;

/// <summary>存档读取结果来源。</summary>
public enum SaveLoadStatus
{
    /// <summary>正式文件和备份均不存在。</summary>
    NotFound,

    /// <summary>成功读取正式文件。</summary>
    Loaded,

    /// <summary>正式文件不可用，成功读取备份。</summary>
    RecoveredFromBackup,
}
