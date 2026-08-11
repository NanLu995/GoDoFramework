---
translation_of: Docs/Manual/zh-cn/guides/data-table-tool/index.md
translation_source_hash: sha256:d271c358cd175e67b4fe73eb79d4ec5176cd6a369d418a769b2cac0e14c18f04
---

# DataTable tool: generate usable tables from CSV

The DataTable editor tool maintains schemas, checks CSV, and generates runtime table resources. It is content-production work, not runtime work: define columns, types, keys, and client/server ownership; run read-only validation before generation; then version CSV, schema, and output together.

Do not hand-edit generated output or allow failed generation to leave data that appears valid. Confirm generated output is current before export, then load it through `DataTableService`.

See the [DataTable workflow](../data-tables/index.md) for operations and validation.
