using System;
using System.Collections.Generic;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>ResourceKey 的无交互回归验证入口。</summary>
public sealed partial class ResourceKeyRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("合法路径与规范化", VerifyValidPathAndNormalization);
            Run("拒绝空路径与非 res 路径", VerifyRootRejection);
            Run("拒绝非规范化路径", VerifyMalformedPathRejection);
            Run("拒绝父目录跳转", VerifyParentTraversalRejection);
            Run("区分大小写的相等性", VerifyEquality);
            Run("默认值与哈希集合", VerifyDefaultAndHashing);
            Run("UID 资源键", VerifyUidKey);
            Run("UID 反查回退", VerifyResolveUidFallback);

            GD.Print($"[ResourceKeyRegression] PASS ({_passed}/8)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[ResourceKeyRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[ResourceKeyRegression] PASS: {name}");
    }

    private static void VerifyValidPathAndNormalization()
    {
        ResourceKey key = ResourceKey.Create("  res://Scenes\\Level01.tscn  ");

        Assert(key.IsValid, "合法 ResourceKey 被标记为无效");
        Assert(!key.IsUid, "res:// ResourceKey 被误判为 UID");
        AssertEqual("res://Scenes/Level01.tscn", key.Value, "路径没有正确规范化");
        AssertEqual(key.Value, key.ToString(), "ToString 与 Value 不一致");
        AssertEqual(key, ResourceKey.FromPath("res://Scenes/Level01.tscn"), "FromPath 与 Create 行为不一致");
    }

    private static void VerifyRootRejection()
    {
        string?[] invalidPaths =
        {
            null,
            string.Empty,
            "   ",
            "Scenes/Level01.tscn",
            "user://save.dat",
            "https://example.com/asset.tres",
            "res://",
        };

        foreach (string? path in invalidPaths)
            AssertInvalid(path);
    }

    private static void VerifyMalformedPathRejection()
    {
        string[] invalidPaths =
        {
            "res:///Level01.tscn",
            "res://./Level01.tscn",
            "res://Scenes//Level01.tscn",
            "res://Scenes/./Level01.tscn",
            "res://Scenes/.",
            "res://Scenes/",
        };

        foreach (string path in invalidPaths)
            AssertInvalid(path);
    }

    private static void VerifyParentTraversalRejection()
    {
        string[] invalidPaths =
        {
            "res://../Level01.tscn",
            "res://Scenes/../Level01.tscn",
            "res://Scenes/..",
        };

        foreach (string path in invalidPaths)
            AssertInvalid(path);
    }

    private static void VerifyEquality()
    {
        ResourceKey first = ResourceKey.Create("res://Scenes/Level01.tscn");
        ResourceKey same = ResourceKey.Create("res://Scenes/Level01.tscn");
        ResourceKey differentCase = ResourceKey.Create("res://scenes/Level01.tscn");

        Assert(first == same, "相同路径没有判定为相等");
        Assert(first.Equals(same), "Equals 与相等运算符不一致");
        Assert(first != differentCase, "路径比较没有区分大小写");
        Assert(!first.Equals((object)differentCase), "object Equals 没有区分大小写");
    }

    private static void VerifyDefaultAndHashing()
    {
        ResourceKey empty = default;
        ResourceKey first = ResourceKey.Create("res://Config/Game.tres");
        ResourceKey same = ResourceKey.Create("res://Config/Game.tres");
        var set = new HashSet<ResourceKey> { first, same };

        Assert(!empty.IsValid, "默认 ResourceKey 被标记为有效");
        AssertEqual(string.Empty, empty.Value, "默认 ResourceKey.Value 不是空字符串");
        AssertEqual(0, empty.GetHashCode(), "默认 ResourceKey 哈希值不为 0");
        AssertEqual(1, set.Count, "相等 ResourceKey 在 HashSet 中没有合并");
    }

    private static void VerifyUidKey()
    {
        ResourceKey key = ResourceKey.Create("  uid://c8k2n4m8xj3fa  ");

        Assert(key.IsValid, "合法 UID ResourceKey 被标记为无效");
        Assert(key.IsUid, "UID ResourceKey 没有标记 IsUid");
        AssertEqual("uid://c8k2n4m8xj3fa", key.Value, "UID 不应按路径规则规范化");
        AssertEqual(key, ResourceKey.FromUid("uid://c8k2n4m8xj3fa"), "FromUid 与 Create 行为不一致");

        AssertInvalid("uid://");
        AssertInvalid("uid://   ");
    }

    private static void VerifyResolveUidFallback()
    {
        ResourceKey key = ResourceKey.ResolveUid("res://Verification/Automated/MissingResourceForUid.tres");

        Assert(!key.IsUid, "不存在 UID 的路径不应返回 UID ResourceKey");
        AssertEqual(
            "res://Verification/Automated/MissingResourceForUid.tres",
            key.Value,
            "ResolveUid 找不到 UID 时没有回退到原始路径");
    }

    private static void AssertInvalid(string? path)
    {
        try
        {
            ResourceKey.Create(path!);
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException($"无效路径未被拒绝：{path ?? "<null>"}");
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
}
