# NodePool 使用指南

## 定位与优势

`NodePool<T>` 复用由 `PackedScene` 实例化的 Godot Node，减少子弹、特效等高频对象反复 Instantiate/QueueFree 的成本。空闲节点保持在场景树外；Acquire 时加入指定父节点，Release 时移出并缓存。

池只负责实例所有权和生命周期，不猜测业务字段如何重置。只有性能测量或明确高频创建场景证明需要时才使用池。

## 定义可池化节点

```csharp
public partial class Projectile : Node2D, IPoolable
{
    public void OnAcquire()
    {
        Visible = true;
        SetProcess(true);
    }

    public void OnRelease()
    {
        SetProcess(false);
        Visible = false;
    }
}
```

Godot `_Ready()` 默认不会因复用再次调用；每次激活和清理必须放在 `OnAcquire/OnRelease`。

## 创建与使用

```csharp
PackedScene projectileScene = ResourceHub.Load<PackedScene>(projectileKey);
using var pool = new NodePool<Projectile>(
    projectileScene,
    initialSize: 20,
    idleCapacity: 100);

Projectile projectile = pool.Acquire(projectileRoot);

// 使用结束后归还，不要 QueueFree。
bool released = pool.Release(projectile);
```

- `initialSize` 是创建池时预热的空闲数量，不能大于 `idleCapacity`。
- `idleCapacity` 只限制空闲缓存，不限制同时活动数量；超出的归还节点会释放。
- PackedScene 根节点必须兼容 `T`，否则实例化会失败。

## 生命周期与失败语义

- Pool 拥有它创建的所有节点；外部禁止直接 QueueFree 活动节点。
- 重复 Release 或释放非本池节点返回 false，并通过 ErrorHub 警告。
- `OnAcquire` 失败会回滚并释放该节点，再抛出 `InvalidOperationException`。
- `OnRelease` 失败仍会移除并释放节点，再抛出 `InvalidOperationException`，不会把脏节点放回池。
- `Clear()` 只释放空闲节点，不影响活动节点。
- `Dispose()` 清理空闲节点，并对仍活动节点执行尽力 OnRelease 后强制释放；重复 Dispose 安全。

## 线程与性能

- 所有操作只能在创建池的 Godot 主线程调用。
- 预热会立即实例化节点，避免首轮峰值，但会增加启动时间和常驻对象数量。
- Release 后节点离开场景树，依赖 `_ExitTree()` 的逻辑会执行；复用时也会再次 `_EnterTree()`。
- Pool 不支持多线程、纯 C# 对象、自动字段重置或复杂淘汰策略。

## 自动回归验证

`Verification/Automated/NodePoolRegression.tscn` 使用最小 PackedScene 和 IPoolable 测试节点，验证预热、Acquire/Release 与实例复用、空闲容量、重复和外部节点拒绝、Clear 以及 Dispose 强制清理活动节点。

```powershell
& $env:GODOT_PATH --headless --path . Verification/Automated/NodePoolRegression.tscn
```

当前 runner 已通过 `dotnet build` 编译，并在项目声明的 Godot Mono Headless 版本中完成 6/6 项验证；成功退出码为 0，失败退出码为 1。测试节点只存在于 `Verification/Automated/`，不进入框架发布包。

## 常见误用

| 应该 | 避免 |
|---|---|
| 在 OnAcquire/OnRelease 重置全部可变状态 | 指望 `_Ready()` 每次复用执行 |
| 通过原 Pool Release | 对活动节点直接 QueueFree |
| 按测量设置合理预热和容量 | 为所有一次性节点建立对象池 |
| Dispose 长期池 | 丢失 Pool 引用却保留活动节点 |
