using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using GoDo;
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
            Run("Debug 快照只读采集", VerifyDebugSnapshot);
            Run("Debugger ECS 页面渲染", VerifyDebuggerPage);

            GD.Print($"[FrifloEcsRegression] PASS ({_passed}/8)");
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
            AssertThrows<InvalidOperationException>(() => _ = host.Systems, "关闭后仍可获取 SystemRoot");
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

    private void VerifyDebugSnapshot()
    {
#if DEBUG
        int baseline = EcsWorldDebugRegistry.GetSnapshot(32, 128).RegisteredWorldCount;
        EcsWorldHost host = CreateDebugHost();
        string hostPath = host.GetPath().ToString();
        try
        {
            EcsWorldDebugSnapshot snapshot = EcsWorldDebugRegistry.GetSnapshot(32, 128);
            Assert(snapshot.RegisteredWorldCount == baseline + 1, "Debug 注册表没有登记新宿主");
            Assert(snapshot.RunningWorldCount >= 1, "Debug 快照没有统计运行中的宿主");

            EcsWorldDebugEntry world = FindWorld(snapshot, hostPath);
            Assert(world.EntityCount == 2, "Debug 快照 Entity 数量错误");
            Assert(world.ArchetypeCount > 0, "Debug 快照没有采集 Archetype");
            Assert(world.IsPerformanceMonitoringEnabled, "Debug 快照没有反映业务启用的性能监控");
            Assert(world.SystemCount == 1, "Debug 快照 System 数量错误");

            EcsSystemDebugEntry system = snapshot.Systems[world.SystemStartIndex];
            Assert(system.HasEntityCount && system.EntityCount == 2, "Debug 快照 Query Entity 数量错误");
            Assert(system.HasPerformance && system.UpdateCount >= 1, "Debug 快照没有读取性能计数");
        }
        finally
        {
            ReleaseHost(host);
        }

        Assert(
            EcsWorldDebugRegistry.GetSnapshot(32, 128).RegisteredWorldCount == baseline,
            "宿主释放后仍残留在 Debug 注册表");
#endif
    }

    private void VerifyDebuggerPage()
    {
#if DEBUG
        EcsWorldHost host = CreateDebugHost();
        EcsWorldHost unmonitoredHost = CreateHost(EcsUpdatePhase.Physics);
        unmonitoredHost.Systems.Add(new CountingSystem());
        try
        {
            Assert(!unmonitoredHost.Systems.MonitorPerf, "测试宿主意外预先开启性能监控");
            DebuggerOverlay overlay = GetNode<DebuggerOverlay>("/root/GoDoRuntime/GoDoDebugger");
            Tree navigation = overlay.GetNode<Tree>("Panel/Margin/VBox/Body/Navigation");
            TreeItem? ecsPage = FindTreeItemRecursive(navigation.GetRoot(), "ECS");
            Assert(ecsPage is not null, "启用 Friflo 集成后没有注册 ECS 导航页");
            ecsPage!.Select(0);
            navigation.EmitSignal(Tree.SignalName.ItemSelected);

            VBoxContainer dashboard = overlay.GetNode<VBoxContainer>(
                "Panel/Margin/VBox/Body/Page/EcsDashboard");
            Label worlds = dashboard.GetNode<Label>("Summary/WorldsCard/Content/Value");
            Label entities = dashboard.GetNode<Label>("Summary/EntitiesCard/Content/Value");
            Tree worldTree = dashboard.GetNode<Tree>("WorldList");
            Tree systemTree = dashboard.GetNode<Tree>("SystemList");

            Assert(dashboard.Visible, "选择 ECS 页面后面板没有显示");
            Assert(int.TryParse(worlds.Text, out int worldCount) && worldCount >= 1,
                "ECS 页面 World 汇总没有刷新");
            Assert(int.TryParse(entities.Text, out int entityCount) && entityCount >= 2,
                "ECS 页面 Entity 汇总没有刷新");
            Assert(FindTreeItemRecursive(worldTree.GetRoot(), host.GetPath().ToString()) is not null,
                "ECS 页面宿主表没有显示测试宿主");
            Assert(FindTreeItemRecursive(systemTree.GetRoot(), nameof(CountingSystem)) is not null,
                "ECS 页面系统树没有显示测试系统");
            Assert(!unmonitoredHost.Systems.MonitorPerf, "Debugger ECS 页面擅自开启了性能监控");
        }
        finally
        {
            ReleaseHost(host);
            ReleaseHost(unmonitoredHost);
        }
#endif
    }

#if DEBUG
    private EcsWorldHost CreateDebugHost()
    {
        EcsWorldHost host = CreateHost(EcsUpdatePhase.Process);
        host.Name = "EcsDebuggerHost";
        host.Systems.Add(new CountingSystem());
        host.Systems.SetMonitorPerf(true);
        host.Store.CreateEntity(new CounterComponent());
        host.Store.CreateEntity(new CounterComponent());
        host.IsRunning = true;
        host._Process(0.25d);
        return host;
    }

    private static EcsWorldDebugEntry FindWorld(EcsWorldDebugSnapshot snapshot, string path)
    {
        for (int index = 0; index < snapshot.Worlds.Length; index++)
        {
            if (string.Equals(snapshot.Worlds[index].NodePath, path, StringComparison.Ordinal))
                return snapshot.Worlds[index];
        }

        throw new InvalidOperationException($"Debug 快照找不到宿主：{path}");
    }

    private static TreeItem? FindTreeItemRecursive(TreeItem? parent, string text)
    {
        TreeItem? item = parent?.GetFirstChild();
        while (item is not null)
        {
            if (item.GetText(0) == text)
                return item;

            TreeItem? child = FindTreeItemRecursive(item, text);
            if (child is not null)
                return child;
            item = item.GetNext();
        }

        return null;
    }
#endif

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
