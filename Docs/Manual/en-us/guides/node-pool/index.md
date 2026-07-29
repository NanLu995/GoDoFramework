---
translation_of: Docs/Manual/zh-cn/guides/node-pool/index.md
translation_source_hash: sha256:e367a7e605f914a2dd36cfa655558f7f7319496aa755003a870bdf831556e9e6
---

# Reuse High-Frequency Objects with NodePool

`NodePool<T>` reuses Godot Nodes instantiated from a `PackedScene`. An acquired Node joins a specified parent; after release it leaves the scene tree and enters an idle cache. This can reduce repeated instantiation and freeing costs for high-frequency objects such as projectiles, hit effects, and floating text.

Pooling adds lifecycle complexity. Build the feature with straightforward Instantiate/QueueFree first. Add a pool only when measurement identifies creation and deletion as a bottleneck, or when the object is clearly produced at high frequency.

## When pooling is worthwhile

Use it when:

- The same PackedScene is created and destroyed many times in a short period.
- Every mutable part of the Node can be reset reliably.
- You can estimate active peaks and a reasonable idle capacity.

It is not intended for:

- Occasionally created menus, level roots, or complex narrative objects.
- Nodes that depend on `_Ready()` running for every activation.
- Plain C# objects, background-thread work, or caches needing complex eviction.

## 1. Implement the reusable lifecycle

```csharp
using Godot;
using GoDo;

namespace MyGame;

public partial class Projectile : Node2D, IPoolable
{
    private Vector2 _velocity;
    private bool _hasHit;

    public void OnAcquire()
    {
        _velocity = Vector2.Zero;
        _hasHit = false;
        Visible = true;
        SetProcess(true);
    }

    public void OnRelease()
    {
        SetProcess(false);
        Visible = false;
        _velocity = Vector2.Zero;
        _hasHit = false;
    }
}
```

Godot normally calls `_Ready()` only the first time a Node enters the tree, not each time a pooled Node is reused. Put per-acquisition initialization in `OnAcquire()` and per-release cleanup in `OnRelease()`.

Typical reset state includes temporary position data, velocity, timers, animation, particle emission, visibility, collision, signal subscriptions, async tokens, and references to other scene objects. Never leak data from one use into the next.

## 2. Load the scene and create a Pool

The long-lived Node that owns these objects creates the Pool:

```csharp
private NodePool<Projectile>? _projectiles;

public override void _Ready()
{
    PackedScene scene = ResourceHub.Load<PackedScene>(
        ResourceKey.FromPath("res://Gameplay/Projectile.tscn"));

    _projectiles = new NodePool<Projectile>(
        scene,
        initialSize: 20,
        idleCapacity: 100);
}
```

The PackedScene root must be compatible with `Projectile`, or instantiation fails.

- `initialSize` is the number of idle instances created immediately.
- `idleCapacity` is the maximum retained idle count; it does not limit simultaneous active Nodes.
- `initialSize` cannot exceed `idleCapacity`.

Prewarming can reduce the first dense spawn peak, but increases scene startup work and initial object count. Derive capacity from actual peak measurements rather than choosing a very large safety value.

## 3. Acquire and configure game data

```csharp
Projectile projectile = _projectiles.Acquire(_projectileRoot);
projectile.GlobalPosition = muzzle.GlobalPosition;
projectile.Launch(direction * speed);
```

`Acquire()` adds the Node to its parent before calling `OnAcquire()`, so that callback may safely access tree and parent-related state. The parent must be a valid Godot Node that is not queued for deletion.

Keep generic reset work in `OnAcquire()` and pass this launch's parameters through an explicit method. The Pool should not understand gameplay concepts such as speed, damage, or faction.

## 4. Release after use

```csharp
bool released = _projectiles.Release(projectile);
if (!released)
    ErrorHub.Warn("Projectile was already released.", "Game.Projectile");
```

Release performs these steps:

1. Remove the Node from the active set.
2. Call `OnRelease()` to clean game state.
3. Remove it from the scene tree.
4. Cache it if idle capacity allows, otherwise free it.

An active Node belongs to its Pool. Do not call `QueueFree()` on it and do not return it to another Pool. Releasing an external or already released Node returns `false` and emits an ErrorHub Warning.

Prefer having a Projectile notify its manager when it hits or expires, or give it an explicit release callback. A Node that does not know its owning Pool should not free itself.

## 5. Clean signals and async state

If each use subscribes to a longer-lived external object, unsubscribe on release:

```csharp
public void OnAcquire()
{
    _target.HealthChanged += OnTargetHealthChanged;
}

public void OnRelease()
{
    if (GodotObject.IsInstanceValid(_target))
        _target.HealthChanged -= OnTargetHealthChanged;

    _target = null;
    _lifetimeCancellation?.Cancel();
    _lifetimeCancellation?.Dispose();
    _lifetimeCancellation = null;
}
```

A clearer design often establishes per-use references through `Launch` or `Configure` and clears them in `OnRelease`. Anonymous lambdas, uncancelled Scheduler tasks, and running Tweens may otherwise continue changing an idle Node.

Every release causes `_ExitTree()`, and reuse causes `_EnterTree()` again. Do not duplicate the same subscription or cleanup in both Godot callbacks and IPoolable callbacks.

## 6. Clear and dispose the Pool

```csharp
_projectiles.Clear();
```

`Clear()` frees only idle Nodes and does not affect active Nodes still in the scene. It can release an unused cache while keeping the Pool available for future creation.

Dispose when the owner exits:

```csharp
public override void _ExitTree()
{
    _projectiles?.Dispose();
    _projectiles = null;
}
```

`Dispose()` closes the Pool before freeing idle Nodes and attempting `OnRelease()` on every active Node. Therefore, `OnRelease()` cannot acquire another Node while the Pool is shutting down. Active Nodes produce a Warning, usually indicating an ownership or shutdown-order issue. Repeated Dispose is safe; Acquire, Release, or Clear after disposal throws `ObjectDisposedException`.

## Clean failure behavior

- `OnAcquire()` throws: the Node is removed from active ownership and freed, then `InvalidOperationException` is thrown.
- `OnRelease()` throws: the Node still leaves the scene tree and is freed instead of entering the idle cache, then `InvalidOperationException` is thrown.
- An active Node was externally freed or queued: Release returns `false` and reports a Warning.
- Pool construction, prewarming, and every operation must run on the Godot main thread.

Lifecycle callbacks should be fast, understandable, and unlikely to fail. Do not save game progress, call a network service, or perform long blocking work in `OnRelease()`.

## Tune capacity

Inspect `ActiveCount` and `IdleCount` during development:

```csharp
LogHub.Debug(
    "Projectile pool state",
    "Game.Pool",
    context: $"active={_projectiles.ActiveCount} idle={_projectiles.IdleCount}");
```

Use the common peak as a prewarm reference and choose idle capacity according to acceptable memory cost. Active count may exceed `idleCapacity`; those extra Nodes are simply not cached when returned.

If a rare peak is enormous, retaining the full maximum forever may be wasteful. If idle count frequently reaches zero and instantiation still causes visible hitches, increase prewarm or capacity incrementally and measure again.

## Common failures

- State is wrong on the second acquisition: initialization exists only in `_Ready()` and the acquire/release reset is incomplete.
- Release returns `false`: the Node was already returned, belongs to another Pool, or was externally QueueFree'd.
- Old callbacks run after a scene switch: release did not disconnect a signal, cancel Scheduler work, or stop a Tween.
- Memory does not fall: idle capacity is too large; reduce it and verify with `Clear()`.
- The first dense spawn hitches: prewarm is insufficient, or the PackedScene itself performs excessive initialization.
- Dispose reports active Nodes: the owner did not stop spawning and return active instances before exit.
- Pooling every Node made code harder: without performance evidence, return to simple Instantiate/QueueFree.

For exact members, see <xref:GoDo.NodePool%601> and <xref:GoDo.IPoolable>.
