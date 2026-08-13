using System;
using System.Diagnostics;
using Demo3D;
using Godot;
using GoDo.Integrations.FrifloEcs;

#nullable enable

namespace GoDoFramework.Verification;

/// <summary>测量 Demo3D 512 实体 ECS 更新与 MultiMesh 可视同步的稳态成本。</summary>
public sealed partial class Demo3DEcsSwarmBenchmark : Node
{
    private const int WarmUpIterations = 200;
    private const int MeasuredIterations = 10_000;
    private const double Delta = 1d / 60d;

    /// <inheritdoc />
    public override void _Ready()
    {
        try
        {
            EcsSwarmDemo demo = CreateDemo();
            EcsWorldHost host = demo.GetNode<EcsWorldHost>("EcsWorldHost");
            demo.SetProcess(false);
            host.SetProcess(false);

            for (int index = 0; index < WarmUpIterations; index++)
            {
                host._Process(Delta);
                demo._Process(Delta);
            }

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int index = 0; index < MeasuredIterations; index++)
            {
                host._Process(Delta);
                demo._Process(Delta);
            }
            long finished = Stopwatch.GetTimestamp();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started, finished);

            Assert(demo.Store.Count == EcsSwarmDemo.SwarmEntityCount,
                "Demo3D ECS 群体实体数量不正确");
            Assert(allocated == 0,
                $"Demo3D ECS 更新与 MultiMesh 同步产生托管分配: {allocated} bytes");
            GD.Print(
                $"[Demo3DEcsSwarmBenchmark] PASS; Entities={EcsSwarmDemo.SwarmEntityCount}; " +
                $"Iterations={MeasuredIterations}; ElapsedMs={elapsed.TotalMilliseconds:F3}; " +
                $"AverageMs={elapsed.TotalMilliseconds / MeasuredIterations:F6}; " +
                $"AllocatedBytes={allocated}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"[Demo3DEcsSwarmBenchmark] FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private EcsSwarmDemo CreateDemo()
    {
        var demo = new EcsSwarmDemo { Name = "EcsSwarmDemo" };
        demo.AddChild(new EcsWorldHost
        {
            Name = "EcsWorldHost",
            ProcessPriority = -10,
        });

        var visuals = new MultiMeshInstance3D
        {
            Name = "SwarmVisuals",
            Multimesh = new MultiMesh { Mesh = new SphereMesh() },
        };
        demo.AddChild(visuals);
        demo.AddChild(new Label3D { Name = "StatusLabel" });
        AddChild(demo);
        return demo;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
