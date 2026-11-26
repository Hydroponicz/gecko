using Gecko.Core;
using Gecko.UI;
using Godot;

namespace Gecko.Actors;

/// <summary>
/// Third-person player: camera-relative movement, sprint, jump, and an
/// over-the-shoulder camera driven by mouse or right stick.
/// </summary>
public partial class Player : CharacterBody3D
{
    public const string Group = "player";

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
    private BuildMode _buildMode = null!;
    private PlayerInteraction _interaction = null!;

    private float _gravity;
    private bool _sprintToggled;
    private bool _wasOnFloor = true;

    /// <summary>
    /// While locked the player ignores movement input (camera stays free). Used for interaction
    /// "animations": the character walks to <see cref="ScriptedMoveTarget"/> and performs an action.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>World position to walk to while locked, or null to stand still.</summary>
    public Vector3? ScriptedMoveTarget { get; set; }

    /// <summary>Turns the body toward a world position (used while performing an action).</summary>
    public void FaceTowards(Vector3 worldPosition, float delta)
    {
        var offset = worldPosition - GlobalPosition;
        if (new Vector2(offset.X, offset.Z).LengthSquared() < 0.0001f)
            return;
        float targetYaw = Mathf.Atan2(-offset.X, -offset.Z);
        _model.Rotation = _model.Rotation with { Y = Mathf.LerpAngle(_model.Rotation.Y, targetYaw, TurnSpeed * delta) };
    }

    /// <summary>Placeholder action pose until real animations exist: leans the body forward (0..1).</summary>
    public void SetLean(float amount) =>
        _model.Rotation = _model.Rotation with { X = -0.45f * Mathf.Clamp(amount, 0f, 1f) };

    public override void _Ready()
    {
        _model = GetNode<Node3D>("Model");
        _yaw = GetNode<Node3D>("CameraYaw");
        _pitch = GetNode<Node3D>("CameraYaw/CameraPitch");
        _springArm = GetNode<SpringArm3D>("CameraYaw/CameraPitch/SpringArm3D");
        _buildMode = GetNode<BuildMode>("BuildMode");
        _interaction = GetNode<PlayerInteraction>("PlayerInteraction");

        AddToGroup(Group);
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
        // Cross is "place" in build mode and option 4 while targeting.
        else if (Input.IsActionJustPressed(Actions.Jump) && !IsLocked && !_buildMode.Active && !_interaction.IsTargeting
                 && !DebugMenu.IsOpen)
            velocity.Y = JumpVelocity;

        // Movement relative to where the camera faces. Analog sticks keep their partial tilt.
        Vector2 moveInput = Input.GetVector(Actions.MoveLeft, Actions.MoveRight, Actions.MoveForward, Actions.MoveBack);
        var cameraYaw = new Basis(Vector3.Up, _yaw.GlobalRotation.Y);
        Vector3 direction = cameraYaw * new Vector3(moveInput.X, 0f, moveInput.Y);
        if (IsLocked)
        {
            moveInput = Vector2.Zero;
            direction = Vector3.Zero;
            if (ScriptedMoveTarget is { } target)
            {
                var toTarget = target - GlobalPosition;
                toTarget.Y = 0f;
                // Full speed until close, then ease in so we stop on the spot.
                direction = toTarget.Length() > 0.05f ? toTarget.Normalized() * Mathf.Min(1f, toTarget.Length() * 3f) : Vector3.Zero;
            }
        }

        if (Input.IsActionJustPressed(Actions.SprintToggle))
            _sprintToggled = !_sprintToggled;
        if (moveInput.Length() < 0.1f)
            _sprintToggled = false;
        bool sprinting = !IsLocked && (Input.IsActionPressed(Actions.Sprint) || _sprintToggled);
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
