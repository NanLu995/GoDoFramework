#nullable enable

namespace GoDo;

internal interface ISettingsPlatformAdapter
{
    SettingsPlatform Platform { get; }
    SettingsCapability Capabilities { get; }
    SettingsApplyResult SetWindowMode(SettingsWindowMode mode);
    SettingsApplyResult SetResolution(int width, int height);
    SettingsApplyResult SetVSync(bool enabled);
}
