# 使用节点池复用高频对象

`NodePool<T>` 复用由 `PackedScene` 实例化的 Godot Node。节点租出时加入指定父节点，归还后离开场景树并进入空闲缓存，从而减少子弹、命中特效或飘字等高频对象反复实例化和释放的成本。

对象池会增加生命周期复杂度。先用简单的 Instantiate/QueueFree 完成功能，只有性能测量确认创建和释放是瓶颈，或对象确实高频出现时再使用。

## 什么时候值得使用

适合：

- 同一 PackedScene 在短时间内大量创建和销毁。
- 节点状态可以可靠、完整地重置。
- 活动峰值和合理的空闲容量可以估算。

不适合：

- 偶尔创建的菜单、关卡根节点或复杂剧情对象。
- 依赖 `_Ready()` 每次重新初始化的节点。
- 纯 C# 对象、多线程工作或需要复杂淘汰策略的缓存。

## 1. 让节点支持复用生命周期

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

Godot `_Ready()` 默认只在节点第一次进入树时调用，不会因池化复用再次执行。每次租借需要的初始化放在 `OnAcquire()`，每次归还需要的清理放在 `OnRelease()`。

需要重置的内容通常包括：位置相关临时状态、速度、计时器、动画、粒子发射、可见性、碰撞、信号订阅、异步 Token 和对其他场景对象的引用。不要让上一次使用的数据泄漏到下一次租借。

## 2. 加载场景并创建 Pool

由拥有这批对象的长期 Node 创建 Pool：

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

PackedScene 的根节点必须兼容 `Projectile`，否则实例化会失败。

- `initialSize` 是创建 Pool 时立即预热的空闲数量。
- `idleCapacity` 是最多保留多少空闲节点，不限制同时活动数量。
- `initialSize` 不能大于 `idleCapacity`。

预热能降低第一次密集生成时的峰值，但会增加场景启动时间和初始对象数量。容量应来自实际峰值测量，不要为了“保险”设置成极大值。

## 3. 租借并初始化业务数据

```csharp
Projectile projectile = _projectiles.Acquire(_projectileRoot);
projectile.GlobalPosition = muzzle.GlobalPosition;
projectile.Launch(direction * speed);
```

`Acquire()` 先把节点加入给定父节点，再调用 `OnAcquire()`，因此回调中可以安全访问树和父级相关状态。父节点必须是仍然有效的 Godot Node。

把通用复位放进 `OnAcquire()`，把本次发射的参数通过明确方法设置。不要让 Pool 知道速度、伤害或阵营等玩法概念。

## 4. 使用结束后归还

```csharp
bool released = _projectiles.Release(projectile);
if (!released)
    ErrorHub.Warn("Projectile was already released.", "Game.Projectile");
```

归还顺序是：

1. 从活动集合移除。
2. 调用 `OnRelease()` 清理业务状态。
3. 从场景树移除。
4. 空闲区未满则缓存，否则释放节点。

活动节点属于它的 Pool。不要对它直接 `QueueFree()`，也不要交给另一个 Pool 归还。重复归还或归还外部节点会返回 `false`，并由 ErrorHub 发出 Warning。

推荐让 Projectile 在命中或超时后通知管理器归还，或持有一个明确的释放回调；不要让节点在不知道所属 Pool 的情况下自行 `QueueFree()`。

## 5. 正确处理信号和异步状态

如果节点在每次租借时订阅外部长生命周期对象，应在归还时对称解绑：

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

更稳妥的做法是由明确的 `Launch`/`Configure` 方法建立本次引用，由 `OnRelease` 清理。匿名 lambda、未取消的 Scheduler 任务和未停止的 Tween 都可能在节点归还后继续修改空闲对象。

节点每次归还会 `_ExitTree()`，复用时再次 `_EnterTree()`。不要同时在这些 Godot 回调和 IPoolable 回调中重复执行同一项订阅或清理。

## 6. 清空与关闭 Pool

```csharp
_projectiles.Clear();
```

`Clear()` 只释放当前空闲节点，不影响仍在场景中活动的节点。它适合释放暂时不用的缓存，但 Pool 之后仍能继续创建新节点。

拥有者退出时关闭：

```csharp
public override void _ExitTree()
{
    _projectiles?.Dispose();
    _projectiles = null;
}
```

`Dispose()` 会释放空闲节点，并对仍活动的节点尽力调用 `OnRelease()` 后强制释放。存在活动节点时会产生 Warning，通常说明所有权边界或退出顺序值得检查。重复 Dispose 安全；关闭后再 Acquire、Release 或 Clear 会抛出 `ObjectDisposedException`。

## 失败时如何保证干净状态

- `OnAcquire()` 抛出异常：该节点从活动集合移除并被释放，然后抛出 `InvalidOperationException`。
- `OnRelease()` 抛出异常：节点仍会移出场景树并释放，不会把脏节点放回空闲区，然后抛出 `InvalidOperationException`。
- 外部已经释放或 QueueFree 活动节点：Release 返回 `false` 并报告 Warning。
- 所有 Pool 操作只能在创建它的 Godot 主线程执行。

生命周期回调应快速、可重复理解且避免失败。不要在 `OnRelease()` 保存游戏进度、联网或执行可能长时间阻塞的工作。

## 容量调优

开发时观察 `ActiveCount` 和 `IdleCount`：

```csharp
LogHub.Debug(
    "Projectile pool state",
    "Game.Pool",
    context: $"active={_projectiles.ActiveCount} idle={_projectiles.IdleCount}");
```

以常见峰值作为预热参考，以可接受的内存占用设置空闲容量。活动数量超过 `idleCapacity` 并不会拒绝生成；只是这些额外节点归还时不会进入缓存。

如果峰值极少出现，不必按最大峰值永久保留全部节点。反之，如果 Idle 经常归零并且实例化仍造成明显卡顿，再逐步提高预热或容量并重新测量。

## 常见错误

- 第二次租借状态异常：只在 `_Ready()` 初始化，没有完整实现 `OnAcquire/OnRelease`。
- Release 返回 `false`：节点已归还、属于另一个 Pool，或被外部 QueueFree。
- 切换场景后收到旧回调：归还时没有解绑信号、取消 Scheduler 或停止 Tween。
- 内存没有下降：空闲容量过大；需要调低并调用 `Clear()` 验证。
- 首次密集生成卡顿：预热不足，或 PackedScene 自身初始化过重。
- Dispose 报告仍有活动节点：拥有者退出前没有先停止生成并归还活动对象。
- 为所有 Node 建池后代码更复杂：没有性能证据时应恢复简单 Instantiate/QueueFree。

精确接口可查询 <xref:GoDo.NodePool%601> 和 <xref:GoDo.IPoolable>。
