using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Godot;
using GoDoFramework.Verification.DataTablePrototype.Generated;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>DataTable 阶段 A 跨语言二进制读取与 Windows 基础性能验证入口。</summary>
public sealed partial class DataTablePrototypeBenchmark : Node
{
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
            string artifactRoot = Directory.GetParent(outputDirectory)!.FullName;
            string compressionDirectory = Path.Combine(artifactRoot, "compression");
            string selectedDirectory = Path.Combine(artifactRoot, "selected");
            string compressedCategoriesPath = Path.Combine(
                compressionDirectory,
                "ItemCategory.gdtb");
            string compressedItemsPath = Path.Combine(compressionDirectory, "Item.gdtb");
            int expectedItemCount = int.Parse(
                File.ReadAllText(Path.Combine(outputDirectory, "benchmark_rows.txt")));

            VerifySemantics(categoriesPath, itemsPath, expectedItemCount);
            VerifyResPathSemantics(expectedItemCount);
            VerifyPckSemantics(categoriesPath, itemsPath, expectedItemCount);
            VerifyCorruptionFailures(outputDirectory);
            VerifyCompressedSemantics(
                compressedCategoriesPath,
                compressedItemsPath,
                selectedDirectory,
                categoriesPath,
                itemsPath,
                expectedItemCount);
            LoadMeasurement uncompressedLoad =
                BenchmarkLoad("None", categoriesPath, itemsPath);
            LoadMeasurement zstdLoad =
                BenchmarkLoad("Zstd", compressedCategoriesPath, compressedItemsPath);
            AssertZstdAllocation(
                uncompressedLoad,
                zstdLoad,
                compressedCategoriesPath,
                compressedItemsPath);
            BenchmarkLookup("None", itemsPath, expectedItemCount);
            BenchmarkLookup("Zstd", compressedItemsPath, expectedItemCount);
            GD.Print("[DataTablePrototypeBenchmark] PASS (10/10)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[DataTablePrototypeBenchmark] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void VerifySemantics(
        string categoriesPath,
        string itemsPath,
        int expectedItemCount)
    {
        ItemCategoryTable categories = DataTableLoader.LoadItemCategory(categoriesPath);
        ItemTable items = DataTableLoader.LoadItem(itemsPath);

        Assert(categories.Count == 4, "ItemCategory 行数不正确");
        Assert(items.Count == expectedItemCount, "Item 行数不正确");
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

    private static void VerifyResPathSemantics(int expectedItemCount)
    {
        ItemCategoryTable categories = DataTableLoader.LoadItemCategory(
            "res://Verification/Experimental/DataTable/Artifacts/output/ItemCategory.gdtb");
        ItemTable items = DataTableLoader.LoadItem(
            "res://Verification/Experimental/DataTable/Artifacts/output/Item.gdtb");

        Assert(categories.Count == 4, "res:// ItemCategory 行数不正确");
        Assert(items.Count == expectedItemCount, "res:// Item 行数不正确");
        GD.Print("[DataTablePrototypeBenchmark] PASS: res:// 读取语义");
    }

    private static void VerifyPckSemantics(
        string categoriesPath,
        string itemsPath,
        int expectedItemCount)
    {
        string pckPath = ProjectSettings.GlobalizePath("user://datatable-prototype-test.pck");
        if (File.Exists(pckPath))
            File.Delete(pckPath);
        using (var packer = new PckPacker())
        {
            Assert(packer.PckStart(pckPath) == Error.Ok, "测试 PCK 创建失败");
            Assert(
                packer.AddFile("res://__godo_datatable_pck_test/ItemCategory.gdtb", categoriesPath) == Error.Ok,
                "ItemCategory 加入测试 PCK 失败");
            Assert(
                packer.AddFile("res://__godo_datatable_pck_test/Item.gdtb", itemsPath) == Error.Ok,
                "Item 加入测试 PCK 失败");
            Assert(packer.Flush() == Error.Ok, "测试 PCK 写入失败");
        }
        Assert(ProjectSettings.LoadResourcePack(pckPath), "测试 PCK 加载失败");
        ItemCategoryTable categories = DataTableLoader.LoadItemCategory(
            "res://__godo_datatable_pck_test/ItemCategory.gdtb");
        ItemTable items = DataTableLoader.LoadItem(
            "res://__godo_datatable_pck_test/Item.gdtb");
        Assert(categories.Count == 4, "PCK ItemCategory 行数不正确");
        Assert(items.Count == expectedItemCount, "PCK Item 行数不正确");
        GD.Print("[DataTablePrototypeBenchmark] PASS: PCK res:// 读取语义");
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
            ("bad-flags.gdtb", "未知 flags"),
            ("tampered-payload.gdtb", "payload 摘要"),
            ("truncated.gdtb", "未压缩 payload 大小"),
            ("bad-string-index.gdtb", "字符串池索引越界"),
            ("bad-primary-index.gdtb", "主键索引无效"),
        };

        foreach ((string fileName, string message) in cases)
        {
            string path = Path.Combine(corruptionDirectory, fileName);
            AssertInvalidData(
                () => DataTableLoader.LoadItem(path),
                message,
                fileName);
        }
        string compressedCorruptionDirectory = Path.Combine(
            Directory.GetParent(outputDirectory)!.FullName,
            "compression-corruption");
        (string FileName, string Message)[] compressedCases =
        {
            ("tampered-zstd.gdtb", "Zstd 解压"),
            ("bad-uncompressed-size.gdtb", "Zstd 解压"),
            ("bad-payload-hash.gdtb", "payload 摘要"),
        };
        foreach ((string fileName, string message) in compressedCases)
        {
            string path = Path.Combine(compressedCorruptionDirectory, fileName);
            AssertInvalidData(
                () => DataTableLoader.LoadItem(path),
                message,
                fileName);
        }
        GD.Print(
            $"[DataTablePrototypeBenchmark] PASS: 损坏与版本拒绝 " +
            $"({cases.Length + compressedCases.Length}/11)");
    }

    private static void VerifyCompressedSemantics(
        string categoriesPath,
        string itemsPath,
        string selectedDirectory,
        string uncompressedCategoriesPath,
        string uncompressedItemsPath,
        int expectedItemCount)
    {
        ItemCategoryTable categories = DataTableLoader.LoadItemCategory(categoriesPath);
        ItemTable items = DataTableLoader.LoadItem(itemsPath);
        Assert(categories.Count == 4, "Zstd ItemCategory 行数不正确");
        Assert(items.Count == expectedItemCount, "Zstd Item 行数不正确");
        Assert(items.Get("item_00005").Description is null, "Zstd null 语义错误");

        Assert(
            File.ReadAllBytes(Path.Combine(selectedDirectory, "ItemCategory.gdtb"))
                .AsSpan()
                .SequenceEqual(File.ReadAllBytes(uncompressedCategoriesPath)),
            "Auto 错误选择了 ItemCategory Zstd 候选");
        Assert(
            File.ReadAllBytes(Path.Combine(selectedDirectory, "Item.gdtb"))
                .AsSpan()
                .SequenceEqual(File.ReadAllBytes(uncompressedItemsPath)),
            "Auto 错误选择了 Item Zstd 候选");
        GD.Print("[DataTablePrototypeBenchmark] PASS: Zstd 与 Auto 保守选择语义");
    }

    private static LoadMeasurement BenchmarkLoad(
        string compression,
        string categoriesPath,
        string itemsPath)
    {
        _ = DataTableLoader.LoadItemCategory(categoriesPath);
        _ = DataTableLoader.LoadItem(itemsPath);
        ForceFullCollection();

        long memoryBefore = GC.GetTotalMemory(forceFullCollection: false);
        LoadMeasurement measurement = MeasureLoad(categoriesPath, itemsPath);
        ForceFullCollection();
        long memoryAfterRelease = GC.GetTotalMemory(forceFullCollection: false);

        long binaryBytes = new FileInfo(categoriesPath).Length + new FileInfo(itemsPath).Length;
        GD.Print(
            $"[DataTablePrototypeBenchmark] Load: Build={BuildConfiguration}; " +
            $"Compression={compression}; " +
            $"Rows={measurement.RowCount}; " +
            $"BinaryBytes={binaryBytes}; ElapsedMs={measurement.Elapsed.TotalMilliseconds:F3}; " +
            $"AllocatedBytes={measurement.AllocatedBytes}; " +
            $"RetainedManagedBytes={measurement.MemoryWhileLoaded - memoryBefore}; " +
            $"PostReleaseManagedBytes={memoryAfterRelease - memoryBefore}");
        return measurement;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static LoadMeasurement MeasureLoad(
        string categoriesPath,
        string itemsPath)
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        ItemCategoryTable categories = DataTableLoader.LoadItemCategory(categoriesPath);
        ItemTable items = DataTableLoader.LoadItem(itemsPath);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        int rowCount = categories.Count + items.Count;
        long memoryWhileLoaded = GC.GetTotalMemory(forceFullCollection: true);
        GC.KeepAlive(categories);
        GC.KeepAlive(items);
        return new LoadMeasurement(
            rowCount,
            elapsed,
            allocated,
            memoryWhileLoaded);
    }

    private static void BenchmarkLookup(
        string compression,
        string itemsPath,
        int expectedItemCount)
    {
        ItemTable items = DataTableLoader.LoadItem(itemsPath);
        var ids = new string[expectedItemCount];
        for (int index = 0; index < ids.Length; index++)
            ids[index] = $"item_{index + 1:00000}";

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        double weightSum = 0;
        for (int index = 0; index < LookupCount; index++)
            weightSum += items.Get(ids[index % expectedItemCount]).Weight;
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert(weightSum > 0, "查询结果未被消费");
        Assert(allocated == 0, $"预生成键查询产生托管分配：{allocated} bytes");
        GD.Print(
            $"[DataTablePrototypeBenchmark] Lookup: Compression={compression}; Count={LookupCount}; " +
            $"ElapsedMs={elapsed.TotalMilliseconds:F3}; AllocatedBytes={allocated}");
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void AssertZstdAllocation(
        LoadMeasurement uncompressed,
        LoadMeasurement zstd,
        string categoriesPath,
        string itemsPath)
    {
        long compressedBytes =
            new FileInfo(categoriesPath).Length + new FileInfo(itemsPath).Length;
        long allocationOverhead = zstd.AllocatedBytes - uncompressed.AllocatedBytes;
        const long toleranceBytes = 64 * 1024;
        Assert(
            allocationOverhead <= compressedBytes + toleranceBytes,
            $"Zstd 加载复制了压缩 payload：额外分配 {allocationOverhead} bytes，" +
            $"压缩文件 {compressedBytes} bytes。");
        GD.Print("[DataTablePrototypeBenchmark] PASS: Zstd 压缩 payload 单份分配");
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

    private readonly record struct LoadMeasurement(
        int RowCount,
        TimeSpan Elapsed,
        long AllocatedBytes,
        long MemoryWhileLoaded);
}
