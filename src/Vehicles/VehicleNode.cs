using Gecko.Core;
using Godot;

namespace Gecko.Vehicles;

/// <summary>
/// A drivable car (gray-box sedan) with arcade handling, a chase camera and a trunk.
/// The car's front is its local +Z (Godot's vehicle convention).
/// Handling numbers are exported so they can be tuned in the editor.
/// </summary>
public partial class VehicleNode : VehicleBody3D
{
    public const string Group = "vehicles";

    [ExportGroup("Vehicle")]
    [Export] public string DisplayName { get; set; } = "Sedan";
    [Export] public Color PaintColor { get; set; } = new(0.45f, 0.12f, 0.1f); // faded maroon
    [Export] public int TrunkCapacity { get; set; } = 20;

    [ExportGroup("Handling")]
    [Export] public float EnginePower { get; set; } = 160f;
    [Export] public float ReversePower { get; set; } = 80f;
    [Export] public float BrakePower { get; set; } = 30f;
    [Export] public float HandbrakePower { get; set; } = 25f;
    [Export] public float TopSpeed { get; set; } = 32f;        // m/s (~115 km/h)
    [Export] public float TopReverseSpeed { get; set; } = 8f;  // m/s
    [Export] public float MaxSteerDegrees { get; set; } = 32f;
    [Export] public float HighSpeedSteerFactor { get; set; } = 0.35f; // less steering at top speed
    [Export] public float SteerSpeed { get; set; } = 4f;
    [Export] public float Grip { get; set; } = 6f;
    [Export] public float HandbrakeRearGrip { get; set; } = 1.6f; // rear slides out: handbrake turns
    [Export] public float Downforce { get; set; } = 3f;

    [ExportGroup("Camera")]
    [Export] public float CameraDistance { get; set; } = 6.5f;
    [Export] public float CameraHeight { get; set; } = 1.4f;
    [Export] public float CameraRecenterDelay { get; set; } = 1.5f;

    private readonly VehicleWheel3D[] _wheels = new VehicleWheel3D[4]; // FL, FR, RL, RR
    private Node3D _cameraYaw = null!;
    private Node3D _cameraPitch = null!;
    private Camera3D _camera = null!;
    private float _sinceManualLook = 99f;
    private float _upsideDownTimer;

    public CargoState Cargo { get; private set; } = null!;

    /// <summary>The player driving, or null when parked.</summary>
    public Node3D? Driver { get; private set; }

    /// <summary>When set (targeting from the car), the camera turns to frame this point and follows it.</summary>
    public Vector3? LookAtTarget { get; set; }

    /// <summary>Forward speed in m/s (negative when reversing).</summary>
    public float ForwardSpeed => LinearVelocity.Dot(GlobalBasis.Z);

    /// <summary>Where the driver gets out (left side, by the driver's door).</summary>
    public Vector3 DriverDoorPosition => GlobalTransform * new Vector3(1.55f, 0f, 0.3f);

    /// <summary>Behind the car, where you stand to use the trunk.</summary>
    public Vector3 TrunkPosition => GlobalTransform * new Vector3(0f, 0.9f, -2.3f);

    public override void _Ready()
    {
        AddToGroup(Group);
        Mass = 1200f;
        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = new Vector3(0f, 0.35f, 0f); // low: hard to roll over
        Cargo = new CargoState(TrunkCapacity);

        BuildBody();
        BuildWheels();
        BuildCamera();
        AddChild(new TrunkView(this));
    }

    // ---- Driver ----

    public void SetDriver(Node3D? driver)
    {
        Driver = driver;
        if (driver != null)
        {
            _cameraYaw.GlobalRotation = new Vector3(0f, GlobalRotation.Y + Mathf.Pi, 0f);
            _camera.MakeCurrent();
        }
        else
        {
            EngineForce = 0f;
            Steering = 0f;
            foreach (var wheel in _wheels)
                wheel.Brake = BrakePower * 0.5f; // parked
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (Driver != null)
            Drive(dt);
        ApplyDownforce();
        FlipBackIfUpsideDown(dt);
    }

    private void Drive(float dt)
    {
        float throttle = Input.GetActionStrength(Actions.VehicleAccelerate);
        float brake = Input.GetActionStrength(Actions.VehicleBrake);
        float steerInput = Input.GetAxis(Actions.VehicleSteerRight, Actions.VehicleSteerLeft); // + = left
        bool handbrake = Input.IsActionPressed(Actions.VehicleHandbrake);
        float speed = ForwardSpeed;

        float engine = 0f;
        float brakeForce = 0f;
        if (throttle > 0.05f)
        {
            if (speed < -1f)
                brakeForce = BrakePower * throttle; // rolling backwards: brake first
            else if (speed < TopSpeed)
                engine = EnginePower * throttle;
        }
        if (brake > 0.05f)
        {
            if (speed > 1f)
                brakeForce = Mathf.Max(brakeForce, BrakePower * brake);
            else if (speed > -TopReverseSpeed)
                engine = -ReversePower * brake; // stopped: brake becomes reverse
        }
        EngineForce = engine;

        // Less steering at speed, so the car stays controllable.
        float speedFactor = Mathf.Lerp(1f, HighSpeedSteerFactor, Mathf.Clamp(Mathf.Abs(speed) / TopSpeed, 0f, 1f));
        float targetSteer = steerInput * Mathf.DegToRad(MaxSteerDegrees) * speedFactor;
        Steering = Mathf.MoveToward(Steering, targetSteer, SteerSpeed * dt);

        for (int i = 0; i < 4; i++)
        {
            bool rear = i >= 2;
            _wheels[i].Brake = brakeForce + (handbrake && rear ? HandbrakePower : 0f);
            _wheels[i].WheelFrictionSlip = handbrake && rear ? HandbrakeRearGrip : Grip;
        }
    }

    private void ApplyDownforce()
    {
        float speed = LinearVelocity.Length();
        ApplyCentralForce(-GlobalBasis.Y * speed * speed * Downforce);
    }

    /// <summary>Fail-safe: a car stuck on its side or roof for 3 seconds is set back on its wheels.</summary>
    private void FlipBackIfUpsideDown(float dt)
    {
        bool upsideDown = GlobalBasis.Y.Dot(Vector3.Up) < 0.3f && LinearVelocity.Length() < 2f;
        _upsideDownTimer = upsideDown ? _upsideDownTimer + dt : 0f;
        if (_upsideDownTimer < 3f)
            return;
        _upsideDownTimer = 0f;
        var yaw = GlobalRotation.Y;
        GlobalTransform = new Transform3D(new Basis(Vector3.Up, yaw), GlobalPosition + Vector3.Up * 1.5f);
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
    }

    // ---- Camera ----

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _cameraYaw.GlobalPosition = GlobalPosition + Vector3.Up * CameraHeight;
        if (Driver == null)
            return;

        if (LookAtTarget is { } target)
        {
            var to = target - _cameraYaw.GlobalPosition;
            float wantYaw = Mathf.Atan2(-to.X, -to.Z);
            float blend = 1f - Mathf.Exp(-6f * dt);
            _cameraYaw.Rotation = _cameraYaw.Rotation with { Y = Mathf.LerpAngle(_cameraYaw.Rotation.Y, wantYaw, blend) };
            _sinceManualLook = 0f;
            return;
        }

        var look = Input.GetVector(Actions.LookLeft, Actions.LookRight, Actions.LookUp, Actions.LookDown);
        if (look != Vector2.Zero)
        {
            _sinceManualLook = 0f;
            _cameraYaw.RotateY(-look.X * 2.5f * dt);
            _cameraPitch.Rotation = _cameraPitch.Rotation with
            {
                X = Mathf.Clamp(_cameraPitch.Rotation.X - look.Y * 2f * dt, Mathf.DegToRad(-40f), Mathf.DegToRad(15f)),
            };
        }
        else
        {
            _sinceManualLook += dt;
        }

        // After a moment without camera input, swing back behind the car while moving.
        if (_sinceManualLook > CameraRecenterDelay && Mathf.Abs(ForwardSpeed) > 2f)
        {
            float behindYaw = GlobalRotation.Y + (ForwardSpeed >= 0f ? Mathf.Pi : 0f);
            float blend = 1f - Mathf.Exp(-3f * dt);
            _cameraYaw.Rotation = _cameraYaw.Rotation with { Y = Mathf.LerpAngle(_cameraYaw.Rotation.Y, behindYaw, blend) };
            _cameraPitch.Rotation = _cameraPitch.Rotation with { X = Mathf.Lerp(_cameraPitch.Rotation.X, Mathf.DegToRad(-12f), blend) };
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Driver == null || LookAtTarget != null || @event is not InputEventMouseMotion motion
            || Input.MouseMode != Input.MouseModeEnum.Captured)
            return;
        _sinceManualLook = 0f;
        _cameraYaw.RotateY(-motion.Relative.X * 0.0025f);
        _cameraPitch.Rotation = _cameraPitch.Rotation with
        {
            X = Mathf.Clamp(_cameraPitch.Rotation.X - motion.Relative.Y * 0.0025f, Mathf.DegToRad(-40f), Mathf.DegToRad(15f)),
        };
    }

    // ---- Building the gray box ----

    private void BuildBody()
    {
        var paint = new StandardMaterial3D { AlbedoColor = PaintColor, Roughness = 0.4f };
        var glass = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.2f, 0.25f), Roughness = 0.1f };
        AddChild(new MeshInstance3D // lower body
        {
            Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.6f, 4.5f) },
            MaterialOverride = paint,
            Position = new Vector3(0f, 0.75f, 0f),
        });
        AddChild(new MeshInstance3D // cabin
        {
            Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.55f, 2.2f) },
            MaterialOverride = glass,
            Position = new Vector3(0f, 1.32f, -0.15f),
        });
        AddChild(new MeshInstance3D // headlights, so you can tell the front
        {
            Mesh = new BoxMesh { Size = new Vector3(1.4f, 0.12f, 0.05f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.95f, 0.7f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.95f, 0.7f),
                EmissionEnergyMultiplier = 0.8f,
            },
            Position = new Vector3(0f, 0.85f, 2.26f),
        });
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.8f, 1.1f, 4.5f) },
            Position = new Vector3(0f, 1.0f, 0f),
        });
    }

    private void BuildWheels()
    {
        var tire = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f) };
        Vector3[] positions =
        [
            new(0.85f, 0.5f, 1.45f), new(-0.85f, 0.5f, 1.45f), // front
            new(0.85f, 0.5f, -1.45f), new(-0.85f, 0.5f, -1.45f), // rear
        ];
        for (int i = 0; i < 4; i++)
        {
            bool front = i < 2;
            var wheel = new VehicleWheel3D
            {
                Position = positions[i],
                UseAsSteering = front,
                UseAsTraction = true, // all-wheel drive: forgiving, arcade
                WheelRadius = 0.36f,
                WheelRestLength = 0.15f,
                SuspensionTravel = 0.2f,
                SuspensionStiffness = 45f,
                SuspensionMaxForce = 12000f,
                DampingCompression = 0.85f,
                DampingRelaxation = 0.9f,
                WheelFrictionSlip = Grip,
                WheelRollInfluence = 0.05f, // resists rolling over in corners
            };
            wheel.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.36f, Height = 0.25f },
                MaterialOverride = tire,
                Rotation = new Vector3(0f, 0f, Mathf.Pi / 2f),
            });
            AddChild(wheel);
            _wheels[i] = wheel;
        }
    }

    private void BuildCamera()
    {
        _cameraYaw = new Node3D { TopLevel = true };
        AddChild(_cameraYaw);
        _cameraPitch = new Node3D { Rotation = new Vector3(Mathf.DegToRad(-12f), 0f, 0f) };
        _cameraYaw.AddChild(_cameraPitch);
        var arm = new SpringArm3D { SpringLength = CameraDistance, Shape = new SphereShape3D { Radius = 0.3f } };
        arm.AddExcludedObject(GetRid());
        _cameraPitch.AddChild(arm);
        _camera = new Camera3D();
        arm.AddChild(_camera);
    }
}
