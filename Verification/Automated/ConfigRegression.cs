using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>Config 的无交互回归验证入口。</summary>
public sealed partial class ConfigRegression : Node
{
    private static readonly ResourceKey ValidKey =
        ResourceKey.Create("res://resources/ConfigTestValid.tres");
    private static readonly ResourceKey InvalidKey =
        ResourceKey.Create("res://resources/ConfigTestInvalid.tres");
    private static readonly ResourceKey MissingKey =
        ResourceKey.Create("res://resources/ConfigTestMissing.tres");

    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("有效配置加载与校验", VerifyValidConfig);
            Run("无效配置异常上下文", VerifyInvalidConfig);
            Run("缺失资源异常透传", VerifyMissingConfig);
            Run("ConfigTable 查询与比较器", VerifyTableLookup);
            Run("ConfigTable 缺失键", VerifyMissingTableKey);
            Run("ConfigTable 拒绝重复键与空项", VerifyInvalidTableEntries);

            GD.Print($"[ConfigRegression] PASS ({_passed}/6)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[ConfigRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[ConfigRegression] PASS: {name}");
    }

    private static void VerifyValidConfig()
    {
        ConfigTestResource config = ConfigHub.Load<ConfigTestResource>(ValidKey);
        AssertEqual("valid", config.Id, "有效配置 Id 错误");
        AssertEqual(42, config.Value, "有效配置 Value 错误");
    }

    private static void VerifyInvalidConfig()
    {
        ConfigValidationException exception = AssertThrows<ConfigValidationException>(
            static () => ConfigHub.Load<ConfigTestResource>(InvalidKey),
            "无效配置没有抛出 ConfigValidationException");

        AssertEqual(InvalidKey, exception.Key, "校验异常 Key 错误");
        AssertEqual(typeof(ConfigTestResource), exception.ConfigType, "校验异常配置类型错误");
        Assert(exception.InnerException is InvalidOperationException, "校验异常没有保留原始异常");
    }

    private static void VerifyMissingConfig()
    {
        ResourceLoadException exception = AssertThrows<ResourceLoadException>(
            static () => ConfigHub.Load<ConfigTestResource>(MissingKey),
            "缺失配置没有透传 ResourceLoadException");
        AssertEqual(MissingKey, exception.Key, "缺失配置异常 Key 错误");
    }

    private static void VerifyTableLookup()
    {
        Entry[] entries =
        {
            new("alpha", 1),
            new("BETA", 2),
        };
        var table = new ConfigTable<string, Entry>(
            entries,
            static entry => entry.Id,
            StringComparer.OrdinalIgnoreCase);

        AssertEqual(2, table.Count, "ConfigTable Count 错误");
        AssertEqual(1, table.Get("ALPHA").Value, "Get 没有使用指定比较器");
        Assert(table.TryGet("beta", out Entry? beta), "TryGet 没有找到已有键");
        AssertEqual(2, beta!.Value, "TryGet 返回配置项错误");
    }

    private static void VerifyMissingTableKey()
    {
        var table = new ConfigTable<string, Entry>(
            new[] { new Entry("known", 1) },
            static entry => entry.Id);

        Assert(!table.TryGet("missing", out Entry? missing), "缺失键 TryGet 返回 true");
        Assert(missing is null, "缺失键 TryGet 没有输出 null");
        AssertThrows<KeyNotFoundException>(
            () => table.Get("missing"),
            "缺失键 Get 没有抛出 KeyNotFoundException");
    }

    private static void VerifyInvalidTableEntries()
    {
        AssertThrows<ArgumentException>(
            static () => new ConfigTable<string, Entry>(
                new[] { new Entry("same", 1), new Entry("same", 2) },
                static entry => entry.Id),
            "重复键没有被拒绝");

        AssertThrows<ArgumentException>(
            static () => new ConfigTable<string, Entry>(
                new Entry[] { new("valid", 1), null! },
                static entry => entry.Id),
            "空配置项没有被拒绝");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}；期望 {expected}，实际 {actual}");
        }
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

    private sealed record Entry(string Id, int Value);
}
