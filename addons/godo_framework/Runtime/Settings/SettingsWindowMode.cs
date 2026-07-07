namespace GoDo;

/// <summary>跨业务层暴露的桌面窗口模式。</summary>
public enum SettingsWindowMode
{
    /// <summary>带系统边框的普通窗口。</summary>
    Windowed,

    /// <summary>无系统边框的窗口。</summary>
    Borderless,

    /// <summary>非独占全屏窗口。</summary>
    Fullscreen,
}
