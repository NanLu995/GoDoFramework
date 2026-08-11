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
    /// <param name="bindingId">产生提示的非默认绑定槽位 ID。</param>
    /// <param name="contextId">提示所属的非默认 Context ID。</param>
    /// <param name="actionId">提示对应的非默认 Action ID。</param>
    /// <param name="device">提示对应的已定义非 Unknown 设备类别。</param>
    /// <param name="displayText">已绑定时的非空文本，或未绑定时的空字符串。</param>
    /// <param name="isBound">槽位当前是否存在有效绑定。</param>
    /// <exception cref="ArgumentException">任一 ID 为默认值，或文本与 <paramref name="isBound"/> 状态不一致。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="displayText"/> 为 null。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="device"/> 未定义或为 Unknown。</exception>
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
