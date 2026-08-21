using System;

#nullable enable

namespace GoDo;

/// <summary>
/// 定义由游戏业务拥有、由 <see cref="ISettingsService"/> 统一加载、应用、保存和关闭的设置模块。
/// </summary>
/// <typeparam name="TSettings">模块独占的设置数据类型；框架不会解释其业务含义。</typeparam>
public interface ISettingsModule<TSettings>
{
    /// <summary>跨版本保持稳定的模块标识；必须由小写 ASCII 字母、数字和连字符组成。</summary>
    string Id { get; }

    /// <summary>模块顺序；较小值先加载、应用和保存，相同值按 <see cref="Id"/> 排序。</summary>
    int Order { get; }

    /// <summary>当前写入的数据版本；必须大于 0。</summary>
    int CurrentVersion { get; }

    /// <summary>模块失败时阻断启动还是降级继续。</summary>
    SettingsModuleFailurePolicy FailurePolicy { get; }

    /// <summary>创建一份有效的默认设置。</summary>
    /// <returns>新的默认设置实例。</returns>
    TSettings CreateDefaults();

    /// <summary>解码指定版本的数据，并在需要时迁移为当前内存模型。</summary>
    /// <param name="payload">模块独占槽位中的 Payload。</param>
    /// <param name="dataVersion">写入 Payload 时记录的数据版本。</param>
    /// <returns>解码并迁移后的设置。</returns>
    /// <exception cref="SettingsModuleVersionException">当前模块不能读取该数据版本。</exception>
    TSettings Decode(ReadOnlySpan<byte> payload, int dataVersion);

    /// <summary>把当前模型编码为模块独占 Payload。</summary>
    /// <param name="settings">已通过 <see cref="Validate"/> 的设置。</param>
    /// <returns>要交给 SaveService 保存的字节。</returns>
    byte[] Encode(TSettings settings);

    /// <summary>验证设置；失败时应抛出带明确信息的参数或数据异常，且不得改变运行时状态。</summary>
    /// <param name="settings">要验证的设置。</param>
    void Validate(TSettings settings);

    /// <summary>
    /// 在 Godot 主线程应用设置。实现应先完成可能失败的准备，再发布状态，以便失败时避免留下部分应用结果。
    /// </summary>
    /// <param name="settings">已经验证的设置。</param>
    void Apply(TSettings settings);

    /// <summary>捕获当前要持久化的业务设置；不得返回 <see langword="null"/>。</summary>
    /// <returns>当前设置模型。</returns>
    TSettings Capture();

    /// <summary>解除模块拥有的订阅并释放生命周期资源；不得保存数据或访问静态业务全局状态。</summary>
    void Shutdown();
}
