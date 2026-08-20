using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;

#nullable enable

namespace GoDo.Integrations.FrifloEcs;

/// <summary>
/// 持有一个场景级 Friflo ECS World，并由指定 Godot 帧阶段驱动其 SystemRoot。
/// 节点进入场景树时创建 World，退出时停止更新并释放 SystemRoot；节点可在不同场景中独立使用。
/// </summary>
public sealed partial class EcsWorldHost : Node
{
    private EntityStore? _store;
    private SystemRoot? _systems;
    private float _elapsedTime;
    private EcsUpdatePhase _updatePhase;
    private bool _isRunning;

    /// <summary>选择 ECS 系统运行在 Process 还是 Physics Process 阶段；进入场景树后不可更改。</summary>
    [Export]
    public EcsUpdatePhase UpdatePhase
    {
        get => _updatePhase;
        set
        {
            if (IsInitialized)
                throw new InvalidOperationException("ECS 宿主初始化后不能更改更新阶段。");
            _updatePhase = value;
        }
    }

    /// <summary>控制系统更新；默认关闭，业务添加系统后显式启用；暂停时 World 仍保留，可由业务读写。</summary>
    [Export]
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            RefreshProcessing();
        }
    }

    /// <summary>当前场景级实体存储。</summary>
    /// <exception cref="InvalidOperationException">节点尚未进入场景树或已经退出。</exception>
    public EntityStore Store => _store ?? throw new InvalidOperationException("ECS World 尚未初始化或已经关闭。");

    /// <summary>当前 SystemRoot；业务应在节点进入场景树后添加系统。</summary>
    /// <exception cref="InvalidOperationException">节点尚未进入场景树或已经退出。</exception>
    public SystemRoot Systems => _systems ?? throw new InvalidOperationException("ECS SystemRoot 尚未初始化或已经关闭。");

    /// <summary>节点是否持有可用的 World 与 SystemRoot。</summary>
    public bool IsInitialized => _store != null && _systems != null;

    /// <inheritdoc />
    public override void _EnterTree()
    {
        if (IsInitialized)
            return;

        _store = new EntityStore();
        _systems = new SystemRoot(_store);
        _elapsedTime = 0f;
#if DEBUG
        EcsWorldDebugRegistry.Register(this);
#endif
    }

    /// <inheritdoc />
    public override void _Ready()
    {
        RefreshProcessing();
    }

    /// <inheritdoc />
    /// <param name="delta">Godot Process 帧间隔秒数，将以单精度传入 Friflo SystemRoot。</param>
    public override void _Process(double delta)
    {
        if (IsRunning)
            UpdateSystems(delta);
    }

    /// <inheritdoc />
    /// <param name="delta">Godot Physics Process 固定帧间隔秒数，将以单精度传入 Friflo SystemRoot。</param>
    public override void _PhysicsProcess(double delta)
    {
        if (IsRunning)
            UpdateSystems(delta);
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        Shutdown();
    }

    /// <summary>幂等关闭 SystemRoot 并使 World 不再可访问；节点退出场景树时自动调用。</summary>
    public void Shutdown()
    {
#if DEBUG
        EcsWorldDebugRegistry.Unregister(this);
#endif
        SetProcess(false);
        SetPhysicsProcess(false);
        _systems = null;
        _store = null;
        _elapsedTime = 0f;
    }

    private void UpdateSystems(double delta)
    {
        float frameDelta = (float)delta;
        _elapsedTime += frameDelta;
        _systems?.Update(new UpdateTick(frameDelta, _elapsedTime));
    }

    private void RefreshProcessing()
    {
        bool shouldProcess = IsInitialized && _isRunning;
        SetProcess(shouldProcess && UpdatePhase == EcsUpdatePhase.Process);
        SetPhysicsProcess(shouldProcess && UpdatePhase == EcsUpdatePhase.Physics);
    }
}
