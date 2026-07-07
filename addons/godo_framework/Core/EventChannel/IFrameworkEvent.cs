namespace GoDo;

/// <summary>
/// 仅供 GoDo 框架内部通信使用的事件标记。
/// 框架拆分为独立程序集后，外部业务程序集无法实现此接口；
/// 当前单程序集项目仍需遵守架构约定，不在业务代码中实现它。
/// </summary>
internal interface IFrameworkEvent : IEventMessage { }
