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
