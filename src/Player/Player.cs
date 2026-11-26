using Gecko.Core;
using Godot;

namespace Gecko.Actors;

/// <summary>
/// Third-person player: camera-relative movement, sprint, jump, and an
/// over-the-shoulder camera driven by mouse or right stick.
/// </summary>
public partial class Player : CharacterBody3D
{
    [ExportGroup("Movement")]
    [Export] public float WalkSpeed { get; set; } = 4f;
    [Export] public float SprintSpeed { get; set; } = 7.5f;
    /// <summary>m/s², how quickly you reach target speed.</summary>
    [Export] public float Acceleration { get; set; } = 30f;
    /// <summary>How quickly the body turns to face movement.</summary>
    [Export] public float TurnSpeed { get; set; } = 12f;
    [Export] public float JumpVelocity { get; set; } = 4.5f;

    [ExportGroup("Camera")]
    /// <summary>Radians per pixel.</summary>
    [Export] public float MouseSensitivity { get; set; } = 0.0025f;
    /// <summary>Radians per second at full stick tilt.</summary>
    [Export] public float StickSensitivity { get; set; } = 3f;
    [Export] public float MinPitchDegrees { get; set; } = -70f;
    [Export] public float MaxPitchDegrees { get; set; } = 50f;
    [Export] public float StartPitchDegrees { get; set; } = -15f;

    private Node3D _model = null!;
    private Node3D _yaw = null!;
    private Node3D _pitch = null!;
    private SpringArm3D _springArm = null!;

    private float _gravity;
    private bool _sprintToggled;
    private bool _wasOnFloor = true;

    public override void _Ready()
    {
        _model = GetNode<Node3D>("Model");
        _yaw = GetNode<Node3D>("CameraYaw");
        _pitch = GetNode<Node3D>("CameraYaw/CameraPitch");
        _springArm = GetNode<SpringArm3D>("CameraYaw/CameraPitch/SpringArm3D");

        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _springArm.AddExcludedObject(GetRid());
        _pitch.Rotation = _pitch.Rotation with { X = Mathf.DegToRad(StartPitchDegrees) };
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateCamera(-motion.Relative.X * MouseSensitivity, -motion.Relative.Y * MouseSensitivity);
        }
        else if (@event.IsActionPressed(Actions.Pause))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
        else if (@event is InputEventMouseButton { Pressed: true } && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Right-stick camera
        Vector2 look = Input.GetVector(Actions.LookLeft, Actions.LookRight, Actions.LookUp, Actions.LookDown);
        if (look != Vector2.Zero)
            RotateCamera(-look.X * StickSensitivity * dt, -look.Y * StickSensitivity * dt);

        Vector3 velocity = Velocity;

        // Gravity and jumping
        if (!IsOnFloor())
            velocity.Y -= _gravity * dt;
        else if (Input.IsActionJustPressed(Actions.Jump))
            velocity.Y = JumpVelocity;

        // Movement relative to where the camera faces. Analog sticks keep their partial tilt.
        Vector2 moveInput = Input.GetVector(Actions.MoveLeft, Actions.MoveRight, Actions.MoveForward, Actions.MoveBack);
        var cameraYaw = new Basis(Vector3.Up, _yaw.GlobalRotation.Y);
        Vector3 direction = cameraYaw * new Vector3(moveInput.X, 0f, moveInput.Y);

        if (Input.IsActionJustPressed(Actions.SprintToggle))
            _sprintToggled = !_sprintToggled;
        if (moveInput.Length() < 0.1f)
            _sprintToggled = false;
        bool sprinting = Input.IsActionPressed(Actions.Sprint) || _sprintToggled;
        float speed = sprinting ? SprintSpeed : WalkSpeed;

        var horizontal = new Vector3(velocity.X, 0f, velocity.Z)
            .MoveToward(direction * speed, Acceleration * dt);
        velocity.X = horizontal.X;
        velocity.Z = horizontal.Z;

        // Turn the body (not the camera) to face the movement direction.
        if (direction.Length() > 0.1f)
        {
            float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
            _model.Rotation = _model.Rotation with
            {
                Y = Mathf.LerpAngle(_model.Rotation.Y, targetYaw, TurnSpeed * dt)
            };
        }

        float fallSpeed = -velocity.Y;
        Velocity = velocity;
        MoveAndSlide();

        // Small rumble on a hard landing.
        bool onFloor = IsOnFloor();
        if (onFloor && !_wasOnFloor && fallSpeed > 6f)
            Gamepad.Instance.Rumble(0.3f, 0.6f, 0.15f);
        _wasOnFloor = onFloor;
    }

    private void RotateCamera(float yawDelta, float pitchDelta)
    {
        _yaw.RotateY(yawDelta);
        _pitch.Rotation = _pitch.Rotation with
        {
            X = Mathf.Clamp(
                _pitch.Rotation.X + pitchDelta,
                Mathf.DegToRad(MinPitchDegrees),
                Mathf.DegToRad(MaxPitchDegrees))
        };
    }
}
