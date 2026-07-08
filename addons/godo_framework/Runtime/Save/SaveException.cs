using System;

#nullable enable

namespace GoDo;

/// <summary>表示存档文件、容器校验或业务编解码失败。</summary>
public sealed class SaveException : Exception
{
    /// <summary>失败的槽位。</summary>
    public SaveSlot Slot { get; }

    /// <summary>失败的操作。</summary>
    public SaveOperation Operation { get; }

    /// <summary>创建存档异常。</summary>
    public SaveException(
        SaveSlot slot,
        SaveOperation operation,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Slot = slot;
        Operation = operation;
    }
}

/// <summary>SaveException 对应的服务操作。</summary>
public enum SaveOperation
{
    /// <summary>写入存档。</summary>
    Save,
    /// <summary>读取存档。</summary>
    Load,
    /// <summary>删除存档。</summary>
    Delete,
}
