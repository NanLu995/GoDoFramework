using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

/// <summary>当前开发模块的临时运行时验证入口。</summary>
public sealed partial class TestScene : Node
{
    private static readonly ResourceKey ValidConfigKey =
        ResourceKey.Create("res://resources/ConfigTestValid.tres");
    private static readonly ResourceKey InvalidConfigKey =
        ResourceKey.Create("res://resources/ConfigTestInvalid.tres");
    public override void _Ready()
    {
        try
        {
            RunConfigVerification();
            GD.Print("Config 首版运行时验证通过。");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Config 首版运行时验证失败：{exception}");
            throw;
        }
    }

    private static void RunConfigVerification()
    {
        ConfigTestResource config = ConfigHub.Load<ConfigTestResource>(ValidConfigKey);
        Assert(config.Id == "valid" && config.Value == 42, "有效配置应完成强类型加载。");

        ConfigValidationException validationException =
            AssertThrows<ConfigValidationException>(
                () => ConfigHub.Load<ConfigTestResource>(InvalidConfigKey));
        Assert(validationException.Key == InvalidConfigKey, "校验异常应保留资源键。");
        Assert(validationException.ConfigType == typeof(ConfigTestResource), "校验异常应保留配置类型。");
        Assert(validationException.InnerException is InvalidOperationException,
            "校验异常应保留具体失败原因。");

        AssertThrows<ResourceLoadException>(
            () => ConfigHub.Load<ConfigTestResource>(
                ResourceKey.Create("res://resources/MissingConfig.tres")));

        var entries = new[]
        {
            new TestEntry("first", 1),
            new TestEntry("second", 2),
        };
        var table = new ConfigTable<string, TestEntry>(entries, entry => entry.Id);
        Assert(table.Count == 2, "配置表数量应与输入一致。");
        Assert(table.Get("second").Value == 2, "Get 应返回对应配置项。");
        Assert(table.TryGet("first", out TestEntry? first) && first is not null && first.Value == 1,
            "TryGet 应返回对应配置项。");
        Assert(!table.TryGet("missing", out _), "TryGet 对缺失键应返回 false。");
        AssertThrows<KeyNotFoundException>(() => table.Get("missing"));
        AssertThrows<ArgumentException>(() => new ConfigTable<string, TestEntry>(
            new[] { new TestEntry("same", 1), new TestEntry("same", 2) },
            entry => entry.Id));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static TException AssertThrows<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException($"预期抛出 {typeof(TException).Name}。");
    }

    private sealed record TestEntry(string Id, int Value);
}
