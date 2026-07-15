using Godot;
using GoDo;

namespace Demo3D;

/// <summary>触碰后上报一次收集事件的关卡物件。</summary>
public sealed partial class Collectible : Area3D
{
    [Export] public float RotationSpeed { get; set; } = 2f;

    private bool _isCollected;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public override void _ExitTree()
    {
        BodyEntered -= OnBodyEntered;
    }

    public override void _Process(double delta)
    {
        RotateY(RotationSpeed * (float)delta);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_isCollected || body is not PlayerController)
            return;

        _isCollected = true;
        EventChannel.Emit<CollectibleCollectedEvent>();
        QueueFree();
    }
}
