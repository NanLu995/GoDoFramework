using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using GoDo.Integrations.FrifloEcs;

#nullable enable

namespace Demo3D;

/// <summary>使用 Friflo ECS 与单个 MultiMesh 展示场景级群体模拟。</summary>
public sealed partial class EcsSwarmDemo : Node3D
{
    /// <summary>演示 World 中保持活动的实体数量。</summary>
    public const int SwarmEntityCount = 512;

    private const float HalfExtent = 3.5f;
    private const float MinimumHeight = 0.35f;
    private const float MaximumHeight = 3.2f;

    private readonly Entity[] _entities = new Entity[SwarmEntityCount];
    private EcsWorldHost? _host;
    private SwarmMovementSystem? _movementSystem;
    private MultiMeshInstance3D? _visuals;
    private Label3D? _statusLabel;
    private bool _lastPaused;
    private int _visualSyncCount;

    /// <summary>当前场景级 ECS World，节点进入树并完成初始化后可用。</summary>
    public EntityStore Store => RequireHost().Store;

    /// <summary>已经同步到 MultiMesh 的帧数，用于验证 ECS 到 Godot 的主线程边界。</summary>
    public int VisualSyncCount => _visualSyncCount;

    /// <summary>当前宿主是否正在驱动 ECS System。</summary>
    public bool IsSimulationRunning => _host?.IsRunning == true;

    /// <summary>ECS 移动 System 已执行的更新次数。</summary>
    public int SimulationUpdateCount => _movementSystem?.UpdateCount ?? 0;

    /// <inheritdoc />
    public override void _Ready()
    {
        _host = GetNode<EcsWorldHost>("EcsWorldHost");
        _visuals = GetNode<MultiMeshInstance3D>("SwarmVisuals");
        _statusLabel = GetNode<Label3D>("StatusLabel");

        Mesh mesh = _visuals.Multimesh?.Mesh ??
            throw new InvalidOperationException("EcsSwarmDemo 缺少 MultiMesh 实例 Mesh。");
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = mesh,
            InstanceCount = SwarmEntityCount,
        };
        _visuals.Multimesh = multiMesh;

        _movementSystem = new SwarmMovementSystem(
            HalfExtent,
            MinimumHeight,
            MaximumHeight);
        _host.Systems.Add(_movementSystem);
        CreateEntities(multiMesh);
        _host.IsRunning = true;

        _lastPaused = GetTree().Paused;
        RefreshStatus(_lastPaused);
        SyncVisuals();
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        bool paused = GetTree().Paused;
        if (paused != _lastPaused)
        {
            _lastPaused = paused;
            RefreshStatus(paused);
        }

        if (!paused)
            SyncVisuals();
    }

    private void CreateEntities(MultiMesh multiMesh)
    {
        const int side = 8;
        for (int index = 0; index < SwarmEntityCount; index++)
        {
            int xIndex = index % side;
            int zIndex = (index / side) % side;
            int yIndex = index / (side * side);
            float x = Mathf.Lerp(-HalfExtent, HalfExtent, xIndex / (float)(side - 1));
            float z = Mathf.Lerp(-HalfExtent, HalfExtent, zIndex / (float)(side - 1));
            float y = Mathf.Lerp(MinimumHeight, MaximumHeight, yIndex / (float)(side - 1));

            float angle = index * 2.3999632f;
            float vertical = ((index % 7) - 3) * 0.055f;
            var position = new SwarmPosition(x, y, z);
            var velocity = new SwarmVelocity(
                Mathf.Cos(angle) * 1.25f,
                vertical,
                Mathf.Sin(angle) * 1.25f);
            _entities[index] = RequireHost().Store.CreateEntity(position, velocity);

            float hue = (index % 32) / 32f;
            multiMesh.SetInstanceColor(index, Color.FromHsv(0.48f + (hue * 0.12f), 0.7f, 1f));
        }
    }

    private void SyncVisuals()
    {
        MultiMesh multiMesh = _visuals?.Multimesh ??
            throw new InvalidOperationException("EcsSwarmDemo MultiMesh 尚未初始化。");
        for (int index = 0; index < _entities.Length; index++)
        {
            ref SwarmPosition position = ref _entities[index].GetComponent<SwarmPosition>();
            multiMesh.SetInstanceTransform(
                index,
                new Transform3D(Basis.Identity, new Vector3(position.X, position.Y, position.Z)));
        }

        _visualSyncCount++;
    }

    private void RefreshStatus(bool paused)
    {
        _statusLabel!.Text = paused
            ? $"Friflo ECS · {SwarmEntityCount} Entities\nProcess: Paused"
            : $"Friflo ECS · {SwarmEntityCount} Entities\nProcess: Running";
    }

    private EcsWorldHost RequireHost() =>
        _host ?? throw new InvalidOperationException("EcsSwarmDemo 尚未完成初始化。");

    private sealed class SwarmMovementSystem : QuerySystem<SwarmPosition, SwarmVelocity>
    {
        private readonly ForEachEntity<SwarmPosition, SwarmVelocity> _moveEntity;
        private readonly float _halfExtent;
        private readonly float _minimumHeight;
        private readonly float _maximumHeight;

        public SwarmMovementSystem(float halfExtent, float minimumHeight, float maximumHeight)
        {
            _halfExtent = halfExtent;
            _minimumHeight = minimumHeight;
            _maximumHeight = maximumHeight;
            _moveEntity = MoveEntity;
        }

        public int UpdateCount { get; private set; }

        protected override void OnUpdate()
        {
            UpdateCount++;
            Query.ForEachEntity(_moveEntity);
        }

        private void MoveEntity(
            ref SwarmPosition position,
            ref SwarmVelocity velocity,
            Entity entity)
        {
            float delta = Tick.deltaTime;
            position.X += velocity.X * delta;
            position.Y += velocity.Y * delta;
            position.Z += velocity.Z * delta;

            Reflect(ref position.X, ref velocity.X, -_halfExtent, _halfExtent);
            Reflect(ref position.Y, ref velocity.Y, _minimumHeight, _maximumHeight);
            Reflect(ref position.Z, ref velocity.Z, -_halfExtent, _halfExtent);
        }

        private static void Reflect(ref float position, ref float velocity, float minimum, float maximum)
        {
            if (position < minimum)
            {
                position = minimum;
                velocity = MathF.Abs(velocity);
            }
            else if (position > maximum)
            {
                position = maximum;
                velocity = -MathF.Abs(velocity);
            }
        }
    }

    private struct SwarmPosition : IComponent
    {
        public SwarmPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X;
        public float Y;
        public float Z;
    }

    private struct SwarmVelocity : IComponent
    {
        public SwarmVelocity(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X;
        public float Y;
        public float Z;
    }
}
