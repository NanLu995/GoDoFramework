using System;
using System.Buffers.Binary;
using GoDo;

namespace GoDoFramework.Demo;

/// <summary>点击挑战 Demo 首版存档的固定长度二进制 Codec。</summary>
public sealed class DemoSaveCodec : ISaveCodec<DemoSaveData>
{
    public const int CurrentDataVersion = 1;
    private const int PayloadLength = sizeof(int) * 3;

    public byte[] Encode(DemoSaveData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Validate(value);

        var payload = new byte[PayloadLength];
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), value.BestScore);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), value.GamesPlayed);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8, 4), value.LastScore);
        return payload;
    }

    public DemoSaveData Decode(ReadOnlySpan<byte> payload, int dataVersion)
    {
        if (dataVersion != CurrentDataVersion)
            throw new InvalidOperationException($"不支持 Demo 存档数据版本 {dataVersion}。");
        if (payload.Length != PayloadLength)
            throw new InvalidOperationException($"Demo 存档 Payload 长度应为 {PayloadLength}，实际为 {payload.Length}。");

        var value = new DemoSaveData
        {
            BestScore = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(0, 4)),
            GamesPlayed = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(4, 4)),
            LastScore = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(8, 4))
        };
        Validate(value);
        return value;
    }

    private static void Validate(DemoSaveData value)
    {
        if (value.BestScore < 0 || value.GamesPlayed < 0 || value.LastScore < 0)
            throw new InvalidOperationException("Demo 存档中的分数和游戏次数不能为负数。");
        if (value.LastScore > value.BestScore)
            throw new InvalidOperationException("Demo 上一局分数不能高于最高分。");
    }
}
