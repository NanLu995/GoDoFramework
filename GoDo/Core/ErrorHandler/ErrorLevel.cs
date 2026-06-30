namespace GoDo;

/// <summary>
/// 错误等级，从低到高排列。
/// </summary>
public enum ErrorLevel
{
    /// <summary>仅在 Debug 模式下输出，Release 自动忽略。</summary>
    Debug = 0,

    /// <summary>非致命警告，业务可继续运行。</summary>
    Warning = 1,

    /// <summary>运行时错误，当前操作失败但框架可恢复。</summary>
    Error = 2,

    /// <summary>致命错误，框架无法继续正常运行。</summary>
    Fatal = 3,
}
