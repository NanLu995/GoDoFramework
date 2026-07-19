---
translation_of: Docs/Manual/zh-cn/guides/configuration/index.md
translation_source_hash: sha256:d2856808856ffad052ce542a261d365c835b6ed7da2b5e30323b5ee2b54dca71
---

# Create, Validate, and Query Typed Configuration

ConfigHub represents game configuration with C# types and validates content immediately after loading. Designers and developers still edit `.tres` assets in the Godot Inspector, while game code receives explicit types instead of parsing dictionaries, string fields, or JSON nodes throughout runtime.

Config only adds content validation and unique-key lookup. Resource location, type checking, and Godot Resource caching remain ResourceHub responsibilities; there is no second cache.

## When to use Config

Use it for:

- Static enemy, item, level-rule, and numeric-curve data shipped with the build.
- Detecting missing fields, duplicate IDs, and invalid values at startup or before gameplay begins.
- Fast runtime lookup by stable ID.

It is not intended for:

- Player saves or runtime settings; use SaveService or SettingsService.
- CSV/JSON import, online configuration, hot reload, or remote feature flags.
- Gameplay state that changes every frame.

## 1. Define one configuration entry

```csharp
using Godot;

namespace MyGame.Config;

[GlobalClass]
public sealed partial class EnemyDefinition : Resource
{
    [Export]
    public string Id { get; set; } = string.Empty;

    [Export(PropertyHint.Range, "1,100000,1")]
    public int MaxHealth { get; set; } = 100;

    [Export(PropertyHint.Range, "0,10000,0.1")]
    public float MoveSpeed { get; set; } = 100f;
}
```

Configuration types belong to the game namespace, not `GoDo.*`. An ID is a stable contract shared by saves, levels, and game code; do not derive it from an array position or display name.

## 2. Define a catalog Resource and validate it

```csharp
using System;
using System.Collections.Generic;
using Godot;
using GoDo;

namespace MyGame.Config;

[GlobalClass]
public sealed partial class EnemyCatalog : Resource, IConfigResource
{
    [Export]
    public Godot.Collections.Array<EnemyDefinition> Entries { get; set; } = new();

    public void Validate()
    {
        if (Entries.Count == 0)
            throw new InvalidOperationException("Enemy catalog cannot be empty.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < Entries.Count; i++)
        {
            EnemyDefinition? entry = Entries[i];
            if (entry == null)
                throw new InvalidOperationException($"Entry {i} cannot be null.");
            if (string.IsNullOrWhiteSpace(entry.Id))
                throw new InvalidOperationException($"Entry {i} has no Id.");
            if (!string.Equals(entry.Id, entry.Id.Trim(), StringComparison.Ordinal))
                throw new InvalidOperationException($"Id '{entry.Id}' has surrounding whitespace.");
            if (!ids.Add(entry.Id))
                throw new InvalidOperationException($"Id '{entry.Id}' is duplicated.");
            if (entry.MaxHealth <= 0)
                throw new InvalidOperationException($"'{entry.Id}' health must be positive.");
            if (entry.MoveSpeed < 0)
                throw new InvalidOperationException($"'{entry.Id}' move speed cannot be negative.");
        }
    }
}
```

`Validate()` should check every invariant that can be determined from this Resource and give a reason that identifies the bad content directly. Typical checks include:

- Required fields, null entries, and surrounding whitespace.
- Numeric ranges and relationships between fields.
- Stable-ID uniqueness.
- Valid game enums, Resource references, and array structure.

Do not mutate configuration, create scene Nodes, publish game events, or write saves in Validate. Do not catch and report through ErrorHub there; ConfigHub preserves the original exception for the calling boundary.

## 3. Create the asset in Inspector

After Godot completes the C# build, create an `EnemyCatalog` Resource in FileSystem, for example:

```text
res://Config/EnemyCatalog.tres
```

Add `EnemyDefinition` subresources to `Entries` and assign stable IDs. Commit the `.tres` and matching `.uid` so Resource references remain stable if files move.

Inspector ranges, enums, and Resource-type restrictions reduce input errors but do not replace runtime `Validate()`. Text merges, script changes, and migrations can still produce invalid combinations the Inspector could not prevent.

## 4. Load and handle failure at startup

```csharp
private static readonly ResourceKey EnemyCatalogKey =
    ResourceKey.FromPath("res://Config/EnemyCatalog.tres");

EnemyCatalog catalog;
try
{
    catalog = ConfigHub.Load<EnemyCatalog>(EnemyCatalogKey);
}
catch (ResourceLoadException exception)
{
    ErrorHub.Fatal(exception, "Game.Config", context: "EnemyCatalog load");
    ShowStartupFailure();
    return;
}
catch (ConfigValidationException exception)
{
    ErrorHub.Fatal(
        exception,
        "Game.Config",
        context: $"type={exception.ConfigType.Name} key={exception.Key.Value}");
    ShowStartupFailure();
    return;
}
```

The two failures mean different things:

- `ResourceLoadException`: the path is missing, loading failed, or the actual type differs.
- `ConfigValidationException`: the Resource loaded, but `Validate()` rejected its content; `InnerException` preserves the concrete reason.

ConfigHub never returns null and does not report before throwing. Record the failure once at the boundary that can fall back or stop startup.

Only synchronous loading currently exists. Call it on Godot's main thread after ResourceHub initialization. Do not wrap Resource loading in `Task.Run`.

## 5. Build a unique-key lookup table

Build the index once after loading and validation:

```csharp
var enemies = new ConfigTable<string, EnemyDefinition>(
    catalog.Entries,
    entry => entry.Id,
    StringComparer.Ordinal);
```

Query a required entry directly:

```csharp
EnemyDefinition boss = enemies.Get("boss");
```

When absence is genuinely allowed:

```csharp
if (enemies.TryGet("tutorial_dummy", out EnemyDefinition? dummy))
    Spawn(dummy);
```

ConfigTable construction immediately throws `ArgumentException` for a null entry, null key, or duplicate key. A missing `Get()` throws `KeyNotFoundException`; `TryGet()` returns `false`.

String IDs normally use `StringComparer.Ordinal`, remaining case-sensitive and independent of system locale. Use another comparer only when the game contract explicitly ignores case, and keep saves and tooling on the same rule.

## 6. Retain configuration instead of reloading per frame

```csharp
public sealed class GameConfig
{
    public EnemyCatalog EnemyCatalog { get; }
    public ConfigTable<string, EnemyDefinition> Enemies { get; }

    public GameConfig(EnemyCatalog catalog)
    {
        EnemyCatalog = catalog;
        Enemies = new ConfigTable<string, EnemyDefinition>(
            catalog.Entries,
            entry => entry.Id,
            StringComparer.Ordinal);
    }
}
```

ConfigHub retains no second reference, and ConfigTable does not deep-copy entries. The game startup layer chooses retention lifetime and treats loaded configuration as read-only data.

ConfigTable construction costs O(n) time and O(n) index memory; average lookup is O(1). Do not reload configuration and rebuild an index in `_Process()`, for every enemy spawn, or whenever a panel opens.

## 7. Workflow after changing configuration

The current version has no hot reload. After changing a `.tres` or configuration C# type:

1. Let Godot complete Resource scanning and the C# build.
2. Restart the relevant test or game session.
3. Let startup loading execute Validate again.
4. Check scenes, save migration, and code that refer to affected IDs.

Removing or renaming a stable ID is a game-data compatibility change. ConfigHub can detect errors within the current file, but it does not migrate old saves or references in other Resources.

## Common failures

- Configuration cannot load: the path is wrong, the file was not committed, the actual Resource type differs, or ResourceHub is not initialized.
- Validation only says “invalid”: the exception lacks entry index, ID, and field reason, so content authors cannot locate it.
- Duplicate error logs: Validate or a lower layer reports, then startup reports again; record once at the final handling boundary.
- Lookup sometimes fails: ID case or surrounding-whitespace rules are inconsistent.
- Runtime does not change after an edit: hot reload is not supported; restart the loading session.
- Entries are mutated after ConfigTable creation: the index does not deep-copy objects, so game code must treat configuration as read-only.
- Per-frame allocation appears: ConfigTable is rebuilt on a hot path instead of being cached during initialization.

For exact members, see <xref:GoDo.ConfigHub>, <xref:GoDo.IConfigResource>, <xref:GoDo.ConfigTable%602>, and <xref:GoDo.ConfigValidationException>.
