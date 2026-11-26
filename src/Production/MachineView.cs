using System.Text;
using Godot;

namespace Gecko.Production;

/// <summary>Gray-box visuals, collision and interaction for one placed machine.</summary>
public partial class MachineView : StaticBody3D, IInteractable
{
    private FacilityNode _facility = null!;
    private Label3D _label = null!;
    private Node3D _outputStack = null!;
    private int _shownOutput = -1;

    public MachineState State { get; private set; } = null!;

    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 0.8f;
    public float InteractRadius => Mathf.Max(State.Def.Footprint.X, State.Def.Footprint.Y) * 0.5f;

    public void Setup(FacilityNode facility, MachineState state)
    {
        _facility = facility;
        State = state;
        var def = state.Def;
        var size = new Vector3(def.Footprint.X * 0.9f, def.Height, def.Footprint.Y * 0.9f);

        var body = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = def.Color },
            Position = Vector3.Up * def.Height / 2f,
        };
        AddChild(body);
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
            Position = Vector3.Up * def.Height / 2f,
        });

        // Finished output sits on top of the machine so you can see it.
        _outputStack = new Node3D { Position = Vector3.Up * def.Height };
        AddChild(_outputStack);

        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 28,
            OutlineSize = 8,
            PixelSize = 0.004f,
            Position = Vector3.Up * (def.Height + 0.9f),
        };
        AddChild(_label);
        SyncTransform();
    }

    public void SyncTransform() => Transform = _facility.MachineLocalTransform(State.Def, State.Cell, State.Rotation);

    /// <summary>Hidden while lifted in build mode.</summary>
    public void SetLifted(bool lifted)
    {
        Visible = !lifted;
        foreach (var child in GetChildren())
        {
            if (child is CollisionShape3D shape)
                shape.Disabled = lifted;
        }
    }

    public override void _Process(double delta)
    {
        var def = State.Def;
        var text = new StringBuilder(def.Name).Append('\n');
        foreach (var input in def.Inputs.Keys)
            text.Append($"{Items.Get(input).Name} {State.InputCount(input)}/{def.InputCapacity}  ");
        text.Append('\n');
        if (def.Slots > 1)
            text.Append($"Working {State.ActiveCrafts}/{def.Slots}  ");
        text.Append(ProgressBar(State.LeadProgress));
        text.Append($"\n-> {Items.Get(def.Output).Name}: {State.OutputCount}");
        _label.Text = text.ToString();

        if (_shownOutput != State.OutputCount)
        {
            _shownOutput = State.OutputCount;
            ItemVisuals.BuildStack(_outputStack, def.Output, State.OutputCount, 6);
        }
    }

    public InteractMode? GetMode(CarryState carry)
    {
        if (carry.Type is { } type)
        {
            if (State.InputRoom(type) > 0)
                return InteractMode.Give;
            if (type == State.Def.Output && State.OutputCount > 0 && carry.RoomFor(type) > 0)
                return InteractMode.Take;
            return null;
        }
        return State.OutputCount > 0 ? InteractMode.Take : null;
    }

    public string GetPrompt(InteractMode mode, CarryState carry) => mode == InteractMode.Give
        ? $"Load {Items.Get(carry.Type ?? State.Def.Output).Name}"
        : $"Take {Items.Get(State.Def.Output).Name}";

    public bool Interact(CarryState carry, InteractMode mode) => mode == InteractMode.Give
        ? _facility.State.LoadMachine(State, carry, 1) > 0
        : _facility.State.TakeFromMachine(State, carry, 1) > 0;

    private static string ProgressBar(float progress)
    {
        const int width = 10;
        int filled = Mathf.Clamp(Mathf.RoundToInt(progress * width), 0, width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
    }
}
