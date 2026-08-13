using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using GoDo.Integrations.FrifloEcs;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>Friflo ECS 场景级宿主生命周期与帧阶段语义的无交互回归入口。</summary>
public sealed partial class FrifloEcsRegression : Node
{
    private int _passed;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            Run("Process 宿主初始化与更新", VerifyProcessHost);
            Run("Physics 宿主只启用物理更新", VerifyPhysicsHost);
            Run("暂停保留 World", VerifyPauseKeepsWorld);
            Run("初始化后拒绝更改阶段", VerifyPhaseChangeRejected);
            Run("关闭幂等且拒绝继续访问", VerifyShutdown);
            Run("重新进入场景树创建新 World", VerifyReentryCreatesNewWorld);

            GD.Print($"[FrifloEcsRegression] PASS ({_passed}/6)");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[FrifloEcsRegression] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run(string name, Action verification)
    {
        verification();
        _passed++;
        GD.Print($"[FrifloEcsRegression] PASS: {name}");
    }

    private void VerifyProcessHost()
    {
        EcsWorldHost host = CreateHost(EcsUpdatePhase.Process);
        try
        {
            var system = new CountingSystem();
            host.Systems.Add(system);
            host.Store.CreateEntity(new CounterComponent());
            host.IsRunning = true;

            host._Process(0.25d);

            Assert(host.IsInitialized, "Process 宿主没有初始化");
            Assert(host.IsProcessing(), "Process 宿主没有启用 Process");
            Assert(!host.IsPhysicsProcessing(), "Process 宿主错误启用了 Physics Process");
            Assert(system.UpdateCount == 1, "Process 没有驱动系统一次");
            Assert(Math.Abs(system.LastDelta - 0.25f) < 0.0001f, "Process delta 没有传入 Friflo");
            Assert(system.EntityCount == 1, "系统没有访问宿主 World 中的实体");
        }
        finally
        {
            ReleaseHost(host);
        }
    }

    private void VerifyPhysicsHost()
    {
        EcsWorldHost host = CreateHost(EcsUpdatePhase.Physics);
        try
        {
            var system = new CountingSystem();
            host.Systems.Add(system);
            host.IsRunning = true;

            host._PhysicsProcess(0.125d);

            Assert(!host.IsProcessing(), "Physics 宿主错误启用了 Process");
            Assert(host.IsPhysicsProcessing(), "Physics 宿主没有启用 Physics Process");
            Assert(system.UpdateCount == 1, "Physics Process 没有驱动系统一次");
            Assert(Math.Abs(system.LastDelta - 0.125f) < 0.0001f, "Physics delta 没有传入 Friflo");
        }
        finally
        {
            ReleaseHost(host);
        }
    }

    private void VerifyPauseKeepsWorld()
    {
        EcsWorldHost host = CreateHost(EcsUpdatePhase.Process);
        try
        {
            var system = new CountingSystem();
            host.Systems.Add(system);
            EntityStore store = host.Store;

            host.IsRunning = true;
            host.IsRunning = false;
            host._Process(0.25d);

            Assert(system.UpdateCount == 0, "暂停时系统仍被更新");
            Assert(ReferenceEquals(store, host.Store), "暂停时替换了 World");
            Assert(!host.IsProcessing(), "暂停时 Process 没有关闭");
            Assert(!host.IsPhysicsProcessing(), "暂停时 Physics Process 没有关闭");

            host.IsRunning = true;
            host._Process(0.25d);
            Assert(system.UpdateCount == 1, "恢复后系统没有继续更新");
            Assert(host.IsProcessing(), "恢复后 Process 没有重新启用");
        }
        finally
        {
            ReleaseHost(host);
        }
    }

    private void VerifyShutdown()
    {
        EcsWorldHost host = CreateHost(EcsUpdatePhase.Process);
        try
        {
            host.Shutdown();
            host.Shutdown();

            Assert(!host.IsInitialized, "关闭后宿主仍显示已初始化");
            Assert(!host.IsProcessing(), "关闭后 Process 仍启用");
            Assert(!host.IsPhysicsProcessing(), "关闭后 Physics Process 仍启用");
            AssertThrows<InvalidOperationException>(() => _ = host.Store, "关闭后仍可获取 World");
            AssertThrows<InvalidOperationException>(() => _ = host.Systems, "关闭后仍可获取系统根");
        }
        finally
        {
            ReleaseHost(host);
        }
    }

    private void VerifyPhaseChangeRejected()
    {
        EcsWorldHost host = CreateHost(EcsUpdatePhase.Process);
        try
        {
            AssertThrows<InvalidOperationException>(
                () => host.UpdatePhase = EcsUpdatePhase.Physics,
                "初始化后仍可更改更新阶段");
        }
        finally
        {
            ReleaseHost(host);
        }
    }

    private void VerifyReentryCreatesNewWorld()
    {
        EcsWorldHost host = CreateHost(EcsUpdatePhase.Process);
        EntityStore firstStore = host.Store;

        RemoveChild(host);
        Assert(!host.IsInitialized, "退出场景树后宿主仍显示已初始化");

        AddChild(host);
        EntityStore secondStore = host.Store;
        Assert(!ReferenceEquals(firstStore, secondStore), "重新进入场景树复用了已关闭的 World");

        ReleaseHost(host);
    }

    private EcsWorldHost CreateHost(EcsUpdatePhase updatePhase)
    {
        var host = new EcsWorldHost { UpdatePhase = updatePhase };
        AddChild(host);
        Assert(!host.IsRunning, "ECS 宿主不应默认运行");
        Assert(!host.IsProcessing(), "无业务系统的宿主不应默认启用 Process");
        Assert(!host.IsPhysicsProcessing(), "无业务系统的宿主不应默认启用 Physics Process");
        return host;
    }

    private static void ReleaseHost(EcsWorldHost host)
    {
        if (!GodotObject.IsInstanceValid(host))
            return;

        host.GetParent()?.RemoveChild(host);
        host.Free();
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CountingSystem : QuerySystem<CounterComponent>
    {
        public int UpdateCount { get; private set; }

        public float LastDelta { get; private set; }

        protected override void OnUpdate()
        {
            UpdateCount++;
            LastDelta = Tick.deltaTime;
        }
    }

    private struct CounterComponent : IComponent
    {
    }
}
