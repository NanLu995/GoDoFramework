using Godot;
using GoDo;
using PhantomCamera;

#nullable enable

namespace Demo3D;

/// <summary>Demo3D 的第三人称角色移动和鼠标视角控制。</summary>
public sealed partial class PlayerController : CharacterBody3D
{
    [Export] public NodePath PhantomCameraPath { get; set; } = null!;
    [Export] public float MoveSpeed { get; set; } = 6f;
    [Export] public float JumpVelocity { get; set; } = 5f;
    [Export] public float MouseSensitivity { get; set; } = 0.05f;

    private PhantomCamera3D _phantomCamera = null!;
    private IInputService _input = null!;
    private Vector2 _movementInput;
    private bool _jumpRequested;
    private bool _wasGameplayContextActive;
    private float _gravity;

    public override void _Ready()
    {
        Node3D? cameraNode = GetNodeOrNull<Node3D>(PhantomCameraPath);
        if (!GodotObject.IsInstanceValid(cameraNode))
            throw new System.InvalidOperationException("PlayerController 缺少 PhantomCamera3D 节点引用。");

        _phantomCamera = cameraNode!.AsPhantomCamera3D();
        _input = Services.Get<IInputService>();
        if (!_input.IsReady)
            throw new System.InvalidOperationException("PlayerController 启动时 InputService 尚未就绪。");

        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _wasGameplayContextActive = _input.IsContextActive(Demo3DInput.Gameplay);
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Process(double delta)
    {
        bool gameplayContextActive = _input.IsContextActive(Demo3DInput.Gameplay);
        if (!gameplayContextActive)
        {
            _movementInput = Vector2.Zero;
            _jumpRequested = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            _wasGameplayContextActive = false;
            return;
        }

        if (!_wasGameplayContextActive)
            Input.MouseMode = Input.MouseModeEnum.Captured;
        _wasGameplayContextActive = true;

        InputFrame frame = _input.Frame;
        _movementInput = frame.Axis2(Demo3DInput.Move).LimitLength(1f);
        _jumpRequested |= frame.JustPressed(Demo3DInput.Jump);

        if (frame.JustPressed(Demo3DInput.ReleasePointer))
            Input.MouseMode = Input.MouseModeEnum.Visible;

        if (Input.MouseMode != Input.MouseModeEnum.Captured)
            return;

        Vector2 lookInput = frame.Axis2(Demo3DInput.Look) * (MouseSensitivity / 0.05f);
        Vector3 rotation = _phantomCamera.GetThirdPersonRotationDegrees();
        rotation.X = Mathf.Clamp(rotation.X + lookInput.Y, -50f, 25f);
        rotation.Y = Mathf.Wrap(rotation.Y + lookInput.X, 0f, 360f);
        _phantomCamera.SetThirdPersonRotationDegrees(rotation);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsOnFloor())
            Velocity = new Vector3(Velocity.X, Velocity.Y - _gravity * (float)delta, Velocity.Z);

        if (_jumpRequested && IsOnFloor())
            Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
        _jumpRequested = false;

        Basis cameraBasis = _phantomCamera!.Node3D.GlobalTransform.Basis;
        Vector3 forward = -cameraBasis.Z;
        Vector3 right = cameraBasis.X;
        forward.Y = 0f;
        right.Y = 0f;
        Vector3 direction = (right.Normalized() * _movementInput.X) + (forward.Normalized() * -_movementInput.Y);

        Velocity = new Vector3(direction.X * MoveSpeed, Velocity.Y, direction.Z * MoveSpeed);
        MoveAndSlide();
    }
}
