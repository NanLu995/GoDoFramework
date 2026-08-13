using System;
using System.Diagnostics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>Friflo ECS 在 1 万与 10 万实体规模下的单线程查询更新基准。</summary>
public sealed partial class FrifloEcsBenchmark : Node
{
    private const int NormalEntityCount = 10_000;
    private const int MaximumEntityCount = 100_000;
    private const int NormalIterations = 1_000;
    private const int MaximumIterations = 100;
    private const int WarmUpIterations = 100;

#if DEBUG
    private const string BuildConfiguration = "Debug";
#else
    private const string BuildConfiguration = "Release";
#endif

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            WarmUpMeasurementApis();
            RunScale(NormalEntityCount, NormalIterations);
            RunScale(MaximumEntityCount, MaximumIterations);
            GD.Print(
                $"[FrifloEcsBenchmark] PASS; Build={BuildConfiguration}; " +
                $"Processors={System.Environment.ProcessorCount}; OS={OS.GetName()}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[FrifloEcsBenchmark] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static void WarmUpMeasurementApis()
    {
        for (int index = 0; index < 10; index++)
        {
            _ = GC.GetAllocatedBytesForCurrentThread();
            _ = Stopwatch.GetTimestamp();
        }
    }

    private static void RunScale(int entityCount, int iterations)
    {
        var store = new EntityStore();
        for (int index = 0; index < entityCount; index++)
        {
            store.CreateEntity(
                new Position(index, index),
                new Velocity(1f, -1f));
        }

        var root = new SystemRoot(store);
        var movement = new MovementSystem();
        root.Add(movement);

        for (int index = 0; index < WarmUpIterations; index++)
            root.Update(new UpdateTick(1f / 60f, index / 60f));

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < iterations; index++)
            root.Update(new UpdateTick(1f / 60f, index / 60f));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        long started = Stopwatch.GetTimestamp();
        for (int index = 0; index < iterations; index++)
            root.Update(new UpdateTick(1f / 60f, index / 60f));
        long finished = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started, finished);

        Assert(movement.EntityCount == entityCount, "MovementSystem 匹配实体数量不正确");
        Assert(allocated == 0, $"稳态查询更新产生托管分配: {allocated} bytes");
        GD.Print(
            $"[FrifloEcsBenchmark] Update: Entities={entityCount}; Iterations={iterations}; " +
            $"ElapsedMs={elapsed.TotalMilliseconds:F3}; " +
            $"AverageMs={elapsed.TotalMilliseconds / iterations:F6}; " +
            $"AllocatedBytes={allocated}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class MovementSystem : QuerySystem<Position, Velocity>
    {
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
        }
    }

    private struct Position : IComponent
    {
        public Position(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X;
        public float Y;
    }

    private readonly struct Velocity : IComponent
    {
        public Velocity(float x, float y)
        {
            X = x;
            Y = y;
        }

        public readonly float X;
        public readonly float Y;
    }
}
