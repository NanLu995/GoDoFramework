using System;

namespace GoDo;

/// <summary>一个可重绑定输入槽位的只读显示信息。</summary>
public readonly struct InputBindingInfo
{
    /// <summary>稳定的绑定槽位 ID。</summary>
    public InputBindingId BindingId { get; }

    /// <summary>绑定所属的语义 Context。</summary>
    public InputContextId ContextId { get; }

    /// <summary>绑定控制的语义 Action。</summary>
    public InputActionId ActionId { get; }

    /// <summary>面向玩家的绑定名称。</summary>
    public string DisplayName { get; }

    /// <summary>面向玩家的设置分组；可以为空。</summary>
    public string DisplayCategory { get; }

    /// <summary>当前绑定的设备类别；未绑定时为 Unknown。</summary>
    public InputDeviceKind Device { get; }

    /// <summary>当前绑定的简短文本；未绑定时为空字符串。</summary>
    public string CurrentDisplayText { get; }

    /// <summary>默认绑定的简短文本；默认未绑定时为空字符串。</summary>
    public string DefaultDisplayText { get; }

    /// <summary>当前绑定是否等于默认绑定。</summary>
    public bool IsDefault { get; }

    /// <summary>创建绑定显示信息。</summary>
    /// <param name="bindingId">稳定且非默认的绑定槽位 ID。</param>
    /// <param name="contextId">槽位所属的非默认 Context ID。</param>
    /// <param name="actionId">槽位控制的非默认 Action ID。</param>
    /// <param name="displayName">面向玩家的非空绑定名称。</param>
    /// <param name="displayCategory">面向玩家的设置分组；允许为空字符串但不能为 null。</param>
    /// <param name="device">当前绑定的已定义设备类别；未绑定时可为 Unknown。</param>
    /// <param name="currentDisplayText">当前绑定文本；未绑定时为空字符串。</param>
    /// <param name="defaultDisplayText">默认绑定文本；默认未绑定时为空字符串。</param>
    /// <param name="isDefault">当前绑定是否与默认绑定相同。</param>
    /// <exception cref="ArgumentException">任一 ID 为默认值，或 <paramref name="displayName"/> 为空。</exception>
    /// <exception cref="ArgumentNullException">任一允许空字符串的文本参数为 null。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="device"/> 未定义。</exception>
    public InputBindingInfo(
        InputBindingId bindingId,
        InputContextId contextId,
        InputActionId actionId,
        string displayName,
        string displayCategory,
        InputDeviceKind device,
        string currentDisplayText,
        string defaultDisplayText,
        bool isDefault)
    {
        if (bindingId.IsEmpty)
            throw new ArgumentException("输入 Binding ID 不能是默认值。", nameof(bindingId));
        if (contextId.IsEmpty)
            throw new ArgumentException("输入 Context ID 不能是默认值。", nameof(contextId));
        if (actionId.IsEmpty)
            throw new ArgumentException("输入 Action ID 不能是默认值。", nameof(actionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(displayCategory);
        if (!Enum.IsDefined(device))
            throw new ArgumentOutOfRangeException(nameof(device));
        ArgumentNullException.ThrowIfNull(currentDisplayText);
        ArgumentNullException.ThrowIfNull(defaultDisplayText);

        BindingId = bindingId;
        ContextId = contextId;
        ActionId = actionId;
        DisplayName = displayName;
        DisplayCategory = displayCategory;
        Device = device;
        CurrentDisplayText = currentDisplayText;
        DefaultDisplayText = defaultDisplayText;
        IsDefault = isDefault;
    }
}
