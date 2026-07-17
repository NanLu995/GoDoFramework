namespace GoDo;

/// <summary>输入绑定成功应用后发布的提示失效事实；订阅方应重新查询需要显示的提示。</summary>
public readonly struct InputBindingsChangedEvent : IEventMessage
{
}
