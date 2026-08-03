using System;
using System.Text.Json;
using GoDo;

#nullable enable

namespace GoDoTemplate.ExampleContent;

/// <summary>可删除的 JSON 存档 Codec 示例，演示业务数据版本的显式校验边界。</summary>
internal sealed class ExampleSaveCodec : ISaveCodec<ExampleSaveData>
{
    internal const int CurrentDataVersion = 1;
    internal static readonly ExampleSaveCodec Instance = new();

    private ExampleSaveCodec()
    {
    }

    public byte[] Encode(ExampleSaveData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.SerializeToUtf8Bytes(value);
    }

    public ExampleSaveData Decode(ReadOnlySpan<byte> payload, int dataVersion)
    {
        if (dataVersion != CurrentDataVersion)
        {
            throw new InvalidOperationException(
                $"ExampleSaveData 不支持数据版本 {dataVersion}。");
        }

        ExampleSaveData? value = JsonSerializer.Deserialize<ExampleSaveData>(payload);
        return value ?? throw new InvalidOperationException("ExampleSaveData Payload 为空。");
    }
}
