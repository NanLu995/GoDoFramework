---
translation_of: Docs/Manual/zh-cn/guides/data-table-tool/index.md
translation_source_hash: sha256:27e49b4f081cdb9a1399f7140f4e70b0e7e25a2aaa5be8b6bc6f8892163f81d9
---

# DataTable tool: generate usable tables from CSV

The DataTable editor tool maintains schemas, checks CSV, and generates runtime table resources. It is content-production work, not runtime work: define columns, types, keys, and client/server ownership; run read-only validation before generation; then version CSV, schema, and output together.

Do not hand-edit generated output or allow failed generation to leave data that appears valid. Confirm generated output is current before export, then load it through `DataTableService`.

See the [DataTable workflow](../data-tables/index.md) for operations and validation.

## Capability map

<div class="godo-capability-list">
<section><h4>Maintain one authoritative schema</h4><p>Declare tables, columns, types, keys, references, and client/server ownership.</p></section>
<section><h4>Validate source data without writing</h4><p>Check missing CSV, column types, duplicate keys, and references before generation.</p></section>
<section><h4>Generate transactionally</h4><p>Publish output and completion markers only after every table passes.</p></section>
<section><h4>Verify freshness and export boundaries</h4><p>Check schema/CSV hashes, client/server isolation, and generator version before release.</p></section>
</div>

The editor tool has no runtime call surface; generated data is consumed through the [IDataTableService API](xref:GoDo.IDataTableService).
