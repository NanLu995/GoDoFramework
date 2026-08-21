using System.Collections.Generic;
using Godot;

namespace GoDo;

/// <summary>
/// 面向业务层的跨平台用户设置服务。加载、保存、重置及所有设置方法只能在 GoDoRuntime 所在的 Godot 主线程调用。
/// </summary>
public interface ISettingsService
{
    /// <summary>当前实际采用的平台配置。</summary>
    SettingsPlatform Platform { get; }

    /// <summary>当前平台支持的全部设置能力。</summary>
    SettingsCapability Capabilities { get; }

    /// <summary>当前内存中的不可变设置快照。</summary>
    SettingsSnapshot Current { get; }

    /// <summary>最近一次加载、保存、重置或关闭操作产生的模块失败；下一次模块操作开始时清空。</summary>
    IReadOnlyList<SettingsModuleFailure> LastModuleFailures { get; }

    /// <summary>注册一个由业务层拥有的设置模块。</summary>
    /// <typeparam name="TSettings">模块独占的设置数据类型。</typeparam>
    /// <param name="module">通过实例字段持有业务依赖和当前状态的模块。</param>
    /// <exception cref="System.ArgumentNullException"><paramref name="module"/> 为 null。</exception>
    /// <exception cref="System.ArgumentException">模块 ID、版本或失败策略无效，或 ID 已注册。</exception>
    /// <exception cref="System.InvalidOperationException">已经开始加载或重置设置、服务已关闭，或当前不在 Godot 主线程。</exception>
    void RegisterModule<TSettings>(ISettingsModule<TSettings> module);

    /// <summary>判断当前平台是否同时支持指定的全部能力。</summary>
    /// <param name="capability">要检查的一个或多个能力标志。</param>
    /// <returns>全部支持且参数不是 None 时为 true，否则为 false。</returns>
    bool Supports(SettingsCapability capability);

    /// <summary>从固定系统槽位及所有模块独占槽位读取，并按稳定顺序立即应用；不存在时应用默认值。</summary>
    /// <returns>设置数据的实际来源。</returns>
    /// <exception cref="System.ArgumentException">读取值或默认 Locale 未被项目支持。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程、GoDoRuntime 尚未初始化，或平台能力声明与应用结果矛盾。</exception>
    /// <exception cref="SaveException">设置读取、容器校验或解码失败。</exception>
    /// <exception cref="SettingsModuleException">关键业务设置模块加载、迁移、验证或应用失败。</exception>
    SettingsLoadStatus LoadAndApply();

    /// <summary>把系统快照和所有可保存模块按稳定顺序写入各自独占槽位。</summary>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    /// <exception cref="SaveException">设置编码或写入失败。</exception>
    /// <exception cref="SettingsModuleException">一个或多个关键业务设置模块捕获、验证、编码或保存失败。</exception>
    void Save();

    /// <summary>立即应用系统设置及所有模块默认值，但不自动写盘。</summary>
    /// <exception cref="System.ArgumentException">默认 Locale 未被项目支持。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程、GoDoRuntime 尚未初始化，或平台能力声明与应用结果矛盾。</exception>
    /// <exception cref="SettingsModuleException">关键业务设置模块创建、验证或应用默认值失败。</exception>
    void ResetToDefaults();

    /// <summary>立即设置 Master 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <returns>值成功应用到运行时并写入当前快照后返回 <see cref="SettingsApplyResult.Applied"/>。</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    SettingsApplyResult SetMasterVolume(float linearVolume);

    /// <summary>立即设置 BGM 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <returns>值成功应用到运行时并写入当前快照后返回 <see cref="SettingsApplyResult.Applied"/>。</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    SettingsApplyResult SetBgmVolume(float linearVolume);

    /// <summary>立即设置 SFX 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <returns>值成功应用到运行时并写入当前快照后返回 <see cref="SettingsApplyResult.Applied"/>。</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    SettingsApplyResult SetSfxVolume(float linearVolume);

    /// <summary>立即设置当前 Locale。</summary>
    /// <param name="locale">项目已加载翻译资源对应的 Locale 标识。</param>
    /// <returns>始终返回 Applied；不支持的 Locale 会抛出异常。</returns>
    /// <exception cref="System.ArgumentException">Locale 为空、仅包含空白字符或未被项目支持。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    SettingsApplyResult SetLocale(string locale);

    /// <summary>立即设置桌面窗口模式。</summary>
    /// <param name="mode">要应用的已定义窗口模式。</param>
    /// <returns>平台支持时为 Applied，否则为 Unsupported。</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">窗口模式不是已定义值。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    SettingsApplyResult SetWindowMode(SettingsWindowMode mode);

    /// <summary>立即设置桌面窗口分辨率。</summary>
    /// <param name="resolution">宽高均为正数的窗口尺寸。</param>
    /// <returns>平台支持时为 Applied，否则为 Unsupported。</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">宽或高不是正数。</exception>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    SettingsApplyResult SetResolution(Vector2I resolution);

    /// <summary>立即启用或禁用垂直同步。</summary>
    /// <param name="enabled">为 <see langword="true"/> 时启用垂直同步，否则禁用。</param>
    /// <returns>平台支持时为 Applied，否则为 Unsupported。</returns>
    /// <exception cref="System.InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    SettingsApplyResult SetVSync(bool enabled);
}
