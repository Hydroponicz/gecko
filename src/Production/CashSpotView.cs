using System.Collections.Generic;
using Gecko.Economy;
using Godot;

namespace Gecko.Production;

/// <summary>
/// The cash table next to the Stash shelves. Dirty cash kept at the property shows here as
/// physical $1,000 stacks. Move cash between your wallet, the table and duffel bags.
/// </summary>
public partial class CashSpotView : StaticBody3D, IInteractable
{
    private const float TableHeight = 0.8f;
    private const int MaxShownStacks = 48;

    private FacilityNode _facility = null!;
    private Vector2I _cell;
    private Label3D _label = null!;
    private Node3D _piles = null!;
    private StandardMaterial3D _material = null!;
    private int _shownCash = -1;

    public Vector3 InteractPosition => GlobalPosition + Vector3.Up * 0.6f;
    public float InteractRadius => 0.5f;
    public bool IsAvailable => IsInstanceValid(this);
    public string Title => "Cash Table";
    public string Status => $"{Money.Format(Stash.DirtyCash)} dirty cash here";

    private StashState Stash => _facility.State.Stash;

    public Vector3 GetAccessPoint(Vector3 from) => _facility.AccessPoint(new Rect2I(_cell, Vector2I.One), from);

    public void Setup(FacilityNode facility, Vector2I cell)
    {
        _facility = facility;
        _cell = cell;
        var size = new Vector3(0.9f, TableHeight, 0.8f);
        _material = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.25f, 0.18f) };
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = _material,
            Position = Vector3.Up * TableHeight / 2f,
        });
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = Vector3.Up * TableHeight / 2f });
        _piles = new Node3D { Position = Vector3.Up * TableHeight };
        AddChild(_piles);
        _label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            FontSize = 28,
            OutlineSize = 8,
            PixelSize = 0.004f,
            Position = Vector3.Up * (TableHeight + 0.8f),
        };
        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        if (_shownCash == Stash.DirtyCash)
            return;
        _shownCash = Stash.DirtyCash;
        _label.Text = $"Cash Table\n{Money.Format(_shownCash)}";

        // One stack per started $1,000, laid out in a 4 x 3 grid, then piled up.
        foreach (var child in _piles.GetChildren())
            child.QueueFree();
        int stacks = Mathf.Min((_shownCash + Money.StackValue - 1) / Money.StackValue, MaxShownStacks);
        var size = Items.Get(ItemType.CashStack).Size;
        for (int i = 0; i < stacks; i++)
        {
            int layer = i / 12;
            int x = i % 4;
            int z = i / 4 % 3;
            var stack = ItemVisuals.Create(ItemType.CashStack);
            stack.Position = new Vector3((x - 1.5f) * 0.19f, size.Y * (layer + 0.5f), (z - 1f) * 0.2f);
            _piles.AddChild(stack);
        }
    }

    public IReadOnlyList<InteractOption> GetOptions(Interactor who)
    {
        var state = _facility.State;
        var wallet = who.Wallet;
        var carry = who.Carry;
        bool handsFreeForBags = carry.IsEmpty || carry.Type == ItemType.DuffelBag;
        bool bagRoom = handsFreeForBags && carry.AddCashToBagsRoom() > 0;
        int bags = carry.Type == ItemType.DuffelBag ? carry.Count : 0;

        return
        [
            new InteractOption("Deposit from wallet", wallet.Dirty > 0,
                wallet.Dirty > 0 ? $"{Money.Format(wallet.Dirty)} in wallet" : "wallet empty",
                units => state.DepositCash(wallet, units)),
            new InteractOption("Take to wallet", Stash.DirtyCash > 0 && wallet.DirtyRoom > 0,
                Stash.DirtyCash == 0 ? "no cash here" : wallet.DirtyRoom == 0 ? "wallet full" : $"wallet room {Money.Format(wallet.DirtyRoom)}",
                units => state.WithdrawCash(wallet, units)),
            new InteractOption("Pack duffel bag", Stash.DirtyCash > 0 && bagRoom,
                Stash.DirtyCash == 0 ? "no cash here" : !handsFreeForBags ? "hands full" : !bagRoom ? "carrying 3 full bags" : $"up to {Money.Format(Money.BagCapacity)} a bag",
                units => state.PackBags(carry, units)),
            new InteractOption("Unpack duffel bags", bags > 0,
                bags > 0 ? $"{bags} bag(s), {Money.Format(carry.TotalBagCash)}" : "no bags on you",
                units => state.UnpackBags(carry, units)),
        ];
    }

    public void SetHighlighted(bool highlighted)
    {
        _material.EmissionEnabled = highlighted;
        _material.Emission = new Color(1f, 0.9f, 0.6f);
        _material.EmissionEnergyMultiplier = 0.35f;
    }
}
