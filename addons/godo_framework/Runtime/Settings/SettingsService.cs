using System;
using System.Collections.Generic;
using System.IO;
using Godot;

#nullable enable

namespace GoDo;

/// <summary>管理跨平台用户设置的应用与持久化；所有改变运行时或磁盘状态的方法只能在 Godot 主线程调用。</summary>
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
    private readonly List<ISettingsModuleEntry> _modules = new();
    private SettingsSnapshot _current = new();
    private IReadOnlyList<SettingsModuleFailure> _lastModuleFailures = Array.Empty<SettingsModuleFailure>();
    private bool _registrationClosed;
    private bool _modulesReady;
    private bool _isShutdown;

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
    public IReadOnlyList<SettingsModuleFailure> LastModuleFailures => _lastModuleFailures;

    /// <inheritdoc/>
    public void RegisterModule<TSettings>(ISettingsModule<TSettings> module)
    {
        VerifyAvailable();
        ArgumentNullException.ThrowIfNull(module);
        if (_registrationClosed)
            throw new InvalidOperationException("设置已经开始加载或重置，不能继续注册模块。");

        ValidateModuleId(module.Id);
        if (module.CurrentVersion <= 0)
            throw new ArgumentException("设置模块当前版本必须大于 0。", nameof(module));
        if (!Enum.IsDefined(module.FailurePolicy))
            throw new ArgumentException("设置模块失败策略无效。", nameof(module));

        for (int i = 0; i < _modules.Count; i++)
        {
            if (string.Equals(_modules[i].Id, module.Id, StringComparison.Ordinal))
                throw new ArgumentException($"设置模块 ID 已注册: {module.Id}。", nameof(module));
        }

        _modules.Add(new SettingsModuleEntry<TSettings>(module));
    }

    /// <inheritdoc/>
    public bool Supports(SettingsCapability capability) =>
        capability != SettingsCapability.None &&
        (Capabilities & capability) == capability;

    /// <inheritdoc/>
    public SettingsLoadStatus LoadAndApply()
    {
        VerifyAvailable();
        PrepareModules();
        _modulesReady = false;
        var failures = new List<SettingsModuleFailure>();
        SaveLoadResult<SettingsSnapshot> result = _saveService.Load(_settingsSlot, _codec);
        if (!result.HasValue)
        {
            ApplySnapshot(new SettingsSnapshot());
            LoadAndApplyModules(failures);
            _modulesReady = true;
            _lastModuleFailures = failures.AsReadOnly();
            return SettingsLoadStatus.DefaultsApplied;
        }

        ApplySnapshot(result.Value);
        LoadAndApplyModules(failures);
        _modulesReady = true;
        _lastModuleFailures = failures.AsReadOnly();
        return result.Status == SaveLoadStatus.RecoveredFromBackup
            ? SettingsLoadStatus.RecoveredFromBackup
            : SettingsLoadStatus.Loaded;
    }

    /// <inheritdoc/>
    public void Save()
    {
        VerifyAvailable();
        if (_modules.Count > 0 && !_modulesReady)
        {
            throw new InvalidOperationException(
                "已注册的设置模块尚未成功完成加载或重置，不能保存。");
        }

        _saveService.Save(_settingsSlot, _current, SettingsCodec.CurrentVersion, _codec);

        var failures = new List<SettingsModuleFailure>();
        var criticalFailures = new List<SettingsModuleFailure>();
        for (int i = 0; i < _modules.Count; i++)
        {
            SettingsModuleFailure? failure = _modules[i].Save(_saveService);
            if (failure == null)
                continue;

            failures.Add(failure);
            if (failure.Policy == SettingsModuleFailurePolicy.Critical)
                criticalFailures.Add(failure);
        }

        _lastModuleFailures = failures.AsReadOnly();
        if (criticalFailures.Count > 0)
            throw new SettingsModuleException(criticalFailures.ToArray());
    }

    /// <inheritdoc/>
    public void ResetToDefaults()
    {
        VerifyAvailable();
        PrepareModules();
        _modulesReady = false;
        ApplySnapshot(new SettingsSnapshot());

        var failures = new List<SettingsModuleFailure>();
        for (int i = 0; i < _modules.Count; i++)
        {
            bool succeeded = _modules[i].ResetToDefaults(failures);
            if (!succeeded && _modules[i].FailurePolicy == SettingsModuleFailurePolicy.Critical)
            {
                _lastModuleFailures = failures.AsReadOnly();
                throw CreateCriticalException(failures);
            }
        }

        _modulesReady = true;
        _lastModuleFailures = failures.AsReadOnly();
    }

    /// <summary>立即设置 Master 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <returns>值成功应用到运行时并写入当前快照后返回 <see cref="SettingsApplyResult.Applied"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    public SettingsApplyResult SetMasterVolume(float linearVolume) =>
        SetVolume(AudioGroup.Master, linearVolume);

    /// <summary>立即设置 BGM 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <returns>值成功应用到运行时并写入当前快照后返回 <see cref="SettingsApplyResult.Applied"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    public SettingsApplyResult SetBgmVolume(float linearVolume) =>
        SetVolume(AudioGroup.Bgm, linearVolume);

    /// <summary>立即设置 SFX 线性音量。</summary>
    /// <param name="linearVolume">0 到 1 的有限值。</param>
    /// <returns>值成功应用到运行时并写入当前快照后返回 <see cref="SettingsApplyResult.Applied"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">音量不是 0 到 1 的有限值。</exception>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    public SettingsApplyResult SetSfxVolume(float linearVolume) =>
        SetVolume(AudioGroup.Sfx, linearVolume);

    /// <inheritdoc/>
    public SettingsApplyResult SetLocale(string locale)
    {
        VerifyAvailable();
        ValidateLocale(locale);
        string standardized = _localization.ApplyLocale(locale);
        _current = _current with { Locale = standardized };
        return SettingsApplyResult.Applied;
    }

    /// <summary>立即设置桌面窗口模式。</summary>
    /// <param name="mode">要应用的已定义窗口模式。</param>
    /// <returns>平台支持时为 <see cref="SettingsApplyResult.Applied"/>，否则为 <see cref="SettingsApplyResult.Unsupported"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException">窗口模式不是已定义值。</exception>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    public SettingsApplyResult SetWindowMode(SettingsWindowMode mode)
    {
        VerifyAvailable();
        ValidateWindowMode(mode);
        SettingsApplyResult result = _platformAdapter.SetWindowMode(mode);
        if (result == SettingsApplyResult.Applied)
            _current = _current with { WindowMode = mode };
        return result;
    }

    /// <inheritdoc/>
    public SettingsApplyResult SetResolution(Vector2I resolution)
    {
        VerifyAvailable();
        ValidateResolution(resolution);
        SettingsApplyResult result = _platformAdapter.SetResolution(resolution.X, resolution.Y);
        if (result == SettingsApplyResult.Applied)
            _current = _current with { Resolution = resolution };
        return result;
    }

    /// <summary>立即启用或禁用垂直同步。</summary>
    /// <param name="enabled">为 <see langword="true"/> 时启用垂直同步，否则禁用。</param>
    /// <returns>平台支持时为 <see cref="SettingsApplyResult.Applied"/>，否则为 <see cref="SettingsApplyResult.Unsupported"/>。</returns>
    /// <exception cref="InvalidOperationException">当前不在 Godot 主线程，或 GoDoRuntime 尚未初始化。</exception>
    public SettingsApplyResult SetVSync(bool enabled)
    {
        VerifyAvailable();
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
        VerifyAvailable();
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

    internal void Shutdown()
    {
        MainThreadGuard.VerifyAccess();
        if (_isShutdown)
            return;

        _isShutdown = true;
        var failures = new List<SettingsModuleFailure>();
        for (int i = _modules.Count - 1; i >= 0; i--)
        {
            try
            {
                _modules[i].Shutdown();
            }
            catch (Exception exception)
            {
                var failure = new SettingsModuleFailure(
                    _modules[i].Id,
                    SettingsModuleStage.Shutdown,
                    _modules[i].FailurePolicy,
                    usedDefaults: false,
                    exception);
                failures.Add(failure);
                ErrorHub.Warn(
                    $"设置模块关闭失败: {exception.Message}",
                    "Settings",
                    $"ModuleId={_modules[i].Id}");
            }
        }

        _modulesReady = false;
        _lastModuleFailures = failures.AsReadOnly();
    }

    private void VerifyAvailable()
    {
        MainThreadGuard.VerifyAccess();
        if (_isShutdown)
            throw new InvalidOperationException("SettingsService 已关闭。");
    }

    private void PrepareModules()
    {
        _registrationClosed = true;
        _modules.Sort(static (left, right) =>
        {
            int orderComparison = left.Order.CompareTo(right.Order);
            return orderComparison != 0
                ? orderComparison
                : StringComparer.Ordinal.Compare(left.Id, right.Id);
        });
        _lastModuleFailures = Array.Empty<SettingsModuleFailure>();
    }

    private void LoadAndApplyModules(List<SettingsModuleFailure> failures)
    {
        for (int i = 0; i < _modules.Count; i++)
        {
            bool succeeded = _modules[i].LoadAndApply(_saveService, failures);
            if (succeeded || _modules[i].FailurePolicy != SettingsModuleFailurePolicy.Critical)
                continue;

            _lastModuleFailures = failures.AsReadOnly();
            throw CreateCriticalException(failures);
        }
    }

    private static SettingsModuleException CreateCriticalException(
        IReadOnlyList<SettingsModuleFailure> failures)
    {
        var criticalFailures = new List<SettingsModuleFailure>();
        for (int i = 0; i < failures.Count; i++)
        {
            if (failures[i].Policy == SettingsModuleFailurePolicy.Critical)
                criticalFailures.Add(failures[i]);
        }

        return new SettingsModuleException(criticalFailures.ToArray());
    }

    private static void ValidateModuleId(string id)
    {
        const int maxLength = 40;
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("设置模块 ID 不能为空。", nameof(id));
        if (id.Length > maxLength)
            throw new ArgumentOutOfRangeException(nameof(id), $"设置模块 ID 不能超过 {maxLength} 个字符。");
        if (id[0] == '-' || id[^1] == '-')
            throw new ArgumentException("设置模块 ID 不能以连字符开头或结尾。", nameof(id));

        for (int i = 0; i < id.Length; i++)
        {
            char character = id[i];
            bool allowed = character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
            if (!allowed)
            {
                throw new ArgumentException(
                    "设置模块 ID 只能包含小写 ASCII 字母、数字和连字符。",
                    nameof(id));
            }
        }
    }

    private static bool ContainsVersionFailure(Exception exception)
    {
        if (exception is SettingsModuleVersionException)
            return true;
        if (exception is AggregateException aggregate)
        {
            for (int i = 0; i < aggregate.InnerExceptions.Count; i++)
            {
                if (ContainsVersionFailure(aggregate.InnerExceptions[i]))
                    return true;
            }
        }

        return exception.InnerException != null && ContainsVersionFailure(exception.InnerException);
    }

    private interface ISettingsModuleEntry
    {
        string Id { get; }
        int Order { get; }
        SettingsModuleFailurePolicy FailurePolicy { get; }
        bool LoadAndApply(ISaveService saveService, List<SettingsModuleFailure> failures);
        bool ResetToDefaults(List<SettingsModuleFailure> failures);
        SettingsModuleFailure? Save(ISaveService saveService);
        void Shutdown();
    }

    private sealed class SettingsModuleEntry<TSettings> : ISettingsModuleEntry
    {
        private const string SlotPrefix = "godo-settings-module-";

        private readonly ISettingsModule<TSettings> _module;
        private readonly string _id;
        private readonly int _order;
        private readonly int _currentVersion;
        private readonly SettingsModuleFailurePolicy _failurePolicy;
        private readonly SaveSlot _slot;
        private readonly ModuleCodec _codec;
        private bool _canSave;

        public SettingsModuleEntry(ISettingsModule<TSettings> module)
        {
            _module = module;
            _id = module.Id;
            _order = module.Order;
            _currentVersion = module.CurrentVersion;
            _failurePolicy = module.FailurePolicy;
            _slot = SaveSlot.Create(SlotPrefix + _id);
            _codec = new ModuleCodec(module);
        }

        public string Id => _id;
        public int Order => _order;
        public SettingsModuleFailurePolicy FailurePolicy => _failurePolicy;

        public bool LoadAndApply(
            ISaveService saveService,
            List<SettingsModuleFailure> failures)
        {
            SaveLoadResult<TSettings> result;
            try
            {
                result = saveService.Load(_slot, _codec);
            }
            catch (Exception exception)
            {
                bool useDefaults = FailurePolicy == SettingsModuleFailurePolicy.Optional;
                failures.Add(CreateFailure(SettingsModuleStage.Load, useDefaults, exception));
                _canSave = false;
                if (!useDefaults)
                    return false;

                bool preserveStoredVersion = ContainsVersionFailure(exception);
                bool defaultsApplied = ResetToDefaults(failures);
                if (defaultsApplied && preserveStoredVersion)
                    _canSave = false;
                return defaultsApplied;
            }

            if (!result.HasValue)
                return ResetToDefaults(failures);

            try
            {
                ApplyValue(result.Value);
                if (result.Status == SaveLoadStatus.RecoveredFromBackup && result.RecoveryFailure != null)
                {
                    failures.Add(CreateFailure(
                        SettingsModuleStage.Load,
                        usedDefaults: false,
                        result.RecoveryFailure));
                }
                _canSave =
                    result.Status != SaveLoadStatus.RecoveredFromBackup ||
                    result.RecoveryFailure == null ||
                    !ContainsVersionFailure(result.RecoveryFailure);
                return true;
            }
            catch (ModuleStepException exception)
            {
                bool useDefaults =
                    FailurePolicy == SettingsModuleFailurePolicy.Optional &&
                    exception.Stage == SettingsModuleStage.Validate;
                failures.Add(CreateFailure(exception.Stage, useDefaults, exception.InnerException!));
                _canSave = false;
                return useDefaults && ResetToDefaults(failures);
            }
        }

        public bool ResetToDefaults(List<SettingsModuleFailure> failures)
        {
            try
            {
                TSettings defaults;
                try
                {
                    defaults = _module.CreateDefaults();
                    EnsureValue(defaults);
                }
                catch (Exception exception)
                {
                    throw new ModuleStepException(SettingsModuleStage.CreateDefaults, exception);
                }

                ApplyValue(defaults);
                _canSave = true;
                return true;
            }
            catch (ModuleStepException exception)
            {
                _canSave = false;
                failures.Add(CreateFailure(exception.Stage, usedDefaults: true, exception.InnerException!));
                return false;
            }
        }

        public SettingsModuleFailure? Save(ISaveService saveService)
        {
            if (!_canSave)
                return null;

            TSettings value;
            try
            {
                value = _module.Capture();
                EnsureValue(value);
            }
            catch (Exception exception)
            {
                return CreateFailure(SettingsModuleStage.Capture, usedDefaults: false, exception);
            }

            try
            {
                _module.Validate(value);
            }
            catch (Exception exception)
            {
                return CreateFailure(SettingsModuleStage.Validate, usedDefaults: false, exception);
            }

            try
            {
                saveService.Save(_slot, value, _currentVersion, _codec);
                return null;
            }
            catch (Exception exception)
            {
                return CreateFailure(SettingsModuleStage.Save, usedDefaults: false, exception);
            }
        }

        public void Shutdown() => _module.Shutdown();

        private void ApplyValue(TSettings value)
        {
            EnsureValue(value);
            try
            {
                _module.Validate(value);
            }
            catch (Exception exception)
            {
                throw new ModuleStepException(SettingsModuleStage.Validate, exception);
            }

            try
            {
                _module.Apply(value);
            }
            catch (Exception exception)
            {
                throw new ModuleStepException(SettingsModuleStage.Apply, exception);
            }
        }

        private SettingsModuleFailure CreateFailure(
            SettingsModuleStage stage,
            bool usedDefaults,
            Exception exception) =>
            new(Id, stage, FailurePolicy, usedDefaults, exception);

        private static void EnsureValue(TSettings value)
        {
            if (value is null)
                throw new InvalidDataException("设置模块返回了 null 数据。");
        }

        private sealed class ModuleCodec : ISaveCodec<TSettings>
        {
            private readonly ISettingsModule<TSettings> _module;

            public ModuleCodec(ISettingsModule<TSettings> module)
            {
                _module = module;
            }

            public byte[] Encode(TSettings value) =>
                _module.Encode(value) ?? throw new InvalidDataException("设置模块 Encode 返回了 null。");

            public TSettings Decode(ReadOnlySpan<byte> payload, int dataVersion)
            {
                TSettings value = _module.Decode(payload, dataVersion);
                EnsureValue(value);
                return value;
            }
        }
    }

    private sealed class ModuleStepException : Exception
    {
        public ModuleStepException(SettingsModuleStage stage, Exception innerException)
            : base($"设置模块阶段失败: {stage}。", innerException)
        {
            Stage = stage;
        }

        public SettingsModuleStage Stage { get; }
    }
}
