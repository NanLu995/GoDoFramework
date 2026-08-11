namespace GoDo;

/// <summary>最近产生有效输入的设备类别已经变化。</summary>
public readonly struct InputDeviceChangedEvent : IEventMessage
{
    /// <summary>变化前的设备类别。</summary>
    public InputDeviceKind Previous { get; }

    /// <summary>变化后的设备类别。</summary>
    public InputDeviceKind Current { get; }

    /// <summary>创建一个设备类别变化事实。</summary>
    /// <param name="previous">变化前的设备类别。</param>
    /// <param name="current">变化后的设备类别。</param>
    public InputDeviceChangedEvent(InputDeviceKind previous, InputDeviceKind current)
    {
        Previous = previous;
        Current = current;
    }
}
