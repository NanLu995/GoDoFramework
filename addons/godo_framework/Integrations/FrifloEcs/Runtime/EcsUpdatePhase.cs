namespace GoDo.Integrations.FrifloEcs;

/// <summary>指定 ECS 系统组由 Godot 的哪个帧阶段驱动。</summary>
public enum EcsUpdatePhase
{
    /// <summary>由 Godot 的 Process 帧驱动，适合非物理模拟和表现前的数据更新。</summary>
    Process,

    /// <summary>由 Godot 的 Physics Process 帧驱动，适合固定步长模拟。</summary>
    Physics,
}
