#nullable enable

using System;
using Godot;

namespace GoDo;

internal sealed class CommonSettingsPlatformAdapter : ISettingsPlatformAdapter
{
    public CommonSettingsPlatformAdapter(SettingsPlatform platform)
    {
        if (platform == SettingsPlatform.WindowsDesktop)
            throw new ArgumentException("WindowsDesktop 必须使用 WindowsSettingsPlatformAdapter。", nameof(platform));

        Platform = platform;
    }

    public SettingsPlatform Platform { get; }
    public SettingsCapability Capabilities => SettingsCapability.Locale;

    public SettingsApplyResult SetLocale(string locale)
    {
        TranslationServer.SetLocale(locale);
        return SettingsApplyResult.Applied;
    }

    public SettingsApplyResult SetWindowMode(SettingsWindowMode mode) => SettingsApplyResult.Unsupported;
    public SettingsApplyResult SetResolution(int width, int height) => SettingsApplyResult.Unsupported;
    public SettingsApplyResult SetVSync(bool enabled) => SettingsApplyResult.Unsupported;
}
