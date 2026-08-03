using System;
using GoDo;

#nullable enable

namespace GoDoTemplate.ExampleContent;

/// <summary>可删除的 SaveService 调用边界示例。</summary>
internal static class ExampleSaveStore
{
    private static readonly SaveSlot Slot = SaveSlot.Create("example-content");

    internal static ExampleSaveData SaveNext(ISaveService saves)
    {
        ArgumentNullException.ThrowIfNull(saves);

        SaveLoadResult<ExampleSaveData> result = saves.Load(Slot, ExampleSaveCodec.Instance);
        int nextWriteCount = result.HasValue ? result.Value.WriteCount + 1 : 1;
        var value = new ExampleSaveData
        {
            WriteCount = nextWriteCount,
            LastSavedAtUtc = DateTimeOffset.UtcNow,
        };

        saves.Save(Slot, value, ExampleSaveCodec.CurrentDataVersion, ExampleSaveCodec.Instance);
        return value;
    }
}
