using Godot;

namespace GoDo;

/// <summary>一份不可变的用户设置数据快照。</summary>
public sealed record SettingsSnapshot
{
    /// <summary>Master 线性音量，范围为 0 到 1。</summary>
    public float MasterVolume { get; init; } = 1f;

    /// <summary>BGM 线性音量，范围为 0 到 1。</summary>
    public float BgmVolume { get; init; } = 1f;

    /// <summary>SFX 线性音量，范围为 0 到 1。</summary>
    public float SfxVolume { get; init; } = 1f;

    /// <summary>当前 Locale 标识。</summary>
    public string Locale { get; init; } = "en";

    /// <summary>桌面窗口模式。</summary>
    public SettingsWindowMode WindowMode { get; init; } = SettingsWindowMode.Windowed;

    /// <summary>桌面窗口分辨率。</summary>
    public Vector2I Resolution { get; init; } = new(1280, 720);

    /// <summary>是否启用垂直同步。</summary>
    public bool VSyncEnabled { get; init; } = true;
}
