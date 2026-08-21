using System;
using System.IO;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>用于演示业务设置模块契约的中性偏好数据，不代表任何具体游戏功能。</summary>
internal sealed record SamplePreferences
{
    public int NoticeLevel { get; init; } = 2;
    public bool CompactPresentation { get; init; }
}

/// <summary>拥有自身数据、版本、迁移、验证、应用和关闭边界的最小示例模块。</summary>
internal sealed class SamplePreferencesModule : ISettingsModule<SamplePreferences>
{
    private SamplePreferences _current = new();

    public SamplePreferencesModule(
        string id = "sample-preferences",
        int order = 0,
        SettingsModuleFailurePolicy failurePolicy = SettingsModuleFailurePolicy.Optional)
    {
        Id = id;
        Order = order;
        FailurePolicy = failurePolicy;
    }

    public string Id { get; }
    public int Order { get; }
    public int CurrentVersion => 2;
    public SettingsModuleFailurePolicy FailurePolicy { get; }
    public SamplePreferences Current => _current;
    public bool IsShutdown { get; private set; }
    public Action<string>? Applied { get; set; }

    public SamplePreferences CreateDefaults() => new();

    public SamplePreferences Decode(ReadOnlySpan<byte> payload, int dataVersion)
    {
        if (dataVersion is not (1 or 2))
            throw new SettingsModuleVersionException(dataVersion, CurrentVersion);

        using var stream = new MemoryStream(payload.ToArray(), writable: false);
        using var reader = new BinaryReader(stream);
        var settings = new SamplePreferences
        {
            NoticeLevel = reader.ReadInt32(),
            CompactPresentation = dataVersion >= 2 && reader.ReadBoolean(),
        };
        if (stream.Position != stream.Length)
            throw new InvalidDataException("SamplePreferences 包含未识别的尾部内容。");
        return settings;
    }

    public byte[] Encode(SamplePreferences settings)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(settings.NoticeLevel);
        writer.Write(settings.CompactPresentation);
        writer.Flush();
        return stream.ToArray();
    }

    public void Validate(SamplePreferences settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.NoticeLevel is < 0 or > 3)
            throw new InvalidDataException("NoticeLevel 必须在 0 到 3 之间。");
    }

    public void Apply(SamplePreferences settings)
    {
        _current = settings;
        Applied?.Invoke(Id);
    }

    public SamplePreferences Capture() => _current;

    public void Shutdown()
    {
        IsShutdown = true;
        Applied = null;
    }

    public void SetCurrent(SamplePreferences settings)
    {
        Validate(settings);
        _current = settings;
    }

    public static byte[] EncodeVersion1(int noticeLevel) => BitConverter.GetBytes(noticeLevel);
}
