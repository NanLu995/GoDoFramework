namespace GoDo;

/// <summary>同一 UI 标识允许存在的运行时实例数量策略。</summary>
public enum UiInstanceMode
{
    /// <summary>同一标识同时只允许存在一个实例。</summary>
    Single,

    /// <summary>同一标识允许同时存在多个独立实例。</summary>
    Multiple
}
