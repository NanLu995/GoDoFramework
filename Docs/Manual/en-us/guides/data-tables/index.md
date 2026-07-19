---
translation_of: Docs/Manual/zh-cn/guides/data-tables/index.md
translation_source_hash: sha256:428d1cab192e758eff79d3469b34687c4774933510e06a542cac7f7865e06ba5
---

# Generate Validated Data Tables from CSV

DataTable is a development-time compiler. It reads UTF-8 CSV and a JSON Profile, validates types, primary keys, ranges, and cross-table foreign keys, then generates binary `.gdtb` files and strongly typed C# readers. The game reads generated artifacts at runtime instead of parsing source CSV.

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
├─ datatable.build.json
├─ profile.json
├─ Sources/
│  ├─ ItemCategories.csv
│  └─ Items.csv
└─ Generated/
   ├─ Data/
   └─ DataTables.Generated.cs
```

Manually maintain only the Build Config, Profile, and Sources. `Generated` is tool output and must not be edited. The team may choose whether to commit generated artifacts, but CI must be able to verify that they are current.

Source CSV uses UTF-8 and may contain a BOM. Column names must match Profile field names.

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

## 3. Declare the schema in a Profile

```json
{
  "format_version": 2,
  "data_set_id": "game.items",
  "protocol_version": 1,
  "compression_mode": "Auto",
  "namespace": "MyGame.Generated",
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

Supported types are currently `string`, `bool`, `int32`, `float64`, and controlled `enum`. `audience` can be:

- `Shared`: included for Client and Server.
- `ClientOnly`: included only in the client target.
- `ServerOnly`: included only in the dedicated-server target.

Increment a table's `schema_version` after an incompatible structural change. The project increments `protocol_version` after changing a cross-endpoint data contract. These versions do not migrate old binaries or network connections automatically.

## 4. Create the Build Config

`DataTables/datatable.build.json`:

```json
{
  "format_version": 1,
  "profile": "profile.json",
  "source": "Sources",
  "output": "Generated/Data",
  "csharp": "Generated/DataTables.Generated.cs"
}
```

Every path is relative to this JSON, uses forward slashes, and cannot be absolute or contain `..`. The C# file must remain outside the data output directory because data generation replaces that directory as a unit.

## 5. Check before generating

Validate without writing files:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py check `
  --build-config DataTables/datatable.build.json
```

Generate everything after a successful check:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate `
  --build-config DataTables/datatable.build.json
```

Success returns exit code 0; data diagnostics, Profile, path, and I/O errors return 1. Files are committed only after all validation succeeds. A failure does not overwrite the last successful output. Unchanged C# is not rewritten, avoiding unnecessary Godot/.NET rebuilds.

Generated artifacts include:

- One `<TableId>.gdtb` per table.
- Full and Client/Server Manifest and Debug JSON files.
- Normalized IR and a build report.
- Aggregated strongly typed C# row, table, and Loader code.

Do not edit these files manually; the next generation replaces them.

## 6. Run it from the Godot editor

Enable the single **GoDo Framework** plugin, then open:

```text
GoDo → DataTable...
```

The window looks for `res://DataTables/datatable.build.json` by default. It provides **Check All**, **Generate All...**, and **Generate Selected Table...**. Generation previews its targets and asks for confirmation, then tells Godot to rescan files when complete.

The Python path is stored only in local EditorSettings, not project configuration. Teams and CI still share the Build Config for consistent input and output paths.

## 7. Read generated tables in the game

Generated code uses the namespace declared in the Profile. The experimental generator currently produces a typed Loader and per-table lookup classes, for example:

```csharp
using MyGame.Generated;

ItemTable items = DataTablePrototypeLoader.LoadItem(
    "res://DataTables/Generated/Data/Item.gdtb");

ItemRow sword = items.Get("iron_sword");
if (items.TryGet("health_potion", out ItemRow potion))
    GD.Print(potion.MaxStack);
```

Generated types are currently assembly-internal and intended for direct use in the same Godot C# project. Generator and Loader naming is not stable. After a framework upgrade, regenerate and compile before building an additional public compatibility layer around generated code.

Runtime reading validates file magic, format version, schema, table ID, field count, size, and SHA-256 payload digest. Corruption or incompatibility throws explicitly. A single file is limited to 2 GiB.

## 8. Verify generated output is current

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py verify-generated `
  --build-config DataTables/datatable.build.json
```

This builds expected output in memory and compares the existing generated directory read-only. Missing, extra, or stale content returns 1. It does not write temporary files, delete extras, or change timestamps. Use it before commits and in CI.

Generate one table:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py generate `
  --build-config DataTables/datatable.build.json `
  --table Item
```

Initial generation, table addition/removal, a stale unselected table, or structural changes require a full generation. Single-table mode still validates every CSV, foreign key, and digest; it is not a bypass around dataset correctness.

## 9. Client/Server isolation and release export

The client Manifest contains only `Shared + ClientOnly`; the server Manifest contains only `Shared + ServerOnly`. Compare shared compatibility with:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable.py compare-manifests `
  --client DataTables/Generated/Data/manifest.client.json `
  --server DataTables/Generated/Data/manifest.server.json
```

Do not rely on clicking Godot Export for a formal release. Godot 4.7 EditorExportPlugin cannot reliably abort a bad export. Use the wrapper to run the read-only gate before launching Godot:

```powershell
python addons/godo_framework/Tools/DataTable/godo_datatable_export.py `
  --godot "E:/Godot/Godot_v4.7/Godot_v4.7-stable_mono_win64_console.exe" `
  --project . `
  --preset "Windows Desktop" `
  --output Builds/Windows/Game.exe `
  --mode release
```

A normal preset selects Client; a preset with the `dedicated_server` feature tag selects Server. Release maps only target `.gdtb` files and `manifest.json`; Debug also includes target `debug.json`. CSV, Profile, and full offline output should not enter the package.

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

DataTable is currently consumed mainly through generated code and has no stable GoDo public runtime API. Before upgrading, read that version's build report and rerun the complete verification workflow.
