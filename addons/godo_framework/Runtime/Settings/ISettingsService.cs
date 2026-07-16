using Godot;

namespace GoDo;

/// <summary>面向业务层的跨平台用户设置服务。</summary>
public interface ISettingsService
{
    /// <summary>当前实际采用的平台配置。</summary>
    SettingsPlatform Platform { get; }

    /// <summary>当前平台支持的全部设置能力。</summary>
    SettingsCapability Capabilities { get; }

    /// <summary>当前内存中的不可变设置快照。</summary>
    SettingsSnapshot Current { get; }

    /// <summary>判断当前平台是否同时支持指定的全部能力。</summary>
    /// <param name="capability">要检查的一个或多个能力标志。</param>
    /// <returns>全部支持且参数不是 None 时为 true，否则为 false。</returns>
    bool Supports(SettingsCapability capability);

    /// <summary>从固定设置槽位读取并立即应用；不存在时应用默认值。</summary>
    /// <returns>设置数据的实际来源。</returns>
    /// <exception cref="SaveException">设置读取、容器校验或解码失败。</exception>
    SettingsLoadStatus LoadAndApply();

    /// <summary>把当前内存快照写入固定设置槽位。</summary>
    /// <exception cref="SaveException">设置编码或写入失败。</exception>
    void Save();

    /// <summary>立即应用默认值但不自动写盘。</summary>
    void ResetToDefaults();

    /// <summary>立即设置 Master 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <exception cref="System.ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    SettingsApplyResult SetMasterVolume(float linearVolume);

    /// <summary>立即设置 BGM 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <exception cref="System.ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    SettingsApplyResult SetBgmVolume(float linearVolume);

    /// <summary>立即设置 SFX 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <exception cref="System.ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    SettingsApplyResult SetSfxVolume(float linearVolume);

    /// <summary>立即设置当前 Locale。</summary>
    /// <param name="locale">项目已加载翻译资源对应的 Locale 标识。</param>
    /// <returns>始终返回 Applied；不支持的 Locale 会抛出异常。</returns>
    /// <exception cref="System.ArgumentException">Locale 为空、仅包含空白字符或未被项目支持。</exception>
    SettingsApplyResult SetLocale(string locale);

    /// <summary>立即设置桌面窗口模式。</summary>
    /// <returns>平台支持时为 Applied，否则为 Unsupported。</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">窗口模式不是已定义值。</exception>
    SettingsApplyResult SetWindowMode(SettingsWindowMode mode);

    /// <summary>立即设置桌面窗口分辨率。</summary>
    /// <param name="resolution">宽高均为正数的窗口尺寸。</param>
    /// <returns>平台支持时为 Applied，否则为 Unsupported。</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">宽或高不是正数。</exception>
    SettingsApplyResult SetResolution(Vector2I resolution);

    /// <summary>立即启用或禁用垂直同步。</summary>
    /// <returns>平台支持时为 Applied，否则为 Unsupported。</returns>
    SettingsApplyResult SetVSync(bool enabled);
}
