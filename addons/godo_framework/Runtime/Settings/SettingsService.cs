using System;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>管理跨平台用户设置的应用与持久化。</summary>
public sealed class SettingsService : ISettingsService
{
    private const string SettingsSlotName = "godo-settings";
    private static readonly SaveSlot SettingsSlot = SaveSlot.Create(SettingsSlotName);

    private readonly IAudioService _audioService;
    private readonly ISaveService _saveService;
    private readonly LocalizationService _localization;
    private readonly ISettingsPlatformAdapter _platformAdapter;
    private readonly SaveSlot _settingsSlot;
    private readonly SettingsCodec _codec = new();
    private SettingsSnapshot _current = new();

    /// <summary>使用独立本地化实例和自动检测的平台适配器创建设置服务。</summary>
    /// <param name="audioService">接收音量设置的长期音频服务。</param>
    /// <param name="saveService">负责设置槽位持久化的存档服务。</param>
    /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
    [Obsolete("请显式传入 LocalizationService，以便 Settings 与业务翻译查询共享同一实例。")]
    public SettingsService(IAudioService audioService, ISaveService saveService)
        : this(audioService, saveService, new LocalizationService())
    {
    }

    /// <summary>使用自动检测的平台适配器创建设置服务。</summary>
    /// <param name="audioService">接收音量设置的长期音频服务。</param>
    /// <param name="saveService">负责设置槽位持久化的存档服务。</param>
    /// <param name="localization">负责 Locale 校验、应用和变更通知的本地化服务。</param>
    /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
    public SettingsService(
        IAudioService audioService,
        ISaveService saveService,
        LocalizationService localization)
        : this(audioService, saveService, localization, SettingsPlatformAdapterFactory.Create(), SettingsSlot)
    {
    }

    internal SettingsService(
        IAudioService audioService,
        ISaveService saveService,
        LocalizationService localization,
        ISettingsPlatformAdapter platformAdapter)
        : this(audioService, saveService, localization, platformAdapter, SettingsSlot)
    {
    }

    internal SettingsService(
        IAudioService audioService,
        ISaveService saveService,
        LocalizationService localization,
        ISettingsPlatformAdapter platformAdapter,
        SaveSlot settingsSlot)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _platformAdapter = platformAdapter ?? throw new ArgumentNullException(nameof(platformAdapter));
        if (!settingsSlot.IsValid)
            throw new ArgumentException("设置槽位必须通过 SaveSlot.Create 创建。", nameof(settingsSlot));
        _settingsSlot = settingsSlot;
    }

    /// <inheritdoc/>
    public SettingsPlatform Platform => _platformAdapter.Platform;

    /// <inheritdoc/>
    public SettingsCapability Capabilities =>
        SettingsCapability.AudioVolume | SettingsCapability.Locale | _platformAdapter.Capabilities;

    /// <inheritdoc/>
    public SettingsSnapshot Current => _current;

    /// <inheritdoc/>
    public bool Supports(SettingsCapability capability) =>
        capability != SettingsCapability.None &&
        (Capabilities & capability) == capability;

    /// <inheritdoc/>
    public SettingsLoadStatus LoadAndApply()
    {
        MainThreadGuard.VerifyAccess();
        SaveLoadResult<SettingsSnapshot> result = _saveService.Load(_settingsSlot, _codec);
        if (!result.HasValue)
        {
            ApplySnapshot(new SettingsSnapshot());
            return SettingsLoadStatus.DefaultsApplied;
        }

        ApplySnapshot(result.Value);
        return result.Status == SaveLoadStatus.RecoveredFromBackup
            ? SettingsLoadStatus.RecoveredFromBackup
            : SettingsLoadStatus.Loaded;
    }

    /// <inheritdoc/>
    public void Save()
    {
        MainThreadGuard.VerifyAccess();
        _saveService.Save(_settingsSlot, _current, SettingsCodec.CurrentVersion, _codec);
    }

    /// <inheritdoc/>
    public void ResetToDefaults()
    {
        MainThreadGuard.VerifyAccess();
        ApplySnapshot(new SettingsSnapshot());
    }

    /// <inheritdoc/>
    public SettingsApplyResult SetMasterVolume(float linearVolume) =>
        SetVolume(AudioGroup.Master, linearVolume);

    /// <inheritdoc/>
    public SettingsApplyResult SetBgmVolume(float linearVolume) =>
        SetVolume(AudioGroup.Bgm, linearVolume);

    /// <inheritdoc/>
    public SettingsApplyResult SetSfxVolume(float linearVolume) =>
        SetVolume(AudioGroup.Sfx, linearVolume);

    /// <inheritdoc/>
    public SettingsApplyResult SetLocale(string locale)
    {
        MainThreadGuard.VerifyAccess();
        ValidateLocale(locale);
        string standardized = _localization.ApplyLocale(locale);
        _current = _current with { Locale = standardized };
        return SettingsApplyResult.Applied;
    }

    /// <inheritdoc/>
    public SettingsApplyResult SetWindowMode(SettingsWindowMode mode)
    {
        MainThreadGuard.VerifyAccess();
        ValidateWindowMode(mode);
        SettingsApplyResult result = _platformAdapter.SetWindowMode(mode);
        if (result == SettingsApplyResult.Applied)
            _current = _current with { WindowMode = mode };
        return result;
    }

    /// <inheritdoc/>
    public SettingsApplyResult SetResolution(Vector2I resolution)
    {
        MainThreadGuard.VerifyAccess();
        ValidateResolution(resolution);
        SettingsApplyResult result = _platformAdapter.SetResolution(resolution.X, resolution.Y);
        if (result == SettingsApplyResult.Applied)
            _current = _current with { Resolution = resolution };
        return result;
    }

    /// <inheritdoc/>
    public SettingsApplyResult SetVSync(bool enabled)
    {
        MainThreadGuard.VerifyAccess();
        SettingsApplyResult result = _platformAdapter.SetVSync(enabled);
        if (result == SettingsApplyResult.Applied)
            _current = _current with { VSyncEnabled = enabled };
        return result;
    }

    internal static void ValidateSnapshot(SettingsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateVolume(snapshot.MasterVolume, nameof(snapshot.MasterVolume));
        ValidateVolume(snapshot.BgmVolume, nameof(snapshot.BgmVolume));
        ValidateVolume(snapshot.SfxVolume, nameof(snapshot.SfxVolume));
        ValidateLocale(snapshot.Locale);
        ValidateWindowMode(snapshot.WindowMode);
        ValidateResolution(snapshot.Resolution);
    }

    private SettingsApplyResult SetVolume(AudioGroup group, float linearVolume)
    {
        MainThreadGuard.VerifyAccess();
        ValidateVolume(linearVolume, nameof(linearVolume));
        _audioService.SetVolume(group, linearVolume);
        _current = group switch
        {
            AudioGroup.Master => _current with { MasterVolume = linearVolume },
            AudioGroup.Bgm => _current with { BgmVolume = linearVolume },
            AudioGroup.Sfx => _current with { SfxVolume = linearVolume },
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, "未知音频分组。"),
        };
        return SettingsApplyResult.Applied;
    }

    private void ApplySnapshot(SettingsSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        if (!_localization.IsLocaleSupported(snapshot.Locale))
            throw new ArgumentException($"Locale 未加载或不受项目支持: {snapshot.Locale}。", nameof(snapshot));

        _audioService.SetVolume(AudioGroup.Master, snapshot.MasterVolume);
        _audioService.SetVolume(AudioGroup.Bgm, snapshot.BgmVolume);
        _audioService.SetVolume(AudioGroup.Sfx, snapshot.SfxVolume);

        string locale = _localization.ApplyLocale(snapshot.Locale);
        ApplyIfSupported(SettingsCapability.WindowMode, () => _platformAdapter.SetWindowMode(snapshot.WindowMode));
        ApplyIfSupported(
            SettingsCapability.Resolution,
            () => _platformAdapter.SetResolution(snapshot.Resolution.X, snapshot.Resolution.Y));
        ApplyIfSupported(SettingsCapability.VSync, () => _platformAdapter.SetVSync(snapshot.VSyncEnabled));
        _current = snapshot with { Locale = locale };
    }

    private void ApplyIfSupported(
        SettingsCapability capability,
        Func<SettingsApplyResult> apply)
    {
        if (!Supports(capability))
            return;

        SettingsApplyResult result = apply();
        if (result != SettingsApplyResult.Applied)
        {
            throw new InvalidOperationException(
                $"平台声明支持 {capability}，但应用设置时返回了 {result}。");
        }
    }

    private static void ValidateVolume(float value, string parameterName)
    {
        if (!float.IsFinite(value) || value < 0f || value > 1f)
            throw new ArgumentOutOfRangeException(parameterName, "音量必须是 0 到 1 的有限值。");
    }

    private static void ValidateLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException("Locale 不能为空。", nameof(locale));
    }

    private static void ValidateWindowMode(SettingsWindowMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "未知窗口模式。");
    }

    private static void ValidateResolution(Vector2I resolution)
    {
        if (resolution.X <= 0 || resolution.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(resolution), "分辨率宽高必须为正数。");
    }
}
