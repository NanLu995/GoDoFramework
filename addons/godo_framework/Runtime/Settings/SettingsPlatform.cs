namespace GoDo;

/// <summary>SettingsService 实际选择的平台配置。</summary>
public enum SettingsPlatform
{
    /// <summary>Windows 桌面平台。</summary>
    WindowsDesktop,

    /// <summary>Android 或 iOS 等移动平台。</summary>
    Mobile,

    /// <summary>未知平台，仅启用跨平台通用设置。</summary>
    CommonOnly,
}
