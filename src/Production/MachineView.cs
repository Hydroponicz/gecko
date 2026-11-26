using System.Collections.Generic;
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
    private StandardMaterial3D _material = null!;

    public MachineState State { get; private set; } = null!;

    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 0.8f;
    public float InteractRadius => Mathf.Max(State.Def.Footprint.X, State.Def.Footprint.Y) * 0.5f;

    public void Setup(FacilityNode facility, MachineState state)
    {
        _facility = facility;
        State = state;
        var def = state.Def;
        var size = new Vector3(def.Footprint.X * 0.9f, def.Height, def.Footprint.Y * 0.9f);

        _material = new StandardMaterial3D { AlbedoColor = def.Color };
        var body = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = _material,
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
        if (def.Kind == MachineKind.Phone)
        {
            _label.Text = $"{def.Name}\n{PhoneMenu.Status(_facility)}";
            return;
        }
        var text = new StringBuilder(def.Name).Append('\n');
        foreach (var input in def.Inputs.Keys)
            text.Append($"{Items.Get(input).Name} {State.Loaded(input)}/{def.InputCapacity}  ");
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

    public bool IsAvailable => IsInstanceValid(this) && Visible;
    public Vector3 GetAccessPoint(Vector3 from) => _facility.AccessPoint(State.Rect, from);
    public string Title => State.Def.Kind == MachineKind.Phone ? "Landline - Supplier" : State.Def.Name;

    public string Status
    {
        get
        {
            if (State.Def.Kind == MachineKind.Phone)
                return PhoneMenu.Status(_facility);
            var text = new StringBuilder();
            foreach (var input in State.Def.Inputs.Keys)
                text.Append($"{Items.Get(input).Name} {State.Loaded(input)}/{State.Def.InputCapacity}   ");
            text.Append($"-> {Items.Get(State.Def.Output).Name}: {State.OutputCount}");
            return text.ToString();
        }
    }

    public IReadOnlyList<InteractOption> GetOptions(Interactor who)
    {
        if (State.Def.Kind == MachineKind.Phone)
            return PhoneMenu.GetOptions(_facility, who);
        var carry = who.Carry;
        var options = new List<InteractOption>();
        var facility = _facility.State;

        foreach (var input in State.Def.Inputs.Keys)
        {
            string name = Items.Get(input).Name;
            bool holding = carry.Type == input;
            bool room = State.InputRoom(input) > 0;
            string detail = !holding ? "none in hand" : !room ? "machine full" : $"{carry.Count} in hand";
            options.Add(new InteractOption($"Load {name}", holding && room, detail,
                amount => facility.LoadMachine(State, carry, amount)));
        }

        var output = State.Def.Output;
        if (!State.Def.OutputToStash || State.OutputCount > 0)
        {
            bool ready = State.OutputCount > 0;
            bool canCarry = carry.RoomFor(output) > 0;
            string detail = !ready ? "none ready"
                : !canCarry ? (carry.Type == output ? "hands full" : "holding something else")
                : $"{State.OutputCount} ready";
            options.Add(new InteractOption($"Take {Items.Get(output).Name}", ready && canCarry, detail,
                amount => facility.TakeFromMachine(State, carry, amount)));
        }
        return options;
    }

    public void SetHighlighted(bool highlighted)
    {
        _material.EmissionEnabled = highlighted;
        _material.Emission = new Color(1f, 0.9f, 0.6f);
        _material.EmissionEnergyMultiplier = 0.35f;
    }

    private static string ProgressBar(float progress)
    {
        const int width = 10;
        int filled = Mathf.Clamp(Mathf.RoundToInt(progress * width), 0, width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
    }
}
