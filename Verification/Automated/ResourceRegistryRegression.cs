using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>ResourceRegistry 的无交互回归验证入口。</summary>
public sealed partial class ResourceRegistryRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("未加载状态", VerifyUnloadedState);
            Run("加载与解析", VerifyLoadAndResolve);
            Run("TryResolve 缺失 ID", VerifyTryResolveMissing);
            Run("重复 ID 覆盖", VerifyDuplicateOverride);
            Run("合并加载", VerifyLoadMerge);
            Run("清空", VerifyClear);

            GD.Print($"[ResourceRegistryRegression] PASS ({_passed}/6)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[ResourceRegistryRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        ResourceRegistry.Clear();
        verification();
        _passed++;
        GD.Print($"[ResourceRegistryRegression] PASS: {name}");
    }

    private static void VerifyUnloadedState()
    {
        AssertThrows<InvalidOperationException>(
            static () => ResourceRegistry.Resolve("existing"),
            "未加载时 Resolve 没有失败");
        Assert(!ResourceRegistry.TryResolve("existing", out ResourceKey key), "未加载时 TryResolve 返回 true");
        Assert(!key.IsValid, "未加载时 TryResolve 输出了有效 ResourceKey");
        AssertEqual(0, ResourceRegistry.Count, "未加载时 Count 不为 0");
    }

    private static void VerifyLoadAndResolve()
    {
        ResourceManifest manifest = CreateManifest(
            Entry("config/main", "res://Config/Main.tres"),
            Entry("ui/close", "uid://c8k2n4m8xj3fa"),
            Entry(string.Empty, "res://Skipped.tres"));

        ResourceRegistry.Load(manifest);

        AssertEqual(2, ResourceRegistry.Count, "空 Id 记录没有被跳过");
        AssertEqual(
            ResourceKey.FromPath("res://Config/Main.tres"),
            ResourceRegistry.Resolve("config/main"),
            "res:// 记录解析错误");
        AssertEqual(
            ResourceKey.FromUid("uid://c8k2n4m8xj3fa"),
            ResourceRegistry.Resolve("ui/close"),
            "uid:// 记录解析错误");
        Assert(ResourceRegistry.TryResolve("ui/close", out ResourceKey key), "TryResolve 没有找到已有 ID");
        Assert(key.IsUid, "TryResolve 返回的 UID key 没有标记 IsUid");

        KeyNotFoundException exception = AssertThrows<KeyNotFoundException>(
            static () => ResourceRegistry.Resolve("missing"),
            "缺失 ID 没有抛出 KeyNotFoundException");
        Assert(exception.Message.Contains("missing", StringComparison.Ordinal), "缺失 ID 异常没有包含 ID");
    }

    private static void VerifyTryResolveMissing()
    {
        ResourceRegistry.Load(CreateManifest(Entry("known", "res://Known.tres")));

        Assert(!ResourceRegistry.TryResolve("missing", out ResourceKey key), "缺失 ID TryResolve 返回 true");
        Assert(!key.IsValid, "缺失 ID TryResolve 输出了有效 ResourceKey");
    }

    private static void VerifyDuplicateOverride()
    {
        ResourceRegistry.Load(CreateManifest(
            Entry("same", "res://First.tres"),
            Entry("same", "uid://second")));

        AssertEqual(1, ResourceRegistry.Count, "重复 ID 没有覆盖为单条记录");
        AssertEqual(ResourceKey.FromUid("uid://second"), ResourceRegistry.Resolve("same"), "重复 ID 没有以后者覆盖前者");
    }

    private static void VerifyLoadMerge()
    {
        ResourceManifest first = CreateManifest(Entry("a", "res://A.tres"));
        ResourceManifest second = CreateManifest(Entry("b", "uid://b"));

        ResourceRegistry.LoadMerge(new[] { first, second });

        AssertEqual(2, ResourceRegistry.Count, "LoadMerge 没有合并多个清单");
        AssertEqual(ResourceKey.FromPath("res://A.tres"), ResourceRegistry.Resolve("a"), "LoadMerge 第一份清单解析错误");
        AssertEqual(ResourceKey.FromUid("uid://b"), ResourceRegistry.Resolve("b"), "LoadMerge 第二份清单解析错误");
    }

    private static void VerifyClear()
    {
        ResourceRegistry.Load(CreateManifest(Entry("known", "res://Known.tres")));
        ResourceRegistry.Clear();

        AssertEqual(0, ResourceRegistry.Count, "Clear 后 Count 不为 0");
        Assert(!ResourceRegistry.TryResolve("known", out _), "Clear 后 TryResolve 仍能解析旧 ID");
    }

    private static ResourceManifest CreateManifest(params ResourceManifestEntry[] entries)
    {
        var manifest = new ResourceManifest();
        foreach (ResourceManifestEntry entry in entries)
            manifest.Entries.Add(entry);
        return manifest;
    }

    private static ResourceManifestEntry Entry(string id, string locator) =>
        new()
        {
            Id = id,
            Locator = locator,
        };

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
}
