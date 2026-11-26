using Godot;

namespace Gecko.Production;

/// <summary>One shelf of the Stash. Shows what's stored and lets the player take or store items.</summary>
public partial class StashSlotView : StaticBody3D, IInteractable
{
    private const float ShelfHeight = 0.9f;

    private FacilityNode _facility = null!;
    private int _index;
    private Label3D _label = null!;
    private Node3D _display = null!;
    private ItemType? _shownType;
    private int _shownCount = -1;

    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 0.6f;
    public float InteractRadius => 0.5f;

    private StashSlot Slot => _facility.State.Stash.Slots[_index];

    public void Setup(FacilityNode facility, int index)
    {
        _facility = facility;
        _index = index;
        var size = new Vector3(0.9f, ShelfHeight, 0.8f);
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.3f, 0.32f) },
            Position = Vector3.Up * ShelfHeight / 2f,
        });
        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size },
            Position = Vector3.Up * ShelfHeight / 2f,
        });
        _display = new Node3D { Position = Vector3.Up * ShelfHeight };
        AddChild(_display);
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 28,
            OutlineSize = 8,
            PixelSize = 0.004f,
            Position = Vector3.Up * (ShelfHeight + 0.7f),
        };
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        if (_shownType == Slot.Type && _shownCount == Slot.Count)
            return;
        _shownType = Slot.Type;
        _shownCount = Slot.Count;
        _label.Text = Slot.Type is { } type ? $"Stash\n{Items.Get(type).Name} x{Slot.Count}" : "Stash\n(empty)";
        ItemVisuals.BuildStack(_display, Slot.Type, Slot.Count, 5);
    }

    public InteractMode? GetMode(CarryState carry)
    {
        if (carry.Type is { } carried)
            return _facility.State.Stash.RoomFor(carried) > 0 ? InteractMode.Give : null;
        return Slot.Type != null ? InteractMode.Take : null;
    }

    public string GetPrompt(InteractMode mode, CarryState carry) => mode == InteractMode.Give
        ? $"Store {Items.Get(carry.Type ?? ItemType.Paper).Name}"
        : $"Take {(Slot.Type is { } type ? Items.Get(type).Name : "")}";

    public bool Interact(CarryState carry, InteractMode mode) => mode == InteractMode.Give
        ? _facility.State.StoreInStash(carry, 1) > 0
        : _facility.State.TakeFromStash(_index, carry, 1) > 0;
}
