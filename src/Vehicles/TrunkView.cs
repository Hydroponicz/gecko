using System.Collections.Generic;
using System.Linq;
using Gecko.Economy;
using Gecko.Production;
using Gecko.World;
using Godot;

namespace Gecko.Vehicles;

/// <summary>
/// The back of a vehicle: target it on foot to load or unload what you're carrying.
/// Cargo is shown sitting in the (open) trunk area.
/// </summary>
public partial class TrunkView : Node3D, IInteractable
{
    private readonly VehicleNode _vehicle;
    private Node3D _display = null!;
    private MeshInstance3D? _highlight;

    public TrunkView(VehicleNode vehicle) => _vehicle = vehicle;

    // Required by Godot for nodes created from scripts.
    public TrunkView() => _vehicle = null!;

    public Vector3 InteractPosition => _vehicle.TrunkPosition;
    public float InteractRadius => 0.9f;
    public bool IsAvailable => IsInstanceValid(this) && _vehicle.Driver == null;
    public string Title => $"{_vehicle.DisplayName} - Trunk";
    public string Status => $"Space {_vehicle.Cargo.SpaceUsed}/{_vehicle.Cargo.Capacity}"
                            + (_vehicle.Cargo.Bags.Count > 0 ? $"   Bags {Money.Format(_vehicle.Cargo.BagCash)}" : "");

    public override void _Ready()
    {
        AddToGroup(PayphoneNode.InteractablesGroup);
        _display = new Node3D { Position = new Vector3(0f, 1.06f, -1.75f) }; // on the rear deck
        AddChild(_display);
        _vehicle.Cargo.Changed += RefreshDisplay;
    }

    public Vector3 GetAccessPoint(Vector3 from)
    {
        var point = _vehicle.GlobalTransform * new Vector3(0f, 0f, -3.0f);
        point.Y = from.Y;
        return point;
    }

    public IReadOnlyList<InteractOption> GetOptions(Interactor who)
    {
        var cargo = _vehicle.Cargo;
        var carry = who.Carry;
        var options = new List<InteractOption>();

        if (carry.Type is { } carried)
        {
            int room = cargo.RoomFor(carried);
            options.Add(new InteractOption($"Load {Items.Get(carried).Name}", room > 0,
                room > 0 ? $"room for {room}" : "trunk full",
                units => cargo.LoadFrom(carry, units)));
        }
        else
        {
            options.Add(new InteractOption("Load", false, "nothing in hand", _ => 0));
        }

        // One "Take" option per stored kind of thing (bags first), up to the menu size.
        var stored = new List<ItemType>();
        if (cargo.Bags.Count > 0)
            stored.Add(ItemType.DuffelBag);
        stored.AddRange(cargo.Items.Keys.OrderBy(t => t));
        foreach (var type in stored.Take(3))
        {
            int have = type == ItemType.DuffelBag ? cargo.Bags.Count : cargo.Items[type];
            bool canTake = type == ItemType.DuffelBag
                ? (carry.IsEmpty || carry.Type == ItemType.DuffelBag) && carry.Count < Items.Get(type).MaxCarry
                : carry.RoomFor(type) > 0;
            options.Add(new InteractOption($"Take {Items.Get(type).Name}", canTake,
                canTake ? $"{have} in trunk" : "hands full",
                units => cargo.UnloadTo(carry, type, units)));
        }
        return options;
    }

    public void SetHighlighted(bool highlighted)
    {
        if (highlighted && _highlight == null)
        {
            _highlight = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.7f, 0.05f, 1.2f) },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.9f, 0.6f, 0.35f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                Position = new Vector3(0f, 1.07f, -1.7f),
            };
            AddChild(_highlight);
        }
        else if (!highlighted && _highlight != null)
        {
            _highlight.QueueFree();
            _highlight = null;
        }
    }

    /// <summary>Shows bags and up to 20 items laid out on the rear deck.</summary>
    private void RefreshDisplay()
    {
        foreach (var child in _display.GetChildren())
            child.QueueFree();
        int slot = 0;
        foreach (int _ in _vehicle.Cargo.Bags)
        {
            var bag = ItemVisuals.Create(ItemType.DuffelBag);
            bag.Position = new Vector3(-0.5f + slot * 0.55f, 0.13f, 0f);
            bag.Rotation = new Vector3(0f, Mathf.Pi / 2f, 0f);
            _display.AddChild(bag);
            slot++;
        }
        int shown = 0;
        foreach (var (type, count) in _vehicle.Cargo.Items)
        {
            var size = Items.Get(type).Size;
            for (int i = 0; i < count && shown < 20; i++, shown++)
            {
                int column = shown % 4;
                int row = shown / 4 % 3;
                int layer = shown / 12;
                var item = ItemVisuals.Create(type);
                item.Position = new Vector3(0.1f + column * 0.2f, size.Y * (layer + 0.5f), -0.3f + row * 0.25f);
                _display.AddChild(item);
            }
        }
    }
}
