using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Game.DataTables.Base;
using Godot;
using GoDo;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>DataTableService 显式加载、进度、事务发布与卸载回归入口。</summary>
public sealed partial class DataTableServiceRegression : Node
{
    /// <inheritdoc />
    public override async void _Ready()
    {
        try
        {
            IDataTableService service = Services.Get<IDataTableService>();
            BaseDataTables.Unload();

            var progress = new List<DataTableLoadProgress>();
            Task loading = BaseDataTables.LoadAsync(progress.Add);
            Assert(!loading.IsCompleted, "多表数据集没有在表之间让出帧。 ");
#if DEBUG
            Thread.Sleep(5);
            DataTableDebugSnapshot loadingSnapshot =
                ((DataTableService)service).GetDebugSnapshot();
            DataTableDebugDataSetEntry loadingEntry = Array.Find(
                loadingSnapshot.DataSets,
                static entry => entry.DataSetId == "game.base");
            Assert(loadingEntry.State == DataTableDebugState.Loading &&
                loadingEntry.AgeMilliseconds >= 1,
                "活动数据集加载年龄没有持续增长。");
#endif
            AssertThrows<InvalidOperationException>(
                () => { _ = BaseDataTables.Unload(); },
                "正在加载的数据集可以被卸载。");
            await loading;
#if DEBUG
            DataTableDebugDataSetEntry loadedEntry = Array.Find(
                ((DataTableService)service).GetDebugSnapshot().DataSets,
                static entry => entry.DataSetId == "game.base");
            Assert(loadedEntry.State == DataTableDebugState.Loaded &&
                loadedEntry.AgeMilliseconds == 0,
                "数据集加载完成后仍保留活动加载年龄。");
#endif

            Assert(BaseDataTables.IsLoaded, "Base 数据集加载后未发布。");
            Assert(progress.Count == 4, "表级进度回调次数不正确。");
            Assert(progress[0].LoadedTableCount == 0 && progress[0].Ratio == 0d, "初始进度不正确。");
            Assert(progress[^1].LoadedTableCount == 3 && progress[^1].Ratio == 1d, "完成进度不正确。");
            Assert(BaseDataTables.ItemCategories.Count == 3, "ItemCategory 表行数不正确。");
            Assert(BaseDataTables.Items.Count == 4, "Item 表行数不正确。");
            Assert(BaseDataTables.Rewards.Count == 4, "Reward 表行数不正确。");
            Assert(BaseDataTables.Items.TryGet("iron_sword", out ItemRow item), "主键查询失败。");
            Assert(item.Price == 120, "强类型字段读取错误。");

            ItemTable firstItems = BaseDataTables.Items;
            await BaseDataTables.LoadAsync(
                _ => throw new InvalidOperationException("已加载数据集不应重复报告进度。"));
            Assert(ReferenceEquals(firstItems, BaseDataTables.Items), "重复加载没有复用已发布表。");

            Assert(BaseDataTables.Unload(), "已加载数据集卸载返回 false。");
            Assert(!BaseDataTables.IsLoaded, "数据集卸载后仍处于已加载状态。");
            AssertThrows<InvalidOperationException>(
                () => _ = BaseDataTables.Items,
                "卸载后仍可获取数据表。");

            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                await AssertCanceledAsync(() => BaseDataTables.LoadAsync(cancellationToken: canceled.Token));
            }
            Assert(!BaseDataTables.IsLoaded, "取消后发布了半加载数据集。");

            await AssertLoadFailureAsync(
                () => BaseDataTables.LoadFromAsync(
                    "res://Verification/Automated/Fixtures/DataTableService/InvalidManifest"),
                "data_set_id");
            Assert(!service.IsLoaded("game.base"), "Manifest 失败后发布了数据集。");

            await AssertLoadFailureAsync(
                () => BaseDataTables.LoadFromAsync(
                    "res://Verification/Automated/Fixtures/DataTableService/DangerousManifest"),
                "产物路径无效");
            Assert(!service.IsLoaded("game.base"), "危险产物路径失败后发布了数据集。");

            await AssertManifestFailureAsync(
                service,
                "missing-field",
                "res://Verification/Automated/Fixtures/DataTableService/MissingField",
                "data_set_id");
            await AssertManifestFailureAsync(
                service,
                "invalid-json",
                "res://Verification/Automated/Fixtures/DataTableService/InvalidJson",
                "JSON 无效");
            await AssertManifestFailureAsync(
                service,
                "version-mismatch",
                "res://Verification/Automated/Fixtures/DataTableService/VersionMismatch",
                "format_version");
            await AssertOversizedManifestFailureAsync(service);

            GD.Print("[DataTableServiceRegression] PASS (14/14)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[DataTableServiceRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static async Task AssertCanceledAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        throw new InvalidOperationException("已取消的加载没有抛出 OperationCanceledException。");
    }

    private static async Task AssertLoadFailureAsync(Func<Task> action, string message)
    {
        try
        {
            await action();
        }
        catch (DataTableLoadException exception) when (exception.Message.Contains(message, StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException($"加载失败未包含预期诊断：{message}");
    }

    private static async Task AssertManifestFailureAsync(
        IDataTableService service,
        string dataSetId,
        string runtimeDirectory,
        string message)
    {
        var definition = new DataTableSetDefinition(
            dataSetId,
            formatVersion: 2,
            protocolVersion: 1,
            Array.Empty<DataTableDefinition>());
        await AssertLoadFailureAsync(
            () => service.LoadAsync(definition, runtimeDirectory),
            message);
        Assert(!service.IsLoaded(dataSetId), $"Manifest 失败后发布了数据集：{dataSetId}");
    }

    private static async Task AssertOversizedManifestFailureAsync(IDataTableService service)
    {
        const string DataSetId = "oversized-manifest";
        string directoryName = $"godo-datatable-{Guid.NewGuid():N}";
        string godotDirectory = $"user://{directoryName}";
        string systemDirectory = ProjectSettings.GlobalizePath(godotDirectory);
        string manifestPath = Path.Combine(systemDirectory, "manifest.json");
        Directory.CreateDirectory(systemDirectory);
        try
        {
            using (var stream = new FileStream(
                manifestPath,
                FileMode.CreateNew,
                System.IO.FileAccess.Write))
                stream.SetLength(16L * 1024 * 1024 + 1L);
            await AssertManifestFailureAsync(
                service,
                DataSetId,
                godotDirectory,
                "超过 16 MiB");
        }
        finally
        {
            if (File.Exists(manifestPath))
                File.Delete(manifestPath);
            if (Directory.Exists(systemDirectory))
                Directory.Delete(systemDirectory);
        }
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
