using System;
using GuideCs;

namespace GoDo.GuideInput;

/// <summary>通过 SaveService 可靠保存 GUIDE 重绑定配置。</summary>
internal sealed class GuideInputRebindingPersistence : IInputRebindingPersistence
{
    private readonly GuideInputRebinding _rebinding;
    private readonly ISaveService _saveService;
    private readonly SaveSlot _slot;
    private readonly GuideInputRemappingCodec _codec = new();

    internal GuideInputRebindingPersistence(
        GuideInputRebinding rebinding,
        ISaveService saveService,
        SaveSlot slot)
    {
        _rebinding = rebinding;
        _saveService = saveService;
        _slot = slot;
    }

    /// <inheritdoc />
    public InputBindingLoadStatus LoadAndApply()
    {
        SaveLoadResult<GuideRemappingConfig> result = _saveService.Load(_slot, _codec);
        if (!result.HasValue)
        {
            _rebinding.ApplyConfiguration(new GuideRemappingConfig());
            return InputBindingLoadStatus.DefaultsApplied;
        }

        _rebinding.ApplyConfiguration(result.Value);
        return result.Status switch
        {
            SaveLoadStatus.Loaded => InputBindingLoadStatus.Loaded,
            SaveLoadStatus.RecoveredFromBackup => InputBindingLoadStatus.RecoveredFromBackup,
            _ => throw new InvalidOperationException($"未知 SaveLoadStatus: {result.Status}"),
        };
    }

    /// <inheritdoc />
    public void Save()
    {
        _saveService.Save(
            _slot,
            _rebinding.GetConfiguration(),
            GuideInputRemappingCodec.DataVersion,
            _codec);
    }
}
