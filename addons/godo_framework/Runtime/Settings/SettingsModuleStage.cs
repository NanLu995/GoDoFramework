namespace GoDo;

/// <summary>设置模块发生失败的生命周期阶段。</summary>
public enum SettingsModuleStage
{
    /// <summary>从独占槽位读取或解码数据。</summary>
    Load,

    /// <summary>创建默认数据。</summary>
    CreateDefaults,

    /// <summary>验证加载、默认或捕获的数据。</summary>
    Validate,

    /// <summary>把数据应用到运行时。</summary>
    Apply,

    /// <summary>捕获当前业务设置。</summary>
    Capture,

    /// <summary>编码或保存模块数据。</summary>
    Save,

    /// <summary>关闭模块并释放生命周期资源。</summary>
    Shutdown,
}
