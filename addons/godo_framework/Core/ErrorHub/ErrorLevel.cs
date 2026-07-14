namespace GoDo;

/// <summary>
/// 错误等级，从低到高排列。
/// </summary>
public enum ErrorLevel
{
    /// <summary>非致命警告，业务可继续运行。</summary>
    Warning = 1,

    /// <summary>运行时错误，当前操作失败但框架可恢复。</summary>
    Error = 2,

    /// <summary>最高严重等级；仅记录严重性，不主动终止游戏。</summary>
    Fatal = 3,
}
