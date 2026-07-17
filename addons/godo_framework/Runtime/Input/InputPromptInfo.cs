using System;

namespace GoDo;

/// <summary>一个输入绑定面向当前设备的只读文本提示。</summary>
public readonly struct InputPromptInfo
{
    /// <summary>产生提示的稳定绑定槽位 ID。</summary>
    public InputBindingId BindingId { get; }

    /// <summary>提示所属的语义 Context。</summary>
    public InputContextId ContextId { get; }

    /// <summary>提示对应的语义 Action。</summary>
    public InputActionId ActionId { get; }

    /// <summary>提示对应的具体设备类别。</summary>
    public InputDeviceKind Device { get; }

    /// <summary>后端提供的简短文本；未绑定时为空字符串。</summary>
    public string DisplayText { get; }

    /// <summary>当前槽位是否存在有效绑定。</summary>
    public bool IsBound { get; }

    /// <summary>创建输入提示信息。</summary>
    public InputPromptInfo(
        InputBindingId bindingId,
        InputContextId contextId,
        InputActionId actionId,
        InputDeviceKind device,
        string displayText,
        bool isBound)
    {
        if (bindingId.IsEmpty)
            throw new ArgumentException("输入 Binding ID 不能是默认值。", nameof(bindingId));
        if (contextId.IsEmpty)
            throw new ArgumentException("输入 Context ID 不能是默认值。", nameof(contextId));
        if (actionId.IsEmpty)
            throw new ArgumentException("输入 Action ID 不能是默认值。", nameof(actionId));
        if (!Enum.IsDefined(device) || device == InputDeviceKind.Unknown)
            throw new ArgumentOutOfRangeException(nameof(device));
        ArgumentNullException.ThrowIfNull(displayText);
        if (isBound && string.IsNullOrWhiteSpace(displayText))
            throw new ArgumentException("已绑定输入的提示文本不能为空。", nameof(displayText));
        if (!isBound && displayText.Length != 0)
            throw new ArgumentException("未绑定输入的提示文本必须为空。", nameof(displayText));

        BindingId = bindingId;
        ContextId = contextId;
        ActionId = actionId;
        Device = device;
        DisplayText = displayText;
        IsBound = isBound;
    }
}
