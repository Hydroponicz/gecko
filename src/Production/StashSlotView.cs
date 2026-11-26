using System.Collections.Generic;
using Godot;

namespace Gecko.Production;

/// <summary>One shelf of the Stash. Shows what's stored and lets the player take or store items.</summary>
public partial class StashSlotView : StaticBody3D, IInteractable
{
    private const float ShelfHeight = 0.9f;

    private FacilityNode _facility = null!;
    private int _index;
    private Vector2I _cell;
    private Label3D _label = null!;
    private Node3D _display = null!;
    private ItemType? _shownType;
    private int _shownCount = -1;
    private StandardMaterial3D _material = null!;

    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 0.6f;
    public float InteractRadius => 0.5f;

    private StashSlot Slot => _facility.State.Stash.Slots[_index];

    public void Setup(FacilityNode facility, int index, Vector2I cell)
    {
        _facility = facility;
        _index = index;
        _cell = cell;
        var size = new Vector3(0.9f, ShelfHeight, 0.8f);
        _material = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.3f, 0.32f) };
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = _material,
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

    public bool IsAvailable => IsInstanceValid(this);
    public Vector3 GetAccessPoint(Vector3 from) => _facility.AccessPoint(new Rect2I(_cell, Vector2I.One), from);
    public string Title => Slot.Type is { } type ? $"Stash - {Items.Get(type).Name}" : "Stash - empty shelf";
    public string Status => Slot.Type != null ? $"{Slot.Count} / {StashState.SlotCapacity}" : "Store anything you're carrying";

    public IReadOnlyList<InteractOption> GetOptions(CarryState carry)
    {
        var stash = _facility.State;
        var options = new List<InteractOption>();

        if (Slot.Type is { } stored)
        {
            bool canCarry = carry.RoomFor(stored) > 0;
            string detail = !canCarry
                ? (carry.Type == stored ? "hands full" : "holding something else")
                : $"{Slot.Count} here";
            options.Add(new InteractOption($"Take {Items.Get(stored).Name}", canCarry, detail,
                amount => stash.TakeFromStash(_index, carry, amount)));
        }

        if (carry.Type is { } carried)
        {
            bool room = stash.Stash.RoomFor(carried) > 0;
            options.Add(new InteractOption($"Store {Items.Get(carried).Name}", room,
                room ? $"{carry.Count} in hand" : "stash full",
                amount => stash.StoreInStash(carry, amount)));
        }
        else
        {
            options.Add(new InteractOption("Store", false, "nothing in hand", _ => 0));
        }
        return options;
    }

    public void SetHighlighted(bool highlighted)
    {
        _material.EmissionEnabled = highlighted;
        _material.Emission = new Color(1f, 0.9f, 0.6f);
        _material.EmissionEnergyMultiplier = 0.35f;
    }
}
