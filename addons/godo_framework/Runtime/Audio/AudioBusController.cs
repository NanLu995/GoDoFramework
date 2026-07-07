using System;
using Godot;

namespace GoDo;

/// <summary>AudioService 内部的 Godot Audio Bus 映射与音量控制。</summary>
internal sealed class AudioBusController
{
    internal static readonly StringName MasterBus = new("Master");
    internal static readonly StringName BgmBus = new("BGM");
    internal static readonly StringName SfxBus = new("SFX");

    public void Initialize()
    {
        MainThreadGuard.VerifyAccess();
        EnsureBus(BgmBus);
        EnsureBus(SfxBus);
    }

    public float GetVolume(AudioGroup group)
    {
        MainThreadGuard.VerifyAccess();
        int busIndex = GetRequiredBusIndex(group);
        return AudioServer.GetBusVolumeLinear(busIndex);
    }

    public void SetVolume(AudioGroup group, float linearVolume)
    {
        MainThreadGuard.VerifyAccess();
        if (!float.IsFinite(linearVolume) || linearVolume < 0f || linearVolume > 1f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(linearVolume),
                linearVolume,
                "线性音量必须是 0 到 1 之间的有限值。");
        }

        int busIndex = GetRequiredBusIndex(group);
        AudioServer.SetBusVolumeLinear(busIndex, linearVolume);
    }

    private static int GetRequiredBusIndex(AudioGroup group)
    {
        StringName busName = GetBusName(group);
        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex < 0)
            throw new InvalidOperationException($"Audio Bus 不存在: {busName}");

        return busIndex;
    }

    private static StringName GetBusName(AudioGroup group) => group switch
    {
        AudioGroup.Master => MasterBus,
        AudioGroup.Bgm => BgmBus,
        AudioGroup.Sfx => SfxBus,
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "未知音频分组。"),
    };

    private static void EnsureBus(StringName busName)
    {
        if (AudioServer.GetBusIndex(busName) >= 0)
            return;

        AudioServer.AddBus();
        int busIndex = AudioServer.BusCount - 1;
        AudioServer.SetBusName(busIndex, busName);
        AudioServer.SetBusSend(busIndex, MasterBus);

        ErrorHub.Warn(
            $"运行时缺少 {busName} Audio Bus，已临时创建并发送到 Master",
            "Audio",
            context: "AudioBusController.Initialize");
    }
}
