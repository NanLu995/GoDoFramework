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
    private static readonly ResourceKey UiPageAKey =
        ResourceKey.Create("res://resources/UiTestPageA.tscn");
    private static readonly ResourceKey UiPageBKey =
        ResourceKey.Create("res://resources/UiTestPageB.tscn");
    private static readonly ResourceKey UiInvalidRootKey =
        ResourceKey.Create("res://resources/UiTestInvalidRoot.tscn");

    public override void _Ready()
    {
        try
        {
            RunConfigVerification();
            RunUiVerification();
            GD.Print("Config 与 UI 首版运行时验证通过。");
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Config 或 UI 首版运行时验证失败：{exception}");
            throw;
        }
    }

    private static void RunUiVerification()
    {
        IUiService ui = Services.Get<IUiService>();
        Assert(!ui.TryGoBack(), "初始 UI 栈应为空。");

        Control pageA = ui.OpenPage(UiPageAKey);
        Assert(pageA.Visible, "首个页面打开后应可见。");

        Control pageB = ui.OpenPage(UiPageBKey);
        Assert(!pageA.Visible && pageB.Visible, "新页面应隐藏前一页面。");

        Control modal = ui.OpenModal(UiPageAKey);
        Assert(IsInstanceValid(modal), "模态场景应成功实例化。");
        AssertThrows<InvalidOperationException>(() => ui.Close(pageB));
        Assert(ui.TryGoBack(), "返回应优先关闭顶部模态。");
        Assert(pageB.Visible, "关闭模态不应隐藏当前页面。");

        Assert(ui.TryGoBack(), "第二次返回应关闭顶部页面。");
        Assert(pageA.Visible, "关闭顶部页面后应恢复前一页面。");

        ui.Close(pageA);
        Assert(!ui.TryGoBack(), "关闭全部界面后返回栈应为空。");

        UiOpenException exception =
            AssertThrows<UiOpenException>(() => ui.OpenPage(UiInvalidRootKey));
        Assert(exception.Key == UiInvalidRootKey, "UI 打开异常应保留资源键。");
        Assert(!ui.TryGoBack(), "打开失败不应修改返回栈。");
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
