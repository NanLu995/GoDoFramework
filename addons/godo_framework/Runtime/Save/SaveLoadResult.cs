using System;

#nullable enable

namespace GoDo;

/// <summary>带来源、业务版本和保存时间的读取结果。</summary>
public readonly struct SaveLoadResult<T>
{
    private readonly T? _value;

    /// <summary>读取状态。</summary>
    public SaveLoadStatus Status { get; }

    /// <summary>结果是否包含存档值。</summary>
    public bool HasValue => Status != SaveLoadStatus.NotFound;

    /// <summary>业务 Payload 版本；NotFound 时为 0。</summary>
    public int DataVersion { get; }

    /// <summary>文件记录的 UTC 保存时间；NotFound 时为 null。</summary>
    public DateTimeOffset? SavedAtUtc { get; }

    /// <summary>解码后的业务值；NotFound 时访问会抛出异常。</summary>
    public T Value => HasValue
        ? _value!
        : throw new InvalidOperationException("NotFound 结果不包含存档值。");

    private SaveLoadResult(
        SaveLoadStatus status,
        T? value,
        int dataVersion,
        DateTimeOffset? savedAtUtc)
    {
        Status = status;
        _value = value;
        DataVersion = dataVersion;
        SavedAtUtc = savedAtUtc;
    }

    internal static SaveLoadResult<T> NotFound() =>
        new(SaveLoadStatus.NotFound, default, 0, null);

    internal static SaveLoadResult<T> Loaded(
        T value,
        int dataVersion,
        DateTimeOffset savedAtUtc,
        bool recoveredFromBackup) =>
        new(
            recoveredFromBackup
                ? SaveLoadStatus.RecoveredFromBackup
                : SaveLoadStatus.Loaded,
            value,
            dataVersion,
            savedAtUtc);
}
