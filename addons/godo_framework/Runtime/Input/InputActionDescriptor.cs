using System;

namespace GoDo;

/// <summary>输入后端在初始化时声明的固定 Action 描述。</summary>
public readonly struct InputActionDescriptor
{
    /// <summary>Action 的业务语义 ID。</summary>
    public InputActionId ActionId { get; }

    /// <summary>Action 的固定输出类型。</summary>
    public InputActionValueType ValueType { get; }

    /// <summary>创建 Action 描述。</summary>
    /// <param name="actionId">后端公开的非默认语义 Action ID。</param>
    /// <param name="valueType">后端在整个安装生命周期内保持不变的输出类型。</param>
    /// <exception cref="ArgumentException"><paramref name="actionId"/> 是默认 ID。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="valueType"/> 未定义。</exception>
    public InputActionDescriptor(InputActionId actionId, InputActionValueType valueType)
    {
        if (actionId.IsEmpty)
            throw new ArgumentException("输入 Action ID 不能是默认值。", nameof(actionId));
        if (!Enum.IsDefined(valueType))
            throw new ArgumentOutOfRangeException(nameof(valueType));

        ActionId = actionId;
        ValueType = valueType;
    }
}
