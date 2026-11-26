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
    private Node3D _prop = new();
    private IdleActivity _shownActivity = IdleActivity.None;
    private float _idleTime;
    private float _idleOffset; // so workers don't all sip at the same moment

    public WorkerState State { get; private set; } = null!;

    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 0.9f;
    public float InteractRadius => 0.35f;
    public bool IsAvailable => IsInstanceValid(this) && Visible;
    public string Title => $"Worker #{State.Id}";
    public string Status => State.Status;
    public bool RequiresApproach => false;

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
        _model.AddChild(_prop);
        _idleOffset = state.Id * 1.7f;
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

    public override void _PhysicsProcess(double delta)
    {
        _idleTime += (float)delta;
        Sync();
    }

    private void Sync()
    {
        Visible = State.IsActive;
        _collision.Disabled = !State.IsActive; // not in the building yet
        var local = new Vector3(State.Position.X, 0f, State.Position.Y) * FacilityNode.CellSize;
        GlobalPosition = _facility.ToGlobal(local);
        float yaw = Mathf.Atan2(-State.Facing.X, -State.Facing.Y);
        float lean = -0.45f * Mathf.Sin(State.ActionProgress * Mathf.Pi);
        if (State.Activity == IdleActivity.LeanOnWall)
            lean += 0.18f; // tip back so the shoulders rest on the wall behind
        _model.Rotation = new Vector3(lean, yaw, 0f);
        UpdateIdleProp();
        _label.Text = $"Worker #{State.Id}\n{State.Status}";

        if (_shownType != State.Carry.Type || _shownCount != State.Carry.Count)
        {
            _shownType = State.Carry.Type;
            _shownCount = State.Carry.Count;
            ItemVisuals.BuildStack(_carryAnchor, State.Carry.Type, State.Carry.Count);
        }
    }

    /// <summary>Placeholder props for idle activities: cigarette, beer bottle, crossed arms.</summary>
    private void UpdateIdleProp()
    {
        if (_shownActivity != State.Activity)
        {
            _shownActivity = State.Activity;
            foreach (var child in _prop.GetChildren())
                child.QueueFree();
            switch (State.Activity)
            {
                case IdleActivity.Smoking:
                    _prop.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.008f, BottomRadius = 0.008f, Height = 0.08f },
                        MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.95f, 0.9f) },
                        Rotation = new Vector3(Mathf.Pi / 2f, 0f, 0f),
                    });
                    _prop.AddChild(new MeshInstance3D // glowing tip
                    {
                        Mesh = new SphereMesh { Radius = 0.012f, Height = 0.024f },
                        MaterialOverride = new StandardMaterial3D
                        {
                            AlbedoColor = new Color(1f, 0.35f, 0.05f),
                            EmissionEnabled = true,
                            Emission = new Color(1f, 0.35f, 0.05f),
                            EmissionEnergyMultiplier = 2f,
                        },
                        Position = new Vector3(0f, 0f, -0.045f),
                    });
                    break;
                case IdleActivity.DrinkingBeer:
                    _prop.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.035f, Height = 0.22f },
                        MaterialOverride = new StandardMaterial3D
                        {
                            AlbedoColor = new Color(0.35f, 0.2f, 0.05f, 0.9f),
                            Roughness = 0.2f,
                        },
                    });
                    break;
                case IdleActivity.ArmsCrossed:
                    _prop.AddChild(new MeshInstance3D
                    {
                        Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.12f, 0.14f) },
                        MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.7f, 0.36f, 0.12f) },
                        Position = new Vector3(0f, 1.2f, -0.3f),
                    });
                    break;
            }
        }

        // Every few seconds: raise the cigarette / bottle to the mouth, then lower it.
        if (State.Activity is IdleActivity.Smoking or IdleActivity.DrinkingBeer)
        {
            float cycle = State.Activity == IdleActivity.Smoking ? 6f : 7f;
            float t = (_idleTime + _idleOffset) % cycle;
            float raise = t < 1.6f ? Mathf.Sin(t / 1.6f * Mathf.Pi) : 0f;
            var hand = new Vector3(0.24f, 1.0f, -0.15f);
            var mouth = new Vector3(0.06f, 1.52f, -0.32f);
            _prop.Position = hand.Lerp(mouth, raise);
            _prop.Rotation = State.Activity == IdleActivity.DrinkingBeer
                ? new Vector3(raise * 1.9f, 0f, 0f) // tip the bottle up to drink
                : Vector3.Zero;
        }
        else
        {
            _prop.Position = Vector3.Zero;
            _prop.Rotation = Vector3.Zero;
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
