#nullable enable

namespace GoDo;

/// <summary>表示一个数据集按表加载的当前进度。</summary>
/// <param name="DataSetId">正在加载的数据集 ID。</param>
/// <param name="LoadedTableCount">已经完成解码的表数量。</param>
/// <param name="TotalTableCount">本次 Manifest 中需要加载的表总数。</param>
/// <param name="TableId">最近完成的表 ID；尚未完成任何表时为 <see langword="null"/>。</param>
public readonly record struct DataTableLoadProgress(
    string DataSetId,
    int LoadedTableCount,
    int TotalTableCount,
    string? TableId)
{
    /// <summary>0 到 1 之间的表级完成比例；空数据集为 1。</summary>
    public double Ratio => TotalTableCount == 0 ? 1d : (double)LoadedTableCount / TotalTableCount;
}
