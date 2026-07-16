#nullable enable

using System;
using Godot;

namespace GoDo;

internal sealed class WindowsSettingsPlatformAdapter : ISettingsPlatformAdapter
{
    public SettingsPlatform Platform => SettingsPlatform.WindowsDesktop;
    public SettingsCapability Capabilities =>
        SettingsCapability.WindowMode | SettingsCapability.Resolution | SettingsCapability.VSync;

    public SettingsApplyResult SetWindowMode(SettingsWindowMode mode)
    {
        switch (mode)
        {
            case SettingsWindowMode.Windowed:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
                break;
            case SettingsWindowMode.Borderless:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, true);
                break;
            case SettingsWindowMode.Fullscreen:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知窗口模式。");
        }

        return SettingsApplyResult.Applied;
    }

    public SettingsApplyResult SetResolution(int width, int height)
    {
        DisplayServer.WindowSetSize(new Vector2I(width, height));
        return SettingsApplyResult.Applied;
    }

    public SettingsApplyResult SetVSync(bool enabled)
    {
        DisplayServer.WindowSetVsyncMode(enabled
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);
        return SettingsApplyResult.Applied;
    }
}
