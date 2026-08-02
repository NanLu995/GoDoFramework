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

            GD.Print($"[SettingsServiceRegression] PASS ({_passed}/5)");
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
        public int ActiveSfxCount => 0;
        public int MaxSfxVoices => 0;
        public Task PlayBgmAsync(ResourceKey key, bool restart = false) => Task.CompletedTask;
        public void PauseBgm() { }
        public void ResumeBgm() { }
        public void StopBgm() { }
        public Task<bool> PlaySfxAsync(ResourceKey key) => Task.FromResult(false);
        public void StopAllSfx() { }
        public float GetVolume(AudioGroup group) => _volumes[group];
        public void SetVolume(AudioGroup group, float linearVolume) => _volumes[group] = linearVolume;
    }
}
