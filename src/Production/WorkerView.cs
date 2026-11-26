using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

/// <summary>
/// Gray-box body for a worker. Movement and decisions live in <see cref="WorkerState"/>; this just follows it.
/// Solid, so the player bumps into workers. Target a worker to let them go.
/// </summary>
public partial class WorkerView : AnimatableBody3D, IInteractable
{
    private FacilityNode _facility = null!;
    private Node3D _model = null!;
    private Node3D _carryAnchor = null!;
    private Label3D _label = null!;
    private CollisionShape3D _collision = null!;
    private StandardMaterial3D _material = null!;
    private ItemType? _shownType;
    private int _shownCount = -1;

    public WorkerState State { get; private set; } = null!;

    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 0.9f;
    public float InteractRadius => 0.35f;
    public bool IsAvailable => IsInstanceValid(this) && Visible;
    public string Title => $"Worker #{State.Id}";
    public string Status => State.Status;

    public void Setup(FacilityNode facility, WorkerState state)
    {
        _facility = facility;
        State = state;
        SyncToPhysics = false;

        _model = new Node3D();
        AddChild(_model);
        _material = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.45f, 0.15f) }; // orange work shirt
        _model.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.75f },
            MaterialOverride = _material,
            Position = Vector3.Up * 0.875f,
        });
        _model.AddChild(new MeshInstance3D // "nose" so you can see which way they face
        {
            Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.12f, 0.25f) },
            Position = new Vector3(0f, 1.45f, -0.35f),
        });
        _carryAnchor = new Node3D { Position = new Vector3(0f, 0.85f, -0.45f) };
        _model.AddChild(_carryAnchor);
        _collision = new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.75f },
            Position = Vector3.Up * 0.875f,
        };
        AddChild(_collision);
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 22,
            OutlineSize = 6,
            PixelSize = 0.004f,
            Position = Vector3.Up * 2.1f,
        };
        AddChild(_label);
        Sync();
    }

    public override void _PhysicsProcess(double delta) => Sync();

    private void Sync()
    {
        Visible = State.IsActive;
        _collision.Disabled = !State.IsActive; // not here yet
        var local = new Vector3(State.Position.X, 0f, State.Position.Y) * FacilityNode.CellSize;
        GlobalPosition = _facility.ToGlobal(local);
        float yaw = Mathf.Atan2(-State.Facing.X, -State.Facing.Y);
        float lean = -0.45f * Mathf.Sin(State.ActionProgress * Mathf.Pi);
        _model.Rotation = new Vector3(lean, yaw, 0f);
        _label.Text = $"Worker #{State.Id}\n{State.Status}";

        if (_shownType != State.Carry.Type || _shownCount != State.Carry.Count)
        {
            _shownType = State.Carry.Type;
            _shownCount = State.Carry.Count;
            ItemVisuals.BuildStack(_carryAnchor, State.Carry.Type, State.Carry.Count);
        }
    }

    public Vector3 GetAccessPoint(Vector3 from)
    {
        var away = from - GlobalPosition;
        away.Y = 0f;
        var point = GlobalPosition + (away.LengthSquared() > 0.0001f ? away.Normalized() : Vector3.Forward) * 0.8f;
        point.Y = from.Y;
        return point;
    }

    public IReadOnlyList<InteractOption> GetOptions(Interactor who)
    {
        bool leaving = State.Phase == WorkerPhase.Leaving;
        return
        [
            new InteractOption("Let go", !leaving, leaving ? "already leaving" : $"wage {Gecko.Economy.Money.Format(FacilityState.WorkerWage)}/day",
                _ =>
                {
                    _facility.State.FireWorker(State);
                    return 1;
                }),
        ];
    }

    public void SetHighlighted(bool highlighted)
    {
        _material.EmissionEnabled = highlighted;
        _material.Emission = new Color(1f, 0.9f, 0.6f);
        _material.EmissionEnergyMultiplier = 0.35f;
    }
}
