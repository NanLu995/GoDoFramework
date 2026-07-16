using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>Localization 与 Settings 职责协作的无交互回归验证入口。</summary>
public sealed partial class LocalizationRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("默认语言与可用语言", VerifyLocales);
            Run("翻译、上下文与复数", VerifyTranslations);
            Run("Settings 切换、规范化与事件", VerifySettingsIntegration);
            Run("非法 Locale 保持原状态", VerifyInvalidLocale);
            Run("Settings Locale 内存持久化往返", VerifySettingsPersistence);
            Run("伪本地化运行时开关", VerifyPseudolocalization);
            Run("无翻译资源的核心包回退", VerifyEmptyTranslationFallback);

            GD.Print($"[LocalizationRegression] PASS ({_passed}/7)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[LocalizationRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[LocalizationRegression] PASS: {name}");
    }

    private static void VerifyLocales()
    {
        ILocalizationService localization = Services.Get<ILocalizationService>();
        AssertEqual("en", localization.DefaultLocale, "默认 Locale 错误");
        AssertEqual("en", localization.CurrentLocale, "启动 Locale 错误");
        Assert(localization.IsLocaleSupported("en"), "默认 Locale 未被支持");
        Assert(localization.IsLocaleSupported("fr"), "已加载的法语未被支持");
        Assert(!localization.IsLocaleSupported("zz"), "未加载 Locale 被错误接受");
        Assert(ContainsLocale(localization.AvailableLocales, "en"), "可用语言缺少英语");
        Assert(ContainsLocale(localization.AvailableLocales, "fr"), "可用语言缺少法语");
    }

    private static void VerifyTranslations()
    {
        ILocalizationService localization = Services.Get<ILocalizationService>();
        AssertEqual("Hello", localization.Translate("TEST.GREETING"), "默认语言翻译错误");
        AssertEqual("Open", localization.Translate("TEST.ACTION", "BUTTON"), "上下文翻译错误");
        AssertEqual("%d item", localization.TranslatePlural("TEST.ITEM", "TEST.ITEM_PLURAL", 1), "单数翻译错误");
        AssertEqual("%d items", localization.TranslatePlural("TEST.ITEM", "TEST.ITEM_PLURAL", 2), "复数翻译错误");
        AssertEqual("TEST.MISSING", localization.Translate("TEST.MISSING"), "缺失键未回退到源键");
    }

    private static void VerifySettingsIntegration()
    {
        ILocalizationService localization = Services.Get<ILocalizationService>();
        ISettingsService settings = Services.Get<ISettingsService>();
        int eventCount = 0;
        LocaleChangedEvent captured = default;
        void OnLocaleChanged(LocaleChangedEvent evt)
        {
            eventCount++;
            captured = evt;
        }

        EventChannel.On<LocaleChangedEvent>(OnLocaleChanged);
        try
        {
            SettingsApplyResult result = settings.SetLocale("fr-FR");
            AssertEqual(SettingsApplyResult.Applied, result, "语言切换结果错误");
            AssertEqual("fr_FR", settings.Current.Locale, "Settings 未保存规范 Locale");
            AssertEqual("fr_FR", localization.CurrentLocale, "Localization 当前 Locale 错误");
            AssertEqual("en", captured.PreviousLocale, "事件旧 Locale 错误");
            AssertEqual("fr_FR", captured.CurrentLocale, "事件新 Locale 错误");
            AssertEqual(1, eventCount, "语言变更事件数量错误");
            AssertEqual("Bonjour", localization.Translate("TEST.GREETING"), "切换后翻译错误");

            settings.SetLocale("fr-FR");
            AssertEqual(1, eventCount, "重复设置同一规范 Locale 仍然广播事件");
        }
        finally
        {
            EventChannel.Off<LocaleChangedEvent>(OnLocaleChanged);
            settings.SetLocale("en");
        }
    }

    private static void VerifyInvalidLocale()
    {
        ILocalizationService localization = Services.Get<ILocalizationService>();
        ISettingsService settings = Services.Get<ISettingsService>();
        AssertThrows<ArgumentException>(() => settings.SetLocale("zz"), "未加载 Locale 未被拒绝");
        AssertEqual("en", settings.Current.Locale, "失败后 Settings Locale 被修改");
        AssertEqual("en", localization.CurrentLocale, "失败后 Localization Locale 被修改");
    }

    private static void VerifySettingsPersistence()
    {
        IAudioService audio = Services.Get<IAudioService>();
        var localization = (LocalizationService)Services.Get<ILocalizationService>();
        var saves = new InMemorySaveService();
        SaveSlot slot = SaveSlot.Create("localization-regression");
        var platform = new CommonSettingsPlatformAdapter(SettingsPlatform.CommonOnly);

        try
        {
            var writer = new SettingsService(audio, saves, localization, platform, slot);
            writer.SetLocale("fr-FR");
            writer.Save();
            writer.SetLocale("en");

            var reader = new SettingsService(audio, saves, localization, platform, slot);
            SettingsLoadStatus status = reader.LoadAndApply();
            AssertEqual(SettingsLoadStatus.Loaded, status, "Settings 读取来源错误");
            AssertEqual("fr_FR", reader.Current.Locale, "Settings Locale 往返错误");
            AssertEqual("fr_FR", localization.CurrentLocale, "持久化 Locale 未重新应用");
        }
        finally
        {
            Services.Get<ISettingsService>().SetLocale("en");
        }
    }

    private static void VerifyPseudolocalization()
    {
        bool original = TranslationServer.PseudolocalizationEnabled;
        try
        {
            TranslationServer.PseudolocalizationEnabled = !original;
            ILocalizationService localization = Services.Get<ILocalizationService>();
            AssertEqual(!original, localization.IsPseudolocalizationEnabled, "伪本地化诊断状态错误");
        }
        finally
        {
            TranslationServer.PseudolocalizationEnabled = original;
        }
    }

    private static void VerifyEmptyTranslationFallback()
    {
        TranslationServer.Clear();
        var localization = new LocalizationService();
        AssertEqual("en", localization.DefaultLocale, "空项目默认 Locale 错误");
        AssertEqual("en", localization.CurrentLocale, "空项目当前 Locale 错误");
        Assert(localization.IsLocaleSupported("en"), "空项目不支持默认 Locale");
        Assert(!localization.IsLocaleSupported("fr"), "空项目错误支持未加载 Locale");
        AssertEqual("TEST.GREETING", localization.Translate("TEST.GREETING"), "空项目未回退到源键");
    }

    private static bool ContainsLocale(IReadOnlyList<LocalizationLocale> locales, string code)
    {
        for (int i = 0; i < locales.Count; i++)
        {
            if (locales[i].Code == code)
                return true;
        }

        return false;
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

    private sealed class InMemorySaveService : ISaveService
    {
        private byte[]? _payload;
        private int _dataVersion;

        public void Save<T>(SaveSlot slot, T value, int dataVersion, ISaveCodec<T> codec)
        {
            _payload = codec.Encode(value);
            _dataVersion = dataVersion;
        }

        public SaveLoadResult<T> Load<T>(SaveSlot slot, ISaveCodec<T> codec)
        {
            if (_payload == null)
                return SaveLoadResult<T>.NotFound();

            T value = codec.Decode(_payload, _dataVersion);
            return SaveLoadResult<T>.Loaded(value, _dataVersion, DateTimeOffset.UtcNow, recoveredFromBackup: false);
        }

        public bool Exists(SaveSlot slot) => _payload != null;

        public bool Delete(SaveSlot slot)
        {
            bool existed = _payload != null;
            _payload = null;
            _dataVersion = 0;
            return existed;
        }
    }
}
