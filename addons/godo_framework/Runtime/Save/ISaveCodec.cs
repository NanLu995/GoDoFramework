using System;

namespace GoDo;

/// <summary>由业务层实现的存档 Payload 编解码与版本迁移边界。</summary>
/// <typeparam name="T">业务存档模型类型。</typeparam>
public interface ISaveCodec<T>
{
    /// <summary>把业务存档对象编码为独立字节数组。</summary>
    /// <param name="value">要编码的业务存档值；是否允许 <see langword="null"/> 由具体 Codec 决定。</param>
    /// <returns>不超过 SaveService 上限、可独立写入容器的 Payload 字节。</returns>
    byte[] Encode(T value);

    /// <summary>按文件记录的数据版本解码，并在需要时完成业务迁移。</summary>
    /// <param name="payload">已通过容器长度与 SHA-256 校验的只读 Payload。</param>
    /// <param name="dataVersion">保存时写入的正业务版本号，由 Codec 决定支持与迁移范围。</param>
    /// <returns>解码并迁移到当前业务模型的值。</returns>
    T Decode(ReadOnlySpan<byte> payload, int dataVersion);
}
