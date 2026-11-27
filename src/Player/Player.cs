using Gecko.Core;
using Gecko.Economy;
using Gecko.UI;
using Gecko.Vehicles;
using Godot;

namespace Gecko.Actors;

/// <summary>
/// Third-person player: camera-relative movement, sprint, jump, and an
/// over-the-shoulder camera driven by mouse or right stick.
/// </summary>
public partial class Player : CharacterBody3D
{
    public const string Group = "player";

    /// <summary>This player's money (wallet cash + bank).</summary>
    public Wallet Wallet { get; } = new(Money.StartingDirty);

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
    private CollisionShape3D _collision = null!;
    private Camera3D _camera = null!;

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

    /// <summary>
    /// When set, the camera turns to frame this world position and follows it as it moves
    /// (target lock-on). Manual camera input is ignored meanwhile.
    /// </summary>
    public Vector3? LookAtTarget { get; set; }

    [ExportGroup("Lock-on")]
    [Export] public float LockOnTurnSpeed { get; set; } = 8f;
    [Export] public float LockOnMinPitchDegrees { get; set; } = -35f;
    [Export] public float LockOnMaxPitchDegrees { get; set; } = 15f;

    /// <summary>The vehicle being driven, or null on foot.</summary>
    public VehicleNode? Vehicle { get; private set; }

    public void EnterVehicle(VehicleNode vehicle, bool viaPassengerSide = false)
    {
        Vehicle = vehicle;
        Velocity = Vector3.Zero;
        Visible = false;
        _collision.Disabled = true;
        LookAtTarget = null;
        vehicle.SetDriver(this, viaPassengerSide);
    }

    public void ExitVehicle()
    {
        if (Vehicle is not { } vehicle)
            return;
        Vehicle = null;
        vehicle.LookAtTarget = null;
        vehicle.SetDriver(null);
        GlobalPosition = FindExitSpot(vehicle);
        Velocity = Vector3.Zero;
        Visible = true;
        _collision.Disabled = false;
        // Keep looking the way the car camera was.
        _yaw.Rotation = _yaw.Rotation with { Y = vehicle.GlobalRotation.Y + Mathf.Pi };
        _camera.MakeCurrent();
    }

    /// <summary>Driver's door if it's clear, else the other side, else behind the car.</summary>
    private Vector3 FindExitSpot(VehicleNode vehicle)
    {
        Vector3[] candidates =
        [
            vehicle.DriverDoorPosition,
            vehicle.GlobalTransform * new Vector3(-1.55f, 0f, 0.3f),
            vehicle.GlobalTransform * new Vector3(0f, 0f, -3.2f),
            vehicle.GlobalTransform * new Vector3(0f, 0f, 3.2f),
        ];
        foreach (var spot in candidates)
        {
            if (IsSpotClear(vehicle, spot))
                return new Vector3(spot.X, vehicle.GlobalPosition.Y + 0.1f, spot.Z);
        }
        return vehicle.GlobalPosition + Vector3.Up * 2.2f; // last resort: on the roof
    }

    /// <summary>True if a person could stand at this spot beside the vehicle (nothing solid in the way).</summary>
    public bool IsSpotClear(VehicleNode vehicle, Vector3 spot)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = _collision.Shape,
            Exclude = [vehicle.GetRid(), GetRid()],
            Transform = new Transform3D(Basis.Identity, new Vector3(spot.X, vehicle.GlobalPosition.Y + 1.05f, spot.Z)),
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
    }

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
        _collision = GetNode<CollisionShape3D>("CollisionShape3D");
        _camera = GetNode<Camera3D>("CameraYaw/CameraPitch/SpringArm3D/Camera3D");

        AddToGroup(Group);
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
        _springArm.AddExcludedObject(GetRid());
        _pitch.Rotation = _pitch.Rotation with { X = Mathf.DegToRad(StartPitchDegrees) };
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured && LookAtTarget == null)
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

        // Riding along: the vehicle does the driving.
        if (Vehicle != null)
        {
            GlobalPosition = Vehicle.GlobalPosition;
            return;
        }

        // Right-stick camera
        if (LookAtTarget is { } lookAt)
        {
            UpdateLockOn(lookAt, dt);
        }
        else
        {
            Vector2 look = Input.GetVector(Actions.LookLeft, Actions.LookRight, Actions.LookUp, Actions.LookDown);
            if (look != Vector2.Zero)
                RotateCamera(-look.X * StickSensitivity * dt, -look.Y * StickSensitivity * dt);
        }

        Vector3 velocity = Velocity;

        // Gravity and jumping
        if (!IsOnFloor())
            velocity.Y -= _gravity * dt;
        // Cross is "place" in build mode and option 4 while targeting.
        else if (Input.IsActionJustPressed(Actions.Jump) && !IsLocked && !_buildMode.Active && !_interaction.IsTargeting
                 && !_interaction.Carry.IsEncumbered
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
        // Three duffel bags weigh you down: no sprinting or jumping (and no shooting, once weapons exist).
        bool sprinting = !IsLocked && !_interaction.Carry.IsEncumbered
                         && (Input.IsActionPressed(Actions.Sprint) || _sprintToggled);
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

    /// <summary>Smoothly swings the camera to frame a target (it keeps following if the target moves).</summary>
    private void UpdateLockOn(Vector3 target, float dt)
    {
        var toTarget = target - _pitch.GlobalPosition;
        var flat = new Vector2(toTarget.X, toTarget.Z);
        if (flat.LengthSquared() < 0.01f)
            return;
        float desiredYaw = Mathf.Atan2(-toTarget.X, -toTarget.Z);
        float desiredPitch = Mathf.Clamp(
            Mathf.Atan2(toTarget.Y, flat.Length()),
            Mathf.DegToRad(LockOnMinPitchDegrees),
            Mathf.DegToRad(LockOnMaxPitchDegrees));
        float blend = 1f - Mathf.Exp(-LockOnTurnSpeed * dt);
        _yaw.Rotation = _yaw.Rotation with { Y = Mathf.LerpAngle(_yaw.Rotation.Y, desiredYaw, blend) };
        _pitch.Rotation = _pitch.Rotation with { X = Mathf.Lerp(_pitch.Rotation.X, desiredPitch, blend) };
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
