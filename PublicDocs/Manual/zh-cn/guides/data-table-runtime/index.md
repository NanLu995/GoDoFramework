# DataTable：在运行时加载游戏数据

`DataTableService` 读取由数据表工具生成的强类型表。它适合物品、奖励、数值等需要由内容数据驱动的业务；表 Schema 与生成物应在编辑阶段维护，运行时只按服务提供的加载与查询边界使用。

## 使用建议

1. 在业务启动边界显式加载所需表集，而不是第一次查询时隐式加载。
2. 等待加载成功后再读取数据；UI 可使用该次请求的进度反馈加载状态。
3. 按客户端/服务端子集加载，避免把不需要的数据打进错误的构建目标。

## 关键规则

- 加载失败、取消和卸载都有明确状态；不要把未加载误当成空表。
- 重复加载和并发请求遵循服务的事务性提交边界。
- 运行时服务不负责从 CSV 生成代码或资源。

从 CSV 到运行时加载的端到端流程见[DataTable 工作流](../data-tables/index.md)。

## 能力全景图

<div class="godo-capability-list">
<section><h4>事务性加载表集</h4><p>可选择 Client/Server 子集、观察表级进度并取消等待。</p><pre class="godo-capability-call"><code>await tables.LoadAsync(definition, subset, OnProgress, cancellationToken);</code></pre></section>
<section><h4>检查表集提交状态</h4><p>只有全部解析并发布成功后才返回 true。</p><pre class="godo-capability-call"><code>bool loaded = tables.IsLoaded(setId);</code></pre></section>
<section><h4>获取强类型生成表</h4><p>表集未加载、表名不存在或类型不匹配都会明确失败。</p><pre class="godo-capability-call"><code>ItemTable items = tables.GetTable&lt;ItemTable&gt;(setId, "Item");</code></pre></section>
<section><h4>卸载表集</h4><p>释放已发布快照；业务不能继续持有并假设服务仍管理旧表。</p><pre class="godo-capability-call"><code>bool unloaded = tables.Unload(setId);</code></pre></section>
</div>

精确加载结果、取消和异常见 [IDataTableService API](xref:GoDo.IDataTableService)。
