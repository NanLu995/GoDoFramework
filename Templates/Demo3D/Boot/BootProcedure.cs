using Game.DataTables.Base;
using Godot;
using GoDo;
using System;
using System.Threading.Tasks;

#nullable enable

namespace Demo3D;

public sealed class BootProcedure : IProcedure
{
    public string Name => "Boot";

    public async Task EnterAsync(ProcedureContext context)
    {
        LoadSettings();
        LoadInputBindings();
        await BaseDataTables.LoadAsync(ReportDataTableProgress);
        VerifyDataTableAccess();
        LogDataTableSamples();
        context.RequestChange<GameplayProcedure>();
    }

    public Task ExitAsync(ProcedureContext context) => Task.CompletedTask;

    private static void ReportDataTableProgress(DataTableLoadProgress progress)
    {
        GD.Print(
            $"[Demo3D] DataTable {progress.DataSetId}: " +
            $"{progress.LoadedTableCount}/{progress.TotalTableCount} ({progress.Ratio:P0})");
    }

    private static void VerifyDataTableAccess()
    {
        if (!BaseDataTables.Items.TryGet("iron_sword", out ItemRow item))
            throw new InvalidOperationException("Demo3D 示例数据缺少 iron_sword。");

        GD.Print($"[Demo3D] DataTable 示例：{item.DisplayName}; Price={item.Price}");
    }

    private static void LoadInputBindings()
    {
        IInputService input = Services.Get<IInputService>();
        if (!input.TryGetRebindingPersistence(out IInputRebindingPersistence? persistence) || persistence == null)
            throw new InvalidOperationException("Demo3D 需要支持持久化的输入后端。");

        try
        {
            InputBindingLoadStatus status = persistence.LoadAndApply();
            if (status == InputBindingLoadStatus.RecoveredFromBackup)
            {
                ErrorHub.Warn(
                    "输入绑定正式配置不可用，已从备份恢复",
                    nameof(Boot));
            }
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(Boot));
        }
    }

    private static void LogDataTableSamples()
    {
        if (!BaseDataTables.ItemCategories.TryGet("weapon", out ItemCategoryRow category))
            throw new InvalidOperationException("Demo3D sample data is missing category 'weapon'.");
        if (!BaseDataTables.Items.TryGet("healing_potion", out ItemRow item))
            throw new InvalidOperationException("Demo3D sample data is missing item 'healing_potion'.");
        if (!BaseDataTables.Rewards.TryGet("quest_currency", out RewardRow reward))
            throw new InvalidOperationException("Demo3D sample data is missing reward 'quest_currency'.");

        GD.Print(
            $"[Demo3D] DataTable counts: " +
            $"categories={BaseDataTables.ItemCategories.Count}, " +
            $"items={BaseDataTables.Items.Count}, " +
            $"rewards={BaseDataTables.Rewards.Count}");
        GD.Print(
            $"[Demo3D] Category: id={category.Id}, name={category.DisplayName}, " +
            $"sort={category.SortOrder}, enabled={category.Enabled}");
        GD.Print(
            $"[Demo3D] Item: id={item.Id}, name={item.DisplayName}, " +
            $"rarity={item.Rarity}, price={item.Price}, weight={item.Weight}, " +
            $"sellable={item.IsSellable}");
        GD.Print(
            $"[Demo3D] Reward: id={reward.Id}, name={reward.DisplayName}, " +
            $"type={reward.RewardType}, amount={reward.Amount}, " +
            $"itemId={reward.ItemId ?? "<none>"}, enabled={reward.Enabled}");
    }

    private static void LoadSettings()
    {
        try
        {
            SettingsLoadStatus status = Services.Get<ISettingsService>().LoadAndApply();
            if (status == SettingsLoadStatus.RecoveredFromBackup)
                ErrorHub.Warn("设置正式配置不可用，已从备份恢复", nameof(Boot));
        }
        catch (Exception exception)
        {
            ErrorHub.Report(exception, nameof(Boot), "加载 Demo3D 设置失败，继续使用当前运行时设置");
        }
    }
}
