using System;

#nullable enable

#if DEBUG
namespace GoDo;

internal readonly struct DataTableDebugSnapshot
{
    public DataTableDebugDataSetEntry[] DataSets { get; }
    public DataTableDebugHistoryEntry[] History { get; }
    public int LoadedDataSetCount { get; }
    public int LoadingDataSetCount { get; }
    public int CachedTableCount { get; }
    public int FailedLoadCount { get; }

    public DataTableDebugSnapshot(
        DataTableDebugDataSetEntry[] dataSets,
        DataTableDebugHistoryEntry[] history,
        int loadedDataSetCount,
        int loadingDataSetCount,
        int cachedTableCount,
        int failedLoadCount)
    {
        DataSets = dataSets;
        History = history;
        LoadedDataSetCount = loadedDataSetCount;
        LoadingDataSetCount = loadingDataSetCount;
        CachedTableCount = cachedTableCount;
        FailedLoadCount = failedLoadCount;
    }
}

internal readonly struct DataTableDebugDataSetEntry
{
    public string DataSetId { get; }
    public string RuntimeDirectory { get; }
    public DataTableDebugState State { get; }
    public int LoadedTableCount { get; }
    public int TotalTableCount { get; }
    public string? LastTableId { get; }
    public DataTableDebugTableEntry[] Tables { get; }

    public DataTableDebugDataSetEntry(
        string dataSetId,
        string runtimeDirectory,
        DataTableDebugState state,
        int loadedTableCount,
        int totalTableCount,
        string? lastTableId,
        DataTableDebugTableEntry[] tables)
    {
        DataSetId = dataSetId;
        RuntimeDirectory = runtimeDirectory;
        State = state;
        LoadedTableCount = loadedTableCount;
        TotalTableCount = totalTableCount;
        LastTableId = lastTableId;
        Tables = tables;
    }
}

internal readonly struct DataTableDebugTableEntry
{
    public string TableId { get; }
    public Type TableType { get; }

    public DataTableDebugTableEntry(string tableId, Type tableType)
    {
        TableId = tableId;
        TableType = tableType;
    }
}

internal readonly struct DataTableDebugHistoryEntry
{
    public string DataSetId { get; }
    public DataTableDebugState State { get; }
    public int TableCount { get; }
    public string Detail { get; }

    public DataTableDebugHistoryEntry(
        string dataSetId,
        DataTableDebugState state,
        int tableCount,
        string detail)
    {
        DataSetId = dataSetId;
        State = state;
        TableCount = tableCount;
        Detail = detail;
    }
}

internal enum DataTableDebugState
{
    Loading,
    Loaded,
    Failed,
    Canceled,
    Unloaded,
}
#endif
