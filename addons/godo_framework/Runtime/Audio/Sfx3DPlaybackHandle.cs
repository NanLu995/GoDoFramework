using System;

#nullable enable

namespace GoDo;

/// <summary>
/// 标识一次已经开始的 3D 空间 SFX 播放。自然结束、主动停止、抢占或服务退出后句柄变为过期，
/// 但值本身保持不变，可安全用于查询并得到 false。
/// </summary>
public readonly struct Sfx3DPlaybackHandle : IEquatable<Sfx3DPlaybackHandle>
{
    internal ulong Value { get; }

    /// <summary>
    /// 句柄是否包含非默认播放标识；不表示播放仍然活动，活动状态应通过 IsSfx3DPlaying 查询。
    /// </summary>
    public bool IsValid => Value != 0;

    internal Sfx3DPlaybackHandle(ulong value)
    {
        Value = value;
    }

    /// <inheritdoc />
    public bool Equals(Sfx3DPlaybackHandle other) => Value == other.Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        obj is Sfx3DPlaybackHandle other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>比较两个句柄是否指向同一次 3D 播放。</summary>
    public static bool operator ==(
        Sfx3DPlaybackHandle left,
        Sfx3DPlaybackHandle right) =>
        left.Equals(right);

    /// <summary>比较两个句柄是否不指向同一次 3D 播放。</summary>
    public static bool operator !=(
        Sfx3DPlaybackHandle left,
        Sfx3DPlaybackHandle right) =>
        !left.Equals(right);
}
