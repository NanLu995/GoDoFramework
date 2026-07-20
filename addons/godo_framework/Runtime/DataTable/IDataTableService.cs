using System;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace GoDo;

/// <summary>面向业务层的 DataTable 数据集加载、缓存、查询与卸载服务。</summary>
public interface IDataTableService
{
    /// <summary>
    /// 事务加载一个数据集；框架不会主动调用此方法。
    /// </summary>
    /// <param name="definition">生成代码提供的数据集描述。</param>
    /// <param name="runtimeDirectory">包含 <c>manifest.json</c> 与 <c>*.gdtb</c> 的 Godot 路径。</param>
    /// <param name="progress">可选的同步表级进度回调，在 Godot 主线程调用。</param>
    /// <param name="cancellationToken">在表与表之间取消加载的令牌。</param>
    /// <returns>全部表验证并发布后完成的任务。</returns>
    /// <exception cref="DataTableLoadException">Manifest、版本、表描述或二进制加载失败。</exception>
    /// <exception cref="OperationCanceledException">加载在发布前被取消。</exception>
    Task LoadAsync(
        DataTableSetDefinition definition,
        string runtimeDirectory,
        Action<DataTableLoadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>判断指定数据集是否已经完整加载并发布。</summary>
    bool IsLoaded(string dataSetId);

    /// <summary>
    /// 获取已加载的强类型只读表。
    /// </summary>
    /// <typeparam name="TTable">生成代码中的表类型。</typeparam>
    /// <param name="dataSetId">数据集 ID。</param>
    /// <param name="tableId">表 ID。</param>
    /// <returns>已缓存的表实例。</returns>
    /// <exception cref="InvalidOperationException">数据集或表尚未加载，或者请求类型不匹配。</exception>
    TTable GetTable<TTable>(string dataSetId, string tableId) where TTable : class;

    /// <summary>
    /// 卸载一个已发布数据集并释放 Service 持有的表引用。
    /// </summary>
    /// <param name="dataSetId">数据集 ID。</param>
    /// <returns>确实卸载了数据集时为 <see langword="true"/>；数据集未加载时为 <see langword="false"/>。</returns>
    /// <exception cref="InvalidOperationException">该数据集正在加载。</exception>
    bool Unload(string dataSetId);
}
