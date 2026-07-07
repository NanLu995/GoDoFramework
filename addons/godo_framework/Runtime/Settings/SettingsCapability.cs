using System;

namespace GoDo;

/// <summary>当前平台支持的设置能力。</summary>
[Flags]
public enum SettingsCapability
{
    /// <summary>不表示任何设置能力。</summary>
    None = 0,

    /// <summary>支持 Master、BGM 和 SFX 音量。</summary>
    AudioVolume = 1 << 0,

    /// <summary>支持切换 Locale。</summary>
    Locale = 1 << 1,

    /// <summary>支持切换桌面窗口模式。</summary>
    WindowMode = 1 << 2,

    /// <summary>支持修改桌面窗口分辨率。</summary>
    Resolution = 1 << 3,

    /// <summary>支持启用或禁用垂直同步。</summary>
    VSync = 1 << 4,
}
