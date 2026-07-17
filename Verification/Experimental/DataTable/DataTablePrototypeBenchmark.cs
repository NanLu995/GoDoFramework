using System;
using System.Diagnostics;
using System.IO;
using Godot;
using GoDoFramework.Verification.DataTablePrototype.Generated;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>DataTable 阶段 A 跨语言二进制读取与 Windows 基础性能验证入口。</summary>
public sealed partial class DataTablePrototypeBenchmark : Node
{
    private const int ExpectedItemCount = 10_000;
    private const int LookupCount = 100_000;

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            string outputDirectory = ProjectSettings.GlobalizePath(
                "res://Verification/Experimental/DataTable/Artifacts/output");
            string categoriesPath = Path.Combine(outputDirectory, "ItemCategory.gdtb");
            string itemsPath = Path.Combine(outputDirectory, "Item.gdtb");

            VerifySemantics(categoriesPath, itemsPath);
            VerifyCorruptionFailures(outputDirectory);
            BenchmarkLoad(categoriesPath, itemsPath);
            BenchmarkLookup(itemsPath);
            GD.Print("[DataTablePrototypeBenchmark] PASS (4/4)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[DataTablePrototypeBenchmark] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void VerifySemantics(string categoriesPath, string itemsPath)
    {
        ItemCategoryTable categories = DataTablePrototypeLoader.LoadItemCategory(categoriesPath);
        ItemTable items = DataTablePrototypeLoader.LoadItem(itemsPath);

        Assert(categories.Count == 4, "ItemCategory 行数不正确");
        Assert(items.Count == ExpectedItemCount, "Item 行数不正确");
        Assert(categories.Get("equipment").DisplayName == "装备", "UTF-8 字符串池读取错误");

        ItemRow first = items.Get("item_00001");
        Assert(first.CategoryId == "consumable", "外键字段读取错误");
        Assert(first.Rarity == ItemRarity.Common, "enum 字段读取错误");
        Assert(items.Get("item_00011").Enabled, "bool 默认值语义错误");
        Assert(items.Get("item_00013").MaxStack == 1, "int 默认值语义错误");
        Assert(items.Get("item_00001").Description == string.Empty, "空字符串语义错误");
        Assert(items.Get("item_00005").Description is null, "null token 语义错误");
        Assert(!items.TryGet("missing", out _), "缺失主键查询错误");
        GD.Print("[DataTablePrototypeBenchmark] PASS: 二进制语义");
    }

    private static void VerifyCorruptionFailures(string outputDirectory)
    {
        string corruptionDirectory = Path.Combine(
            Directory.GetParent(outputDirectory)!.FullName,
            "corruption");
        (string FileName, string Message)[] cases =
        {
            ("bad-magic.gdtb", "magic"),
            ("bad-format-version.gdtb", "格式版本"),
            ("bad-schema-version.gdtb", "schema 版本"),
            ("tampered-payload.gdtb", "payload 摘要"),
            ("truncated.gdtb", "payload 摘要"),
            ("bad-string-index.gdtb", "字符串池索引越界"),
            ("bad-primary-index.gdtb", "主键索引无效"),
        };

        foreach ((string fileName, string message) in cases)
        {
            string path = Path.Combine(corruptionDirectory, fileName);
            AssertInvalidData(
                () => DataTablePrototypeLoader.LoadItem(path),
                message,
                fileName);
        }
        GD.Print($"[DataTablePrototypeBenchmark] PASS: 损坏与版本拒绝 ({cases.Length}/7)");
    }

    private static void BenchmarkLoad(string categoriesPath, string itemsPath)
    {
        _ = DataTablePrototypeLoader.LoadItemCategory(categoriesPath);
        _ = DataTablePrototypeLoader.LoadItem(itemsPath);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        ItemCategoryTable categories = DataTablePrototypeLoader.LoadItemCategory(categoriesPath);
        ItemTable items = DataTablePrototypeLoader.LoadItem(itemsPath);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        long memoryAfter = GC.GetTotalMemory(forceFullCollection: true);

        long binaryBytes = new FileInfo(categoriesPath).Length + new FileInfo(itemsPath).Length;
        GD.Print(
            $"[DataTablePrototypeBenchmark] Load: Build={BuildConfiguration}; " +
            $"Rows={categories.Count + items.Count}; " +
            $"BinaryBytes={binaryBytes}; ElapsedMs={elapsed.TotalMilliseconds:F3}; " +
            $"AllocatedBytes={allocated}; RetainedManagedBytes={memoryAfter - memoryBefore}");
    }

    private static void BenchmarkLookup(string itemsPath)
    {
        ItemTable items = DataTablePrototypeLoader.LoadItem(itemsPath);
        var ids = new string[ExpectedItemCount];
        for (int index = 0; index < ids.Length; index++)
            ids[index] = $"item_{index + 1:00000}";

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        double weightSum = 0;
        for (int index = 0; index < LookupCount; index++)
            weightSum += items.Get(ids[index % ExpectedItemCount]).Weight;
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert(weightSum > 0, "查询结果未被消费");
        Assert(allocated == 0, $"预生成键查询产生托管分配：{allocated} bytes");
        GD.Print(
            $"[DataTablePrototypeBenchmark] Lookup: Count={LookupCount}; " +
            $"ElapsedMs={elapsed.TotalMilliseconds:F3}; AllocatedBytes={allocated}");
    }

    private static void AssertInvalidData(Action action, string messageFragment, string caseName)
    {
        try
        {
            action();
        }
        catch (InvalidDataException exception)
        {
            Assert(
                exception.Message.Contains(messageFragment, StringComparison.Ordinal),
                $"{caseName} 的异常消息不明确：{exception.Message}");
            return;
        }

        throw new InvalidOperationException($"损坏样例 {caseName} 未被拒绝");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
