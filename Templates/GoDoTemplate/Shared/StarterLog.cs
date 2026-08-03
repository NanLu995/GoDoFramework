using GoDo;

#nullable enable

namespace GoDoTemplate;

/// <summary>集中维护模板业务层使用的稳定日志通道。</summary>
internal static class StarterLog
{
    internal static readonly LogChannel Boot = LogHub.For("GoDoTemplate.Boot");
    internal static readonly LogChannel MainMenu = LogHub.For("GoDoTemplate.MainMenu");
    internal static readonly LogChannel Gameplay = LogHub.For("GoDoTemplate.Gameplay");
    internal static readonly LogChannel Settings = LogHub.For("GoDoTemplate.Settings");
    internal static readonly LogChannel Input = LogHub.For("GoDoTemplate.Input");
}
