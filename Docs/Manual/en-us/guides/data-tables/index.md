---
translation_of: Docs/Manual/zh-cn/guides/data-tables/index.md
translation_source_hash: sha256:d4cb612308e811dc3da02fc8aed1ec4b17e6e0a75fe8a6118d262f7247a94562
---

# Generate Validated Data Tables from CSV

DataTable is a development-time compiler. It reads UTF-8 CSV and a DataTable Schema, validates types, primary keys, ranges, and cross-table foreign keys, then generates binary `.gdtb` files, a Manifest, and strongly typed C# readers. The game reads generated artifacts at runtime instead of parsing source CSV or the Schema.

> [!IMPORTANT]
> DataTable remains experimental and is not a stable baseline. Generated formats, code names, and workflows may continue to change. It is suitable for evaluation, but should not own an irreplaceable production-data pipeline until the project pins a version and establishes regression checks.

## When to use DataTable

Use it when:

- Large sets of uniform rows are easier to edit in spreadsheet software.
- Primary-key, numeric-range, enum, and cross-table reference validation is required.
- Client and Server need different publication subsets from one source dataset.
- CI should detect generated files that no longer match CSV.

It is not intended for:

- Small, deeply structured configuration with Godot Resource references; prefer ConfigHub.
- Runtime editing, remote configuration, hot updates, or online spreadsheet synchronization.
- Encryption, tamper prevention, or network protocols; Manifest hashes only detect mismatches.

## 1. Create the directory layout

Recommended structure:

```text
DataTables/
└─ Base/
   ├─ .datatable.schema.json
   ├─ .datafiles/
   │  ├─ .gdignore
   │  ├─ ItemCategories.csv
   │  └─ Items.csv
   ├─ Runtime/
   │  ├─ ItemCategory.gdtb
   │  ├─ Item.gdtb
   │  └─ manifest.json
   └─ BaseDataTables.g.cs
```

Use `数据表配置 (DataTable Configuration)...` in the **Data Tables** section of the `GoDo Framework` menu to maintain the Schema and `.datafiles`. `Runtime` and `BaseDataTables.g.cs` are generated and must not be edited. Committing generated artifacts is recommended so a fresh checkout compiles immediately and CI can verify that they are current. The Schema and `.datafiles` are excluded from the final game package.

Source CSV uses UTF-8 and may contain a BOM. Column names must match Schema field names.

## 2. Write CSV source

`ItemCategories.csv`:

```csv
id,display_name,sort_order,enabled
weapon,Weapon,10,true
consumable,Consumable,20,true
```

`Items.csv`:

```csv
id,category_id,display_name,enabled,max_stack,weight,rarity,description
iron_sword,weapon,Iron Sword,true,1,3.5,Common,A basic sword
health_potion,consumable,Health Potion,true,20,0.2,Uncommon,Restores health
```

Stable IDs are case-sensitive. Do not use localized display copy as an ID. A production project commonly stores translation keys in the table and uses LocalizationService for player text.

## 3. Declare structure in the Schema editor

Open `数据表配置 (DataTable Configuration)...` in the **Data Tables** section of the `GoDo Framework` menu, select `.datatable.schema.json`, then choose **Edit Schema...**. The data-file panel shows the file, state, and data table ID, using green for included files, yellow for files not yet included, and red for missing files. Select a whole row to include an unconfigured CSV or remove an included CSV from the Schema without deleting the file. **新建数据表...** creates a new CSV when the Schema is saved. Godot translation is disabled for data table IDs, field names, and CSV paths. Data table IDs and CSV paths change only through explicit actions, while the read-only current table-structure version is maintained by the tool. A row background marks the active field; double-click edits text cells, while types and checkboxes use a single click. A blank default means no fallback is configured—it does not silently become `0`, `false`, or an empty string. The JSON below explains the saved result and is not intended for manual editing:

```json
{
  "format_version": 2,
  "data_set_id": "game.base",
  "protocol_version": 1,
  "namespace": "MyGame.DataTables.Base",
  "source_directory": ".datafiles",
  "output_directory": "Runtime",
  "csharp_output": "BaseDataTables.g.cs",
  "tables": [
    {
      "id": "ItemCategory",
      "source": "ItemCategories.csv",
      "schema_version": 1,
      "audience": "Shared",
      "primary_key": "id",
      "fields": [
        { "name": "id", "type": "string", "required": true, "min_length": 1, "max_length": 64 },
        { "name": "display_name", "type": "string", "required": true, "min_length": 1, "max_length": 128 },
        { "name": "sort_order", "type": "int32", "required": true, "min": 0, "max": 10000 },
        { "name": "enabled", "type": "bool", "required": false, "default": true }
      ]
    },
    {
      "id": "Item",
      "source": "Items.csv",
      "schema_version": 1,
      "audience": "Shared",
      "primary_key": "id",
      "fields": [
        { "name": "id", "type": "string", "required": true, "min_length": 1, "max_length": 64 },
        { "name": "category_id", "type": "string", "required": true, "foreign_key": "ItemCategory.id" },
        { "name": "display_name", "type": "string", "required": true, "min_length": 1, "max_length": 128 },
        { "name": "enabled", "type": "bool", "required": false, "default": true },
        { "name": "max_stack", "type": "int32", "required": false, "default": 1, "min": 1, "max": 999 },
        { "name": "weight", "type": "float64", "required": true, "min": 0, "max": 1000 },
        { "name": "rarity", "type": "enum", "required": true, "values": ["Common", "Uncommon", "Rare", "Epic"] },
        { "name": "description", "type": "string", "required": false, "allow_empty": true, "null_token": "<null>", "max_length": 256 }
      ]
    }
  ]
}
```

Supported types are currently `string`, `bool`, `int32`, `float64`, and controlled `enum`. The editor's **Data export scope** maps to `audience`:

- `Shared`: included for Client and Server.
- `ClientOnly`: included only in the client target.
- `ServerOnly`: included only in the dedicated-server target.

The Schema editor increments a table's `schema_version` only after a real structural change; changing only the CSV path does not increment it. Renaming a data table ID or field updates foreign keys that target it, while a referenced table or field cannot be removed directly. The project increments `protocol_version` after an incompatible cross-endpoint Data Schema change. These versions do not migrate old binaries or network connections automatically.

Save validates the complete Schema and every CSV update in memory, then commits the Schema, CSV headers, and `.gdignore` as one transaction. If any file cannot be written, replaced files are rolled back instead of leaving a partially updated Schema and CSV set.

The Schema also stores the source directory, runtime directory, and C# output path. Every path is relative to the Schema, uses forward slashes, and cannot be absolute or contain `..`. The C# file must remain outside the runtime directory because data generation replaces that directory as a unit.

## 4. Check before generating

Validate without writing files:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py check `
  --schema DataTables/Base/.datatable.schema.json
```

Generate everything after a successful check:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate `
  --schema DataTables/Base/.datatable.schema.json
```

Success returns exit code 0; data diagnostics, Schema, path, and I/O errors return 1. Files are committed only after all validation succeeds. A failure does not overwrite the last successful output. Unchanged C# is not rewritten, avoiding unnecessary Godot/.NET rebuilds.

Generated artifacts include:

- One `<TableId>.gdtb` per table.
- `manifest.json`, plus only the necessary Client/Server Manifest when endpoint-specific tables exist.
- Aggregated strongly typed C# row, table, and Loader code.

Do not edit these files manually; the next generation replaces them.

## 5. Run it from the Godot editor

Enable the single **GoDo Framework** plugin, then open:

```text
GoDo Framework → Data Tables → 数据表配置 (DataTable Configuration)...
```

The window looks for `res://DataTables/Base/.datatable.schema.json` by default. It can edit the Schema, inspect or include data files, run **校验全部数据** for read-only validation, or use **导出当前表...** and **导出全部表...** in the data-export row. Export previews its targets and asks for confirmation, then tells Godot to rescan files when complete.

The Python path is stored only in local EditorSettings, not project configuration. Teams and CI share the version-controlled Schema.

## 6. Read generated tables in the game

Generated code uses the namespace declared in the Schema. Framework startup only registers `DataTableService`; it never loads business data automatically. The business loading flow explicitly loads a dataset and receives table-level progress:

```csharp
using MyGame.DataTables.Base;

await BaseDataTables.LoadAsync(
    progress => loadingView.SetProgress(progress.Ratio));

ItemRow sword = BaseDataTables.Items.Get("iron_sword");
if (BaseDataTables.Items.TryGet("health_potion", out ItemRow potion))
    GD.Print(potion.MaxStack);
```

The final segment of `data_set_id` determines the generated facade and default directory: `game.base` maps to `BaseDataTables` and `res://DataTables/Base/Runtime`. Tables become visible only after the whole dataset succeeds. Failure or cancellation leaves no partially loaded dataset. Repeated loads reuse existing tables, and `BaseDataTables.Unload()` releases references held by the Service. Use `LoadFromAsync(runtimeDirectory)` after business code mounts compatible data elsewhere.

Generated types remain assembly-internal for direct use in the same Godot C# project. Business code chooses when to load Base or DLC datasets and how to retry or degrade. The framework does not download, mount PCKs, perform hot updates, or select business versions.

Runtime reading validates file magic, format version, schema, table ID, field count, size, and SHA-256 payload digest. Corruption or incompatibility throws explicitly. A single file is limited to 2 GiB.

## 7. Verify generated output is current

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py verify-generated `
  --schema DataTables/Base/.datatable.schema.json
```

This builds expected output in memory and compares the existing generated directory read-only. Missing, extra, or stale content returns 1. It does not write temporary files, delete extras, or change timestamps. Use it before commits and in CI.

Generate one table:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate `
  --schema DataTables/Base/.datatable.schema.json `
  --table Item
```

Initial generation, table addition/removal, a stale unselected table, or structural changes require a full generation. Single-table mode still validates every CSV, foreign key, and digest; it is not a bypass around dataset correctness.

## 8. Client/Server isolation and release export

The client Manifest contains only `Shared + ClientOnly`; the server Manifest contains only `Shared + ServerOnly`. Compare shared compatibility with:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py compare-manifests `
  --client DataTables/Base/Runtime/manifest.client.json `
  --server DataTables/Base/Runtime/manifest.server.json
```

Do not rely on clicking Godot Export for a formal release. The supported Godot 4.x EditorExportPlugin cannot reliably abort a bad export; revalidate this limitation after an engine upgrade. Use the wrapper to run the read-only gate before launching Godot:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable_export.py `
    --godot $env:GODOT_PATH `
  --project . `
  --preset "Windows Desktop" `
  --output Builds/Windows/Game.exe `
  --mode release
```

A normal preset selects Client; a preset with the `dedicated_server` feature tag selects Server. Release and Debug both map only target `.gdtb` files and `manifest.json`; `.datatable.schema.json`, `.datafiles`, and diagnostics do not enter the package.

Manifest hashes detect mismatched Client/Server data. They are not digital signatures and do not prove that a file is trusted. Authentication, tamper prevention, and connection rejection belong to the game network layer.

## Common failures

- Chinese CSV text is corrupted: the file is not UTF-8, or spreadsheet export encoding differs.
- A foreign key is missing: the value does not exist in the target primary-key column; fix the source data.
- Generation failed but old files remain: transactional protection preserved them, but they are stale.
- Single-table generation is rejected: the full baseline is missing, another table changed, or the table set changed; generate all.
- The editor exports but the package lacks data: direct Godot export is not a reliable gate; use the wrapper.
- A Client package contains sensitive server data: inspect audience, the preset's `dedicated_server` tag, and the target Manifest.
- The game uses old data after CSV changes: run generate and confirm with verify-generated.
- A Manifest hash is treated as a security signature: it only compares consistency and cannot prevent malicious replacement.

DataTableService is currently a first-version public runtime API under validation. Regenerate and rerun the full verification workflow after framework upgrades. Mobile, AOT, and complete ExportRelease validation remain pending.
