using System;

namespace GoDo;

/// <summary>由业务层实现的存档 Payload 编解码与版本迁移边界。</summary>
public interface ISaveCodec<T>
{
    /// <summary>把业务存档对象编码为独立字节数组。</summary>
    byte[] Encode(T value);

    /// <summary>按文件记录的数据版本解码，并在需要时完成业务迁移。</summary>
    T Decode(ReadOnlySpan<byte> payload, int dataVersion);
}
