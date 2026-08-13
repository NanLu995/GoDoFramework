---
translation_of: Docs/Manual/zh-cn/guides/friflo-ecs/index.md
translation_source_hash: sha256:83a564db0b19a7ebb453c7601d348ac3dd2ae479ae6affa7494f799866d4ca9e
---

# Batch Scene Data with Friflo ECS

This tutorial starts from an empty game scene, creates 1,000 moving entities that exist only in an ECS World, and lets `EcsWorldHost` update them during Godot Process. The output prints `[ECS] First update matched 1000 entities` once, confirming that the World, System, and frame driver are connected.

This path suits large homogeneous data sets such as unit movement, projectiles, and status effects. Menus, saves, scene changes, audio, ordinary UI, and small objects driven mainly by scene-tree behavior remain simpler as GoDo services or Godot nodes.

## 1. Install the optional integration

Install the GoDo core package first, then overlay `GoDoFramework-FrifloEcs-<version>.zip` at the project root. The archive preserves `addons/godo_framework/Integrations/FrifloEcs/` and contains adapter source plus the upstream MIT license, but no NuGet assembly.

Add the verified version to the target `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Friflo.Engine.ECS" Version="3.6.0" />
</ItemGroup>
```

Use the [official NuGet version page](https://www.nuget.org/packages/Friflo.Engine.ECS/3.6.0) to verify `3.6.0` and the exact `PackageReference`. **View NuGet package...** only opens that page. The tool never downloads the assembly; the package is obtained by the subsequent restore/build.

Alternatively, first run **Friflo ECS dependency check...** under `GoDo Framework → Open GoDo Framework... → Editor extensions`. **Add dependency...** is available only when the root contains exactly one ordinary `.csproj`, the reference is missing, and no `Directory.Packages.props` is detected. The confirmation shows the actual project file and exact `PackageReference`. On confirmation, the tool creates a non-overwriting `.godo-backup` beside the project, writes the reference, and checks again; existing backup names receive an incrementing suffix.

After either manual or assisted editing, run restore/build yourself and reopen Godot. The tool does not build the project or download packages. Multiple projects, central package management, MSBuild variable versions, conditional references, and malformed XML remain read-only and are reported for manual handling.

If the project centrally manages NuGet versions, follow its `Directory.Packages.props` or build convention instead of declaring the version twice. Multiple `.csproj` files, MSBuild property versions, and conditional references can require manual confirmation; the inspector reports that uncertainty instead of guessing the effective build result.

## 2. Define data-only components

Create `Position.cs` and `Velocity.cs` under a game-owned directory. Components belong to the game, not `GoDo.*`:

```csharp
using Friflo.Engine.ECS;

namespace MyGame.Simulation.Ecs;

public struct Position : IComponent
{
    public Position(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X;
    public float Y;
}

public readonly struct Velocity : IComponent
{
    public Velocity(float x, float y)
    {
        X = x;
        Y = y;
    }

    public readonly float X;
    public readonly float Y;
}
```

Components contain only data needed for batch computation. Do not store Godot nodes, resources, or scene objects in them. When results must appear on nodes, read ECS data at an explicit main-thread synchronization boundary; never manipulate Godot objects from a parallel Query.

## 3. Write a batch update System

Create `MovementSystem.cs`:

```csharp
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

namespace MyGame.Simulation.Ecs;

public sealed class MovementSystem : QuerySystem<Position, Velocity>
{
    private bool _reportedFirstUpdate;

    protected override void OnUpdate()
    {
        Query.ForEachEntity(static (
            ref Position position,
            ref Velocity velocity,
            Entity entity) =>
        {
            position.X += velocity.X;
            position.Y += velocity.Y;
        });

        if (_reportedFirstUpdate)
            return;

        _reportedFirstUpdate = true;
        GD.Print($"[ECS] First update matched {EntityCount} entities");
    }
}
```

The example adds one velocity unit per update to focus on validating the batch Query. A real per-second velocity can use `Tick.deltaTime`; physics-related simulation should configure the host for Physics.

Do not directly create or delete entities or add and remove components while iterating a Query. Use a Friflo `CommandBuffer` and replay structural changes at a safe boundary.

## 4. Create the scene and start the World

Create `BatchMovementDemo.tscn`:

```text
BatchMovementDemo (Node with BatchMovementDemo.cs)
└── EcsWorldHost (EcsWorldHost)
```

Leave `EcsWorldHost.UpdatePhase` as `Process` in the Inspector. Attach this script to the root:

```csharp
using Friflo.Engine.ECS;
using Godot;
using GoDo.Integrations.FrifloEcs;

namespace MyGame.Simulation.Ecs;

public partial class BatchMovementDemo : Node
{
    private EcsWorldHost? _host;

    public override void _Ready()
    {
        _host = GetNode<EcsWorldHost>("EcsWorldHost");
        _host.Systems.Add(new MovementSystem());

        for (int index = 0; index < 1_000; index++)
        {
            _host.Store.CreateEntity(
                new Position(index, 0f),
                new Velocity(1f, 0f));
        }

        _host.IsRunning = true;
    }
}
```

Run the scene. A single `[ECS] First update matched 1000 entities` confirms that the host created the World, the System matched all entities, and the selected Godot frame phase is driving updates.

`IsRunning` defaults to `false`. Register systems and initial entities before explicitly starting, avoiding an empty per-frame update. `UpdatePhase` cannot change after the host enters the tree; configure Physics in the scene resource or before calling `AddChild()`.

### Inspect the visible Demo3D synchronization example

`Templates/Demo3D/Gameplay/EcsSwarmDemo.cs` demonstrates the full boundary between ECS and Godot rendering. A Friflo World owns a fixed set of 512 position/velocity entities, while one `MultiMeshInstance3D` renders every visible instance without creating one Node per Entity. The host updates data at an earlier Process priority, then the game controller copies positions to the MultiMesh on the main thread.

Run `Templates/Demo3D/Boot/Boot.tscn` and enter Gameplay to see the blue swarm and its world-space status label on the right side of the arena. Pausing stops both the ECS System and visual synchronization. Leaving Gameplay destroys the World; entering again creates a new one. The 512 entities are a fixed regular-demo budget; the separate performance benchmark retains the 10,000/100,000-entity scale tests.

## 5. Pause, resume, and exit the scene

Pausing and resuming does not replace the World:

```csharp
public void SetSimulationPaused(bool paused)
{
    if (_host is not null)
        _host.IsRunning = !paused;
}
```

Setting it to `false` disables the corresponding Godot process, so there is no idle update; existing entities remain. Resuming continues with the same World.

When the host exits the scene tree, it stops updates and releases its references to `EntityStore` and `SystemRoot`. Re-entering creates a new World. Therefore:

- Do not cache an old `Entity` or `EntityStore` in a cross-scene service.
- Do not register `EcsWorldHost` in `Services` to create hidden long-lived ownership.
- Convert persistent information into explicit save or game state, then rebuild it in the new World.
- Call the idempotent `Shutdown()` only when an early manual stop is needed; accessing `Store` or `Systems` afterward throws `InvalidOperationException`.

## 6. Choose Process or Physics

- `Process`: visual simulation, non-physics unit logic, and batches that do not require a fixed step.
- `Physics`: fixed-step simulation that must cooperate with Godot physics timing.

One host runs in one phase. Do not update the same World twice per frame merely to use both callbacks. If both phases are genuinely required, establish separate ownership first and use two hosts that do not share a World.

## Troubleshooting

### The scene runs but the System does not execute

Confirm that `host.IsRunning = true` is set after systems and entities are registered, and that the scene-tree pause policy permits this node to process.

### Changing `UpdatePhase` in `_Ready()` throws

The host has already entered the tree and initialized by `_Ready()`. Set it in the Inspector, or assign `UpdatePhase` before `AddChild()` when constructing the host in code.

### Should every character Node become an Entity?

No. Start with data that measurements show benefits from batch processing. Animation, input, collision nodes, and UI can remain on the Godot side and connect through a small, explicit synchronization boundary.

### Can a future ECS replacement leave all game code unchanged?

Core GoDo modules and the host boundary do not depend on Friflo, but components, systems, queries, and entity operations use Friflo APIs directly. Keeping them together in a game-owned ECS namespace controls the migration surface; it does not remove migration cost.

See [Integrations and extensions](../../integrations/index.md) for installation and backend boundaries, and the [EcsWorldHost API](xref:GoDo.Integrations.FrifloEcs.EcsWorldHost) for exact host signatures.
