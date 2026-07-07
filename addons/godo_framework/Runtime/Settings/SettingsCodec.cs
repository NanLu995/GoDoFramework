using System;
using System.IO;
using System.Text;
using Godot;

namespace GoDo;

internal sealed class SettingsCodec : ISaveCodec<SettingsSnapshot>
{
    public const int CurrentVersion = 1;

    public byte[] Encode(SettingsSnapshot value)
    {
        ArgumentNullException.ThrowIfNull(value);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(value.MasterVolume);
        writer.Write(value.BgmVolume);
        writer.Write(value.SfxVolume);
        writer.Write(value.Locale);
        writer.Write((int)value.WindowMode);
        writer.Write(value.Resolution.X);
        writer.Write(value.Resolution.Y);
        writer.Write(value.VSyncEnabled);
        writer.Flush();
        return stream.ToArray();
    }

    public SettingsSnapshot Decode(ReadOnlySpan<byte> payload, int dataVersion)
    {
        if (dataVersion != CurrentVersion)
            throw new InvalidDataException($"不支持的设置数据版本：{dataVersion}。");

        using var stream = new MemoryStream(payload.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var snapshot = new SettingsSnapshot
        {
            MasterVolume = reader.ReadSingle(),
            BgmVolume = reader.ReadSingle(),
            SfxVolume = reader.ReadSingle(),
            Locale = reader.ReadString(),
            WindowMode = (SettingsWindowMode)reader.ReadInt32(),
            Resolution = new Vector2I(reader.ReadInt32(), reader.ReadInt32()),
            VSyncEnabled = reader.ReadBoolean(),
        };

        if (stream.Position != stream.Length)
            throw new InvalidDataException("设置数据包含未识别的尾部内容。");

        SettingsService.ValidateSnapshot(snapshot);
        return snapshot;
    }
}
