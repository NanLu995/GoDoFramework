using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using GoDo;
using GodotFileAccess = Godot.FileAccess;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>SettingsService 正常与异常边界的无交互回归验证入口。</summary>
public sealed partial class SettingsServiceRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("默认值与不支持能力", VerifyDefaultsAndUnsupportedCapabilities);
            Run("非法输入保持状态", VerifyInvalidInputsPreserveState);
            Run("平台能力声明矛盾", VerifyPlatformContractMismatch);
            Run("依赖异常透传且不重复上报", VerifyDependencyFailures);
            Run("设置文件备份恢复与双重损坏", VerifyPersistenceRecovery);
            Run("单模块加载应用与重复保存", VerifyModuleLoadApplyAndSave);
            Run("多模块顺序稳定", VerifyStableModuleOrder);
            Run("模块版本不兼容与损坏降级", VerifyModuleVersionAndCorruptionFallback);
            Run("可选与关键模块失败策略", VerifyModuleFailurePolicies);
            Run("重复注册与关闭边界", VerifyRegistrationAndShutdownBoundaries);
            Run("模块迁移与系统旧存档兼容", VerifyModuleMigrationAndSystemCompatibility);

            GD.Print($"[SettingsServiceRegression] PASS ({_passed}/11)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SettingsServiceRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[SettingsServiceRegression] PASS: {name}");
    }

    private static void VerifyDefaultsAndUnsupportedCapabilities()
    {
        var audio = new RecordingAudioService();
        var saves = new TestSaveService();
        var localization = new LocalizationService();
        var platform = new TestPlatformAdapter(SettingsCapability.None);
        var settings = CreateSettings(audio, saves, localization, platform);

        AssertEqual(SettingsLoadStatus.DefaultsApplied, settings.LoadAndApply(), "空设置未应用默认值");
        AssertEqual(new SettingsSnapshot(), settings.Current, "默认快照错误");

        SettingsSnapshot before = settings.Current;
        AssertEqual(
            SettingsApplyResult.Unsupported,
            settings.SetWindowMode(SettingsWindowMode.Fullscreen),
            "不支持的窗口模式没有返回 Unsupported");
        AssertEqual(before, settings.Current, "不支持的能力修改了当前快照");
    }

    private static void VerifyInvalidInputsPreserveState()
    {
        var audio = new RecordingAudioService();
        var settings = CreateSettings(
            audio,
            new TestSaveService(),
            new LocalizationService(),
            new TestPlatformAdapter(SettingsCapability.None));
        SettingsSnapshot before = settings.Current;

        AssertThrows<ArgumentOutOfRangeException>(
            () => settings.SetMasterVolume(float.NaN),
            "NaN 音量没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => settings.SetBgmVolume(-0.01f),
            "负音量没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => settings.SetSfxVolume(1.01f),
            "超过 1 的音量没有被拒绝");
        AssertThrows<ArgumentException>(() => settings.SetLocale(" "), "空 Locale 没有被拒绝");
        AssertThrows<ArgumentException>(() => settings.SetLocale("zz"), "未知 Locale 没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => settings.SetWindowMode((SettingsWindowMode)999),
            "未知窗口模式没有被拒绝");
        AssertThrows<ArgumentOutOfRangeException>(
            () => settings.SetResolution(new Vector2I(0, 720)),
            "非正分辨率没有被拒绝");

        AssertEqual(before, settings.Current, "非法输入修改了当前快照");
        AssertEqual(1f, audio.GetVolume(AudioGroup.Master), "非法输入修改了运行时音量");
    }

    private static void VerifyPlatformContractMismatch()
    {
        var platform = new TestPlatformAdapter(SettingsCapability.WindowMode)
        {
            WindowModeResult = SettingsApplyResult.Unsupported,
        };
        var settings = CreateSettings(
            new RecordingAudioService(),
            new TestSaveService(),
            new LocalizationService(),
            platform);
        SettingsSnapshot before = settings.Current;

        AssertThrows<InvalidOperationException>(
            () => settings.LoadAndApply(),
            "平台声明支持但返回 Unsupported 时没有失败");
        AssertEqual(before, settings.Current, "平台契约失败修改了当前快照");
    }

    private static void VerifyDependencyFailures()
    {
        var saves = new TestSaveService
        {
            LoadFailure = new SaveException(
                SaveSlot.Create("settings-regression"),
                SaveOperation.Load,
                "expected load failure"),
        };
        var settings = CreateSettings(
            new RecordingAudioService(),
            saves,
            new LocalizationService(),
            new TestPlatformAdapter(SettingsCapability.None));
        int errorCount = 0;
        void OnError(ErrorReport _) => errorCount++;

        ErrorHub.OnError += OnError;
        try
        {
            AssertThrows<SaveException>(() => settings.LoadAndApply(), "读取异常没有透传");
            saves.LoadFailure = null;
            saves.SaveFailure = new SaveException(
                SaveSlot.Create("settings-regression"),
                SaveOperation.Save,
                "expected save failure");
            AssertThrows<SaveException>(() => settings.Save(), "保存异常没有透传");
            AssertEqual(0, errorCount, "Settings 在抛出 SaveException 前重复上报了 ErrorHub");
        }
        finally
        {
            ErrorHub.OnError -= OnError;
        }
    }

    private static void VerifyPersistenceRecovery()
    {
        var saves = new SaveService();
        SaveSlot slot = SaveSlot.Create($"settings-regression-{Guid.NewGuid():N}");
        var localization = new LocalizationService();
        var platform = new TestPlatformAdapter(SettingsCapability.None);

        try
        {
            var writer = new SettingsService(
                new RecordingAudioService(),
                saves,
                localization,
                platform,
                slot);
            writer.SetMasterVolume(0.25f);
            writer.Save();
            writer.SetMasterVolume(0.75f);
            writer.Save();
            Corrupt(slot, string.Empty);

            var reader = new SettingsService(
                new RecordingAudioService(),
                saves,
                localization,
                platform,
                slot);
            AssertEqual(
                SettingsLoadStatus.RecoveredFromBackup,
                reader.LoadAndApply(),
                "损坏正式设置没有从备份恢复");
            AssertEqual(0.25f, reader.Current.MasterVolume, "恢复的不是健康备份设置");

            Corrupt(slot, ".bak");
            AssertThrows<SaveException>(
                () => reader.LoadAndApply(),
                "正式设置与备份双重损坏时没有抛出 SaveException");
        }
        finally
        {
            saves.Delete(slot);
        }
    }

    private static void VerifyModuleLoadApplyAndSave()
    {
        var saves = new ModuleMemorySaveService();
        SaveSlot systemSlot = SaveSlot.Create("settings-module-normal");
        var firstModule = new SamplePreferencesModule();
        var first = CreateSettings(saves, systemSlot);
        first.RegisterModule(firstModule);

        AssertEqual(SettingsLoadStatus.DefaultsApplied, first.LoadAndApply(), "模块首次加载没有使用默认值");
        AssertEqual(new SamplePreferences(), firstModule.Current, "模块默认值没有应用");
        firstModule.SetCurrent(new SamplePreferences { NoticeLevel = 3, CompactPresentation = true });
        first.Save();
        first.Save();
        AssertEqual(2, saves.GetSaveCount("godo-settings-module-sample-preferences"), "重复保存没有逐次持久化模块");

        var restoredModule = new SamplePreferencesModule();
        var restored = CreateSettings(saves, systemSlot);
        restored.RegisterModule(restoredModule);
        AssertEqual(SettingsLoadStatus.Loaded, restored.LoadAndApply(), "系统设置没有从旧槽位恢复");
        AssertEqual(3, restoredModule.Current.NoticeLevel, "模块保存值没有恢复");
        Assert(restoredModule.Current.CompactPresentation, "模块布尔值没有恢复");
    }

    private static void VerifyStableModuleOrder()
    {
        var order = new List<string>();
        var settings = CreateSettings(new ModuleMemorySaveService(), SaveSlot.Create("settings-module-order"));
        settings.RegisterModule(new SamplePreferencesModule("module-z", order: 10) { Applied = order.Add });
        settings.RegisterModule(new SamplePreferencesModule("module-b", order: -1) { Applied = order.Add });
        settings.RegisterModule(new SamplePreferencesModule("module-a", order: -1) { Applied = order.Add });

        settings.LoadAndApply();
        AssertEqual("module-a,module-b,module-z", string.Join(',', order), "模块没有按 Order 和 ID 稳定应用");
    }

    private static void VerifyModuleVersionAndCorruptionFallback()
    {
        const string moduleSlot = "godo-settings-module-sample-preferences";

        var incompatibleSaves = new ModuleMemorySaveService();
        incompatibleSaves.Seed(moduleSlot, dataVersion: 99, Array.Empty<byte>());
        var incompatibleModule = new SamplePreferencesModule();
        var incompatible = CreateSettings(
            incompatibleSaves,
            SaveSlot.Create("settings-module-incompatible"));
        incompatible.RegisterModule(incompatibleModule);
        incompatible.LoadAndApply();
        AssertEqual(new SamplePreferences(), incompatibleModule.Current, "不兼容版本没有降级到运行时默认值");
        AssertEqual(SettingsModuleStage.Load, incompatible.LastModuleFailures[0].Stage, "版本失败阶段错误");
        incompatible.Save();
        AssertEqual(0, incompatibleSaves.GetSaveCount(moduleSlot), "不兼容的更高版本被旧模块覆盖");

        var recoveredSaves = new ModuleMemorySaveService();
        recoveredSaves.SeedRecovered(
            moduleSlot,
            dataVersion: 1,
            SamplePreferencesModule.EncodeVersion1(1),
            new SettingsModuleVersionException(99, 2));
        var recovered = CreateSettings(recoveredSaves, SaveSlot.Create("settings-module-version-backup"));
        recovered.RegisterModule(new SamplePreferencesModule());
        recovered.LoadAndApply();
        AssertEqual(SettingsModuleStage.Load, recovered.LastModuleFailures[0].Stage, "备份恢复没有保留正式档失败信息");
        recovered.Save();
        AssertEqual(0, recoveredSaves.GetSaveCount(moduleSlot), "从旧备份恢复时覆盖了不兼容的新版本正式档");

        var corruptSaves = new ModuleMemorySaveService();
        corruptSaves.Seed(moduleSlot, dataVersion: 2, new byte[] { 1 });
        var corrupt = CreateSettings(corruptSaves, SaveSlot.Create("settings-module-corrupt"));
        var corruptModule = new SamplePreferencesModule();
        corrupt.RegisterModule(corruptModule);
        corrupt.LoadAndApply();
        AssertEqual(new SamplePreferences(), corruptModule.Current, "损坏数据没有降级到默认值");
        Assert(corrupt.LastModuleFailures[0].UsedDefaults, "损坏数据失败没有记录默认回退");
        corrupt.Save();
        AssertEqual(1, corruptSaves.GetSaveCount(moduleSlot), "损坏数据回退后没有允许显式保存修复");
    }

    private static void VerifyModuleFailurePolicies()
    {
        var optional = CreateSettings(
            new ModuleMemorySaveService(),
            SaveSlot.Create("settings-module-optional"));
        optional.RegisterModule(new FaultingSettingsModule(
            "optional-module",
            SettingsModuleFailurePolicy.Optional));
        AssertEqual(SettingsLoadStatus.DefaultsApplied, optional.LoadAndApply(), "可选模块失败阻断了启动");
        AssertEqual(SettingsModuleStage.Apply, optional.LastModuleFailures[0].Stage, "可选模块失败阶段错误");

        var critical = CreateSettings(
            new ModuleMemorySaveService(),
            SaveSlot.Create("settings-module-critical"));
        var criticalModule = new FaultingSettingsModule(
            "critical-module",
            SettingsModuleFailurePolicy.Critical);
        critical.RegisterModule(criticalModule);
        SettingsModuleException exception = AssertThrows<SettingsModuleException>(
            () => critical.LoadAndApply(),
            "关键模块失败没有阻断启动");
        AssertEqual("critical-module", exception.Failures[0].ModuleId, "关键模块异常缺少模块 ID");
        AssertThrows<InvalidOperationException>(() => critical.Save(), "关键模块失败后仍允许保存");
        criticalModule.ThrowOnApply = false;
        AssertEqual(SettingsLoadStatus.DefaultsApplied, critical.LoadAndApply(), "关键模块修复后无法重试初始化");
    }

    private static void VerifyRegistrationAndShutdownBoundaries()
    {
        var settings = CreateSettings(
            new ModuleMemorySaveService(),
            SaveSlot.Create("settings-module-lifecycle"));
        var module = new SamplePreferencesModule();
        settings.RegisterModule(module);
        AssertThrows<ArgumentException>(
            () => settings.RegisterModule(new SamplePreferencesModule()),
            "重复模块 ID 没有被拒绝");
        settings.LoadAndApply();
        AssertThrows<InvalidOperationException>(
            () => settings.RegisterModule(new SamplePreferencesModule("late-module")),
            "加载后仍允许注册模块");

        settings.Shutdown();
        settings.Shutdown();
        Assert(module.IsShutdown, "服务关闭没有关闭模块");
        AssertThrows<InvalidOperationException>(() => settings.LoadAndApply(), "关闭后仍允许加载");
        AssertThrows<InvalidOperationException>(() => settings.Save(), "关闭后仍允许保存");
        AssertThrows<InvalidOperationException>(() => settings.ResetToDefaults(), "关闭后仍允许重置");
        AssertThrows<InvalidOperationException>(() => settings.SetMasterVolume(0.5f), "关闭后仍允许修改设置");
    }

    private static void VerifyModuleMigrationAndSystemCompatibility()
    {
        const string moduleSlot = "godo-settings-module-sample-preferences";
        var saves = new ModuleMemorySaveService();
        SaveSlot systemSlot = SaveSlot.Create("settings-module-migration");
        saves.Save(
            systemSlot,
            new SettingsSnapshot { MasterVolume = 0.4f },
            SettingsCodec.CurrentVersion,
            new SettingsCodec());
        saves.Seed(moduleSlot, dataVersion: 1, SamplePreferencesModule.EncodeVersion1(3));

        var settings = CreateSettings(saves, systemSlot);
        var module = new SamplePreferencesModule();
        settings.RegisterModule(module);
        AssertEqual(SettingsLoadStatus.Loaded, settings.LoadAndApply(), "现有系统设置存档没有兼容读取");
        AssertEqual(0.4f, settings.Current.MasterVolume, "现有系统设置值没有恢复");
        AssertEqual(3, module.Current.NoticeLevel, "模块 v1 数据没有迁移");
        Assert(!module.Current.CompactPresentation, "模块 v1 缺失字段没有采用迁移默认值");
        settings.Save();
        AssertEqual(2, saves.GetVersion(moduleSlot), "迁移后没有按当前模块版本保存");
    }

    private static SettingsService CreateSettings(
        IAudioService audio,
        ISaveService saves,
        LocalizationService localization,
        ISettingsPlatformAdapter platform) =>
        new(
            audio,
            saves,
            localization,
            platform,
            SaveSlot.Create($"settings-memory-{Guid.NewGuid():N}"));

    private static SettingsService CreateSettings(ISaveService saves, SaveSlot settingsSlot) =>
        new(
            new RecordingAudioService(),
            saves,
            new LocalizationService(),
            new TestPlatformAdapter(SettingsCapability.None),
            settingsSlot);

    private static void Corrupt(SaveSlot slot, string suffix)
    {
        string path = $"user://saves/{slot.Value}.gdsave{suffix}";
        using GodotFileAccess? file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Write);
        if (file is null)
            throw new InvalidOperationException($"无法打开设置测试文件：{path}");
        file.StoreBuffer(new byte[] { 1, 2, 3, 4 });
        file.Flush();
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}；期望 {expected}，实际 {actual}");
    }

    private static TException AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class TestPlatformAdapter : ISettingsPlatformAdapter
    {
        public TestPlatformAdapter(SettingsCapability capabilities)
        {
            Capabilities = capabilities;
        }

        public SettingsPlatform Platform => SettingsPlatform.CommonOnly;
        public SettingsCapability Capabilities { get; }
        public SettingsApplyResult WindowModeResult { get; init; } = SettingsApplyResult.Applied;
        public SettingsApplyResult SetWindowMode(SettingsWindowMode mode) =>
            Capabilities.HasFlag(SettingsCapability.WindowMode)
                ? WindowModeResult
                : SettingsApplyResult.Unsupported;
        public SettingsApplyResult SetResolution(int width, int height) =>
            Capabilities.HasFlag(SettingsCapability.Resolution)
                ? SettingsApplyResult.Applied
                : SettingsApplyResult.Unsupported;
        public SettingsApplyResult SetVSync(bool enabled) =>
            Capabilities.HasFlag(SettingsCapability.VSync)
                ? SettingsApplyResult.Applied
                : SettingsApplyResult.Unsupported;
    }

    private sealed class TestSaveService : ISaveService
    {
        public SaveException? LoadFailure { get; set; }
        public SaveException? SaveFailure { get; set; }

        public void Save<T>(SaveSlot slot, T value, int dataVersion, ISaveCodec<T> codec)
        {
            if (SaveFailure != null)
                throw SaveFailure;
        }

        public SaveLoadResult<T> Load<T>(SaveSlot slot, ISaveCodec<T> codec)
        {
            if (LoadFailure != null)
                throw LoadFailure;
            return SaveLoadResult<T>.NotFound();
        }

        public bool Exists(SaveSlot slot) => false;
        public bool Delete(SaveSlot slot) => false;
    }

    private sealed class ModuleMemorySaveService : ISaveService
    {
        private readonly Dictionary<string, StoredValue> _values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _saveCounts = new(StringComparer.Ordinal);

        public void Save<T>(SaveSlot slot, T value, int dataVersion, ISaveCodec<T> codec)
        {
            byte[] payload = codec.Encode(value);
            _values[slot.Value] = new StoredValue(payload, dataVersion, false, null);
            _saveCounts.TryGetValue(slot.Value, out int count);
            _saveCounts[slot.Value] = count + 1;
        }

        public SaveLoadResult<T> Load<T>(SaveSlot slot, ISaveCodec<T> codec)
        {
            if (!_values.TryGetValue(slot.Value, out StoredValue? stored))
                return SaveLoadResult<T>.NotFound();

            T value = codec.Decode(stored.Payload, stored.DataVersion);
            return SaveLoadResult<T>.Loaded(
                value,
                stored.DataVersion,
                DateTimeOffset.UnixEpoch,
                stored.RecoveredFromBackup,
                stored.RecoveryFailure);
        }

        public bool Exists(SaveSlot slot) => _values.ContainsKey(slot.Value);

        public bool Delete(SaveSlot slot) => _values.Remove(slot.Value);

        public void Seed(string slot, int dataVersion, byte[] payload) =>
            _values[slot] = new StoredValue(payload, dataVersion, false, null);

        public void SeedRecovered(
            string slot,
            int dataVersion,
            byte[] payload,
            Exception recoveryFailure) =>
            _values[slot] = new StoredValue(payload, dataVersion, true, recoveryFailure);

        public int GetSaveCount(string slot) =>
            _saveCounts.TryGetValue(slot, out int count) ? count : 0;

        public int GetVersion(string slot) => _values[slot].DataVersion;

        private sealed record StoredValue(
            byte[] Payload,
            int DataVersion,
            bool RecoveredFromBackup,
            Exception? RecoveryFailure);
    }

    private sealed class FaultingSettingsModule : ISettingsModule<int>
    {
        public FaultingSettingsModule(string id, SettingsModuleFailurePolicy failurePolicy)
        {
            Id = id;
            FailurePolicy = failurePolicy;
        }

        public string Id { get; }
        public int Order => 0;
        public int CurrentVersion => 1;
        public SettingsModuleFailurePolicy FailurePolicy { get; }
        public bool ThrowOnApply { get; set; } = true;
        public int CreateDefaults() => 0;
        public int Decode(ReadOnlySpan<byte> payload, int dataVersion) => 0;
        public byte[] Encode(int settings) => BitConverter.GetBytes(settings);
        public void Validate(int settings) { }
        public void Apply(int settings)
        {
            if (ThrowOnApply)
                throw new InvalidOperationException("expected apply failure");
        }
        public int Capture() => 0;
        public void Shutdown() { }
    }

    private sealed class RecordingAudioService : IAudioService
    {
        private readonly Dictionary<AudioGroup, float> _volumes = new()
        {
            [AudioGroup.Master] = 1f,
            [AudioGroup.Bgm] = 1f,
            [AudioGroup.Sfx] = 1f,
        };

        public ResourceKey? CurrentBgm => null;
        public bool IsBgmPlaying => false;
        public bool IsBgmLoading => false;
        public BgmPlaybackState BgmState => BgmPlaybackState.Stopped;
        public int ActiveSfxCount => 0;
        public int PendingSfxCount => 0;
        public int PreparedSfxVoiceCount => 0;
        public int MaxSfxVoices => 0;
        public long RejectedSfxCount => 0;
        public long PreemptedSfxCount => 0;
        public int ActiveSfx3DCount => 0;
        public int PendingSfx3DCount => 0;
        public int PreparedSfx3DVoiceCount => 0;
        public int MaxSfx3DVoices => 0;
        public int FollowingSfx3DCount => 0;
        public int MaxFollowingSfx3DVoices => 0;
        public long RejectedSfx3DCount => 0;
        public long PreemptedSfx3DCount => 0;
        public Task PlayBgmAsync(ResourceKey key, bool restart = false) => Task.CompletedTask;
        public Task CrossfadeBgmAsync(
            ResourceKey key,
            double durationSeconds,
            System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FadeOutBgmAsync(
            double durationSeconds,
            System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void PauseBgm() { }
        public void ResumeBgm() { }
        public void StopBgm() { }
        public Task<bool> PlaySfxAsync(ResourceKey key) => Task.FromResult(false);
        public Task<bool> PlaySfxAsync(
            ResourceKey key,
            float volumeLinear,
            float pitchScale = 1f) => Task.FromResult(false);
        public Task<SfxPlaybackResult> PlaySfxAsync(
            ResourceKey key,
            SfxPlaybackOptions options) =>
            Task.FromResult(new SfxPlaybackResult());
        public Task PrepareSfxAsync(
            ResourceKey key,
            System.Threading.CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public int PrewarmSfxVoices(int targetVoiceCount) => 0;
        public Task<Sfx3DPlaybackResult> PlaySfx3DAsync(
            ResourceKey key,
            Vector3 globalPosition,
            Sfx3DPlaybackOptions options) =>
            Task.FromResult(new Sfx3DPlaybackResult());
        public Task<Sfx3DPlaybackResult> PlaySfx3DFollowAsync(
            ResourceKey key,
            Node3D target,
            Vector3 localOffset,
            Sfx3DPlaybackOptions options) =>
            Task.FromResult(new Sfx3DPlaybackResult());
        public int PrewarmSfx3DVoices(int targetVoiceCount) => 0;
        public bool IsSfx3DPlaying(Sfx3DPlaybackHandle handle) => false;
        public bool TryStopSfx3D(Sfx3DPlaybackHandle handle) => false;
        public void StopAllSfx3D() { }
        public bool IsSfxPlaying(SfxPlaybackHandle handle) => false;
        public bool TryStopSfx(SfxPlaybackHandle handle) => false;
        public void StopAllSfx() { }
        public float GetVolume(AudioGroup group) => _volumes[group];
        public void SetVolume(AudioGroup group, float linearVolume) => _volumes[group] = linearVolume;
    }
}
