using System;

namespace GoDo;

/// <summary>由具体输入后端捕获、但不向业务层暴露后端对象的重绑定候选。</summary>
public abstract class InputBindingCandidate
{
    /// <summary>候选输入所属的设备类别。</summary>
    public InputDeviceKind Device { get; }

    /// <summary>适合直接显示在设置界面的简短文本。</summary>
    public string DisplayText { get; }

    /// <summary>由输入后端创建候选输入。</summary>
    /// <param name="device">捕获到候选的已定义非 Unknown 设备类别。</param>
    /// <param name="displayText">适合设置界面显示的非空简短文本。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="device"/> 未定义或为 Unknown。</exception>
    /// <exception cref="ArgumentException"><paramref name="displayText"/> 为 null、空或仅含空白。</exception>
    protected InputBindingCandidate(InputDeviceKind device, string displayText)
    {
        if (!Enum.IsDefined(device) || device == InputDeviceKind.Unknown)
            throw new ArgumentOutOfRangeException(nameof(device));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayText);

        Device = device;
        DisplayText = displayText;
    }
}
