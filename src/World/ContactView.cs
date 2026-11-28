using System.Collections.Generic;
using Gecko.Actors;
using Gecko.Economy;
using Gecko.Production;
using Gecko.Vehicles;
using Godot;

namespace Gecko.World;

/// <summary>
/// A buyer or distributor waiting at a meeting spot. Target them (on foot or from your car) to sell
/// bricks from your hands or from your car's trunk. After the deal they leave once you're out of sight.
/// </summary>
public partial class ContactView : AnimatableBody3D, IInteractable
{
    private const float TrunkReach = 12f; // your car must be parked this close to sell from its trunk
    private const float LeaveDistance = 30f;

    private Deal _deal = null!;
    private SalesState _sales = null!;
    private StandardMaterial3D _material = null!;
    private Label3D _label = null!;

    public Deal Deal => _deal;
    public bool IsAvailable => IsInstanceValid(this) && _deal.Phase == DealPhase.Open;
    public bool RequiresApproach => false; // a person: deal from where you stand (or from the car)
    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 1.0f;
    public float InteractRadius => 0.35f;
    public string Title => _deal.Contact.Name;

    public string Status
    {
        get
        {
            var c = _deal.Contact;
            string limit = c.Kind == ContactKind.Distributor
                ? $"Takes {c.MinPerDeal}-{c.MaxPerDeal}"
                : $"Takes up to {_deal.Remaining} more";
            return $"{limit} at {Money.Format(c.PricePerBrick)} each";
        }
    }

    public void Setup(Deal deal, SalesState sales)
    {
        _deal = deal;
        _sales = sales;
        SyncToPhysics = false;
        AddToGroup(PayphoneNode.InteractablesGroup);
        bool distributor = deal.Contact.Kind == ContactKind.Distributor;
        _material = new StandardMaterial3D
        {
            AlbedoColor = distributor ? new Color(0.25f, 0.3f, 0.4f) : new Color(0.15f, 0.35f, 0.2f),
        };
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.8f },
            MaterialOverride = _material,
            Position = Vector3.Up * 0.9f,
        });
        AddChild(new MeshInstance3D // hat, so contacts read differently from workers
        {
            Mesh = new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.28f, Height = 0.14f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.08f, 0.08f, 0.08f) },
            Position = Vector3.Up * 1.85f,
        });
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.3f, Height = 1.8f },
            Position = Vector3.Up * 0.9f,
        });
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 26,
            OutlineSize = 8,
            PixelSize = 0.004f,
            Position = Vector3.Up * 2.3f,
        };
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        _label.Text = _deal.Phase switch
        {
            DealPhase.Open => _deal.Contact.Name,
            _ => $"{_deal.Contact.Name}\n(leaving)",
        };

        // Gone once the deal is over and you're far enough away not to see them vanish.
        if (!_deal.IsActive && GetTree().GetFirstNodeInGroup(Player.Group) is Node3D player
            && player.GlobalPosition.DistanceTo(GlobalPosition) > LeaveDistance)
            QueueFree();
    }

    public Vector3 GetAccessPoint(Vector3 from)
    {
        var away = from - GlobalPosition;
        away.Y = 0f;
        var point = GlobalPosition + (away.LengthSquared() > 0.0001f ? away.Normalized() : Vector3.Forward) * 0.9f;
        point.Y = from.Y;
        return point;
    }

    public IReadOnlyList<InteractOption> GetOptions(Interactor who)
    {
        var contact = _deal.Contact;
        var options = new List<InteractOption>();
        double now = SalesService.Now();

        // From your hands.
        int inHands = who.Carry.Type == ItemType.CounterfeitBrick ? who.Carry.Count : 0;
        int handMax = MaxFromHands(who, inHands);
        string handDetail = inHands == 0 ? "no bricks in hand"
            : inHands < _deal.MinimumSale ? $"needs {_deal.MinimumSale}+ - use your trunk"
            : handMax < _deal.MinimumSale ? "no room for the cash"
            : $"up to {handMax} x {Money.Format(contact.PricePerBrick)}";
        options.Add(new InteractOption("Sell bricks you're carrying", handMax >= _deal.MinimumSale, handDetail, units =>
        {
            int max = MaxFromHands(who, who.Carry.Type == ItemType.CounterfeitBrick ? who.Carry.Count : 0);
            int count = units == 1 ? _deal.MinimumSale : max;
            return _sales.Sell(_deal, System.Math.Min(count, max), SalesService.Now(),
                n => who.Carry.Remove(n), cash => Payment.Pay(cash, who.Wallet, who.Carry, null));
        }));

        // From the trunk of your car parked nearby (or the one you're sitting in).
        var car = NearbyCar();
        int inTrunk = car?.Cargo.Items.GetValueOrDefault(ItemType.CounterfeitBrick) ?? 0;
        int trunkMax = car == null ? 0
            : SalesState.MaxSellable(_deal, inTrunk, Payment.Room(who.Wallet, who.Carry, car.Cargo));
        string trunkDetail = car == null ? "no car parked nearby"
            : inTrunk == 0 ? "no bricks in the trunk"
            : trunkMax < _deal.MinimumSale ? (inTrunk < _deal.MinimumSale ? $"needs {_deal.MinimumSale}+" : "no room for the cash")
            : $"up to {trunkMax} x {Money.Format(contact.PricePerBrick)}";
        options.Add(new InteractOption($"Sell from the {car?.DisplayName ?? "car"}'s trunk", trunkMax >= _deal.MinimumSale,
            trunkDetail, units =>
            {
                if (NearbyCar() is not { } c)
                    return 0;
                int have = c.Cargo.Items.GetValueOrDefault(ItemType.CounterfeitBrick);
                int max = SalesState.MaxSellable(_deal, have, Payment.Room(who.Wallet, who.Carry, c.Cargo));
                int count = units == 1 ? _deal.MinimumSale : max;
                return _sales.Sell(_deal, System.Math.Min(count, max), SalesService.Now(),
                    n => c.Cargo.TakeItems(ItemType.CounterfeitBrick, n),
                    cash => Payment.Pay(cash, who.Wallet, who.Carry, c.Cargo));
            }));
        return options;
    }

    /// <summary>
    /// Most bricks you can sell from your hands so the cash still fits: wallet room, plus duffel-bag room
    /// only if your hands end up empty (you can't hold bricks and bags at once).
    /// </summary>
    private int MaxFromHands(Interactor who, int inHands)
    {
        for (int n = System.Math.Min(_deal.Remaining, inHands); n >= 1; n--)
        {
            int room = who.Wallet.DirtyRoom + (n == inHands ? 3 * Money.BagCapacity : 0);
            if (n * _deal.Contact.PricePerBrick <= room)
                return n;
        }
        return 0;
    }

    /// <summary>The closest car parked near the meeting (or the one the player is in).</summary>
    private VehicleNode? NearbyCar()
    {
        VehicleNode? best = null;
        float bestDistance = TrunkReach;
        foreach (var node in GetTree().GetNodesInGroup(VehicleNode.Group))
        {
            if (node is not VehicleNode car)
                continue;
            float distance = car.GlobalPosition.DistanceTo(GlobalPosition);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = car;
            }
        }
        return best;
    }

    public void SetHighlighted(bool highlighted)
    {
        _material.EmissionEnabled = highlighted;
        _material.Emission = new Color(1f, 0.9f, 0.6f);
        _material.EmissionEnergyMultiplier = 0.35f;
    }
}
