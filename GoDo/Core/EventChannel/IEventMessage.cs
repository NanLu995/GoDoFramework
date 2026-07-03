namespace GoDo;

/// <summary>
/// EventChannel 可派发消息的公共标记接口。
/// 消息必须是 struct，以避免事件对象产生堆分配。
/// </summary>
public interface IEventMessage { }
