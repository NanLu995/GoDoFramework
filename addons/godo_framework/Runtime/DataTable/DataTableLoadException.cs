using System;

#nullable enable

namespace GoDo;

/// <summary>表示 DataTable Manifest 校验或数据表加载失败。</summary>
public sealed class DataTableLoadException : Exception
{
    /// <summary>
    /// 创建 DataTable 加载异常。
    /// </summary>
    /// <param name="dataSetId">发生失败的数据集 ID。</param>
    /// <param name="message">可供调用方诊断的失败原因。</param>
    /// <param name="innerException">导致本次失败的底层异常；没有底层异常时为 <see langword="null"/>。</param>
    public DataTableLoadException(string dataSetId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        DataSetId = dataSetId;
    }

    /// <summary>发生失败的数据集 ID。</summary>
    public string DataSetId { get; }
}
