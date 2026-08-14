#if DEBUG
using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

#nullable enable

namespace GoDo.Integrations.FrifloEcs;

internal readonly record struct EcsWorldDebugSnapshot(
    int RegisteredWorldCount,
    int RunningWorldCount,
    long TotalEntityCount,
    long TotalArchetypeCount,
    bool WorldsTruncated,
    bool SystemsTruncated,
    EcsWorldDebugEntry[] Worlds,
    EcsSystemDebugEntry[] Systems);

internal readonly record struct EcsWorldDebugEntry(
    string NodePath,
    EcsUpdatePhase UpdatePhase,
    bool IsRunning,
    int EntityCount,
    int ArchetypeCount,
    long ArchetypeCapacity,
    bool IsPerformanceMonitoringEnabled,
    int SystemStartIndex,
    int SystemCount);

internal readonly record struct EcsSystemDebugEntry(
    int ParentIndex,
    string Name,
    string TypeName,
    bool IsEnabled,
    bool HasEntityCount,
    int EntityCount,
    bool HasPerformance,
    float LastMilliseconds,
    int UpdateCount,
    long LastAllocatedBytes);

internal static class EcsWorldDebugRegistry
{
    private static readonly List<WeakReference<EcsWorldHost>> Hosts = new();

    internal static void Register(EcsWorldHost host)
    {
        for (int index = Hosts.Count - 1; index >= 0; index--)
        {
            if (!Hosts[index].TryGetTarget(out EcsWorldHost? candidate) ||
                !GodotObject.IsInstanceValid(candidate))
            {
                Hosts.RemoveAt(index);
                continue;
            }

            if (ReferenceEquals(candidate, host))
                return;
        }

        Hosts.Add(new WeakReference<EcsWorldHost>(host));
    }

    internal static void Unregister(EcsWorldHost host)
    {
        for (int index = Hosts.Count - 1; index >= 0; index--)
        {
            if (!Hosts[index].TryGetTarget(out EcsWorldHost? candidate) ||
                !GodotObject.IsInstanceValid(candidate) ||
                ReferenceEquals(candidate, host))
                Hosts.RemoveAt(index);
        }
    }

    internal static EcsWorldDebugSnapshot GetSnapshot(int maxWorlds, int maxSystems)
    {
        var worlds = new List<EcsWorldDebugEntry>(Math.Min(Hosts.Count, maxWorlds));
        var systems = new List<EcsSystemDebugEntry>(Math.Min(maxSystems, 32));
        int registeredWorldCount = 0;
        int runningWorldCount = 0;
        long totalEntityCount = 0;
        long totalArchetypeCount = 0;
        bool systemsTruncated = false;

        for (int index = Hosts.Count - 1; index >= 0; index--)
        {
            if (!Hosts[index].TryGetTarget(out EcsWorldHost? host) ||
                !GodotObject.IsInstanceValid(host) ||
                !host.IsInsideTree() ||
                !host.IsInitialized)
            {
                Hosts.RemoveAt(index);
            }
        }

        for (int index = 0; index < Hosts.Count; index++)
        {
            if (!Hosts[index].TryGetTarget(out EcsWorldHost? host))
                continue;

            EntityStore store = host.Store;
            SystemRoot root = host.Systems;
            registeredWorldCount++;
            if (host.IsRunning)
                runningWorldCount++;
            totalEntityCount += store.Count;
            totalArchetypeCount += store.ArchetypeCount;

            if (worlds.Count >= maxWorlds)
                continue;

            int systemStartIndex = systems.Count;
            bool monitoringEnabled = root.MonitorPerf;
            CaptureChildren(
                root,
                parentIndex: -1,
                monitoringEnabled,
                systems,
                maxSystems,
                ref systemsTruncated);
            worlds.Add(new EcsWorldDebugEntry(
                host.GetPath().ToString(),
                host.UpdatePhase,
                host.IsRunning,
                store.Count,
                store.ArchetypeCount,
                store.CapacitySumArchetypes,
                monitoringEnabled,
                systemStartIndex,
                systems.Count - systemStartIndex));
        }

        return new EcsWorldDebugSnapshot(
            registeredWorldCount,
            runningWorldCount,
            totalEntityCount,
            totalArchetypeCount,
            registeredWorldCount > worlds.Count,
            systemsTruncated,
            worlds.ToArray(),
            systems.ToArray());
    }

    private static void CaptureChildren(
        SystemGroup group,
        int parentIndex,
        bool monitoringEnabled,
        List<EcsSystemDebugEntry> entries,
        int maxSystems,
        ref bool truncated)
    {
        foreach (BaseSystem system in group.ChildSystems)
        {
            if (entries.Count >= maxSystems)
            {
                truncated = true;
                return;
            }

            int entryIndex = entries.Count;
            bool hasEntityCount = system is QuerySystemBase;
            int entityCount = hasEntityCount ? ((QuerySystemBase)system).EntityCount : 0;
            SystemPerf perf = system.Perf;
            entries.Add(new EcsSystemDebugEntry(
                parentIndex,
                system.Name,
                system.GetType().FullName ?? system.GetType().Name,
                system.Enabled,
                hasEntityCount,
                entityCount,
                monitoringEnabled,
                perf.LastMs,
                perf.UpdateCount,
                perf.LastMemory));

            if (system is SystemGroup childGroup)
            {
                CaptureChildren(
                    childGroup,
                    entryIndex,
                    monitoringEnabled,
                    entries,
                    maxSystems,
                    ref truncated);
                if (truncated)
                    return;
            }
        }
    }
}
#endif
