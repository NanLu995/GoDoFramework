#nullable enable

using Godot;

namespace GoDo;

internal static class SettingsPlatformAdapterFactory
{
    public static ISettingsPlatformAdapter Create()
    {
        if (OS.HasFeature("headless"))
        {
            ErrorHub.Warn("Settings", "检测到 headless 运行环境，仅启用通用设置能力。");
            return new CommonSettingsPlatformAdapter(SettingsPlatform.CommonOnly);
        }

        return OS.GetName() switch
        {
            "Windows" => new WindowsSettingsPlatformAdapter(),
            "Android" or "iOS" => new CommonSettingsPlatformAdapter(SettingsPlatform.Mobile),
            var platformName => CreateFallback(platformName),
        };
    }

    private static ISettingsPlatformAdapter CreateFallback(string platformName)
    {
        ErrorHub.Warn("Settings", $"平台 {platformName} 暂无专用适配器，仅启用通用设置能力。");
        return new CommonSettingsPlatformAdapter(SettingsPlatform.CommonOnly);
    }
}
