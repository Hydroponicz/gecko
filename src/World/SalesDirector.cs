using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gecko.Actors;
using Gecko.Core;
using Gecko.Economy;
using Gecko.UI;
using Godot;

namespace Gecko.World;

/// <summary>
/// Runs selling in the level: owns the <see cref="SalesState"/>, puts contacts at their meeting spots
/// while a window is open, shows marker beams and the objective line, and reports how deals went.
/// </summary>
public partial class SalesDirector : Node3D
{
    private SalesState _sales = null!;
    private readonly Dictionary<Deal, ContactView> _contacts = new();
    private readonly Dictionary<Deal, MeshInstance3D> _markers = new();

    public override void _Ready()
    {
        _sales = new SalesState(Contacts.CreateStarting());
        SalesService.Current = _sales;
        SalesService.Now = () => ClockNode.Clock.TotalMinutes;
        _sales.DealChanged += OnDealChanged;
    }

    public override void _ExitTree()
    {
        if (SalesService.Current == _sales)
            SalesService.Current = null;
    }

    public override void _Process(double delta)
    {
        _sales.Tick(ClockNode.Clock.TotalMinutes);
        UpdateMarkers();
        GameHud.Instance.SetObjective(ObjectiveText());
    }

    private void OnDealChanged(Deal deal)
    {
        var contact = deal.Contact;
        switch (deal.Phase)
        {
            case DealPhase.Scheduled when deal.BricksSold == 0:
                GameHud.Instance.Notify(
                    $"{contact.Name}: \"{deal.Spot.Name}. Be there between {Clock(deal.WindowStart)} and {Clock(deal.WindowEnd)}.\"");
                break;
            case DealPhase.Open when !_contacts.ContainsKey(deal):
                SpawnContact(deal);
                GameHud.Instance.Notify($"{contact.Name} is waiting: {deal.Spot.Name}");
                break;
            case DealPhase.Done:
                GameHud.Instance.Notify($"Deal done with {contact.Name}: {deal.BricksSold} brick(s) for " +
                                        $"{Money.Format(deal.BricksSold * contact.PricePerBrick)}" +
                                        (contact.OnCooldown(ClockNode.Clock.TotalMinutes)
                                            ? $"\n{contact.Name} won't buy again until {SalesState.FormatTime(contact.CooldownUntil)}"
                                            : ""));
                break;
            case DealPhase.Missed:
                GameHud.Instance.Notify($"{contact.Name} got tired of waiting and left.");
                break;
        }
    }

    private void SpawnContact(Deal deal)
    {
        var view = new ContactView { Name = $"Contact_{deal.Contact.Name.Replace(' ', '_')}" };
        AddChild(view);
        view.GlobalPosition = deal.Spot.Position;
        view.Setup(deal, _sales);
        _contacts[deal] = view;
        view.TreeExited += () => _contacts.Remove(deal);
    }

    /// <summary>A tall translucent beam over each meeting spot: amber while waiting, green when they're there.</summary>
    private void UpdateMarkers()
    {
        foreach (var deal in _markers.Keys.Where(d => !d.IsActive).ToList())
        {
            _markers[deal].QueueFree();
            _markers.Remove(deal);
        }
        foreach (var deal in _sales.ActiveDeals)
        {
            if (!_markers.TryGetValue(deal, out var beam))
            {
                beam = new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0.6f, BottomRadius = 0.6f, Height = 60f },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(beam);
                beam.GlobalPosition = deal.Spot.Position + Vector3.Up * 30f;
                _markers[deal] = beam;
            }
            beam.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = deal.Phase == DealPhase.Open ? new Color(0.3f, 1f, 0.4f, 0.25f) : new Color(1f, 0.75f, 0.2f, 0.2f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
        }
    }

    private string ObjectiveText()
    {
        var deals = _sales.ActiveDeals.OrderBy(d => d.WindowStart).ToList();
        if (deals.Count == 0)
            return "";
        var player = GetTree().GetFirstNodeInGroup(Player.Group) as Node3D;
        var text = new StringBuilder();
        foreach (var deal in deals)
        {
            string distance = player != null ? $"{Mathf.RoundToInt(player.GlobalPosition.DistanceTo(deal.Spot.Position))} m" : "";
            string when = deal.Phase == DealPhase.Open
                ? $"there now, until {Clock(deal.WindowEnd)}"
                : $"{Clock(deal.WindowStart)}-{Clock(deal.WindowEnd)}";
            text.AppendLine($"Meet {deal.Contact.Name}: {deal.Spot.Name} ({when}) - {distance}");
        }
        return text.ToString().TrimEnd();
    }

    private static string Clock(double totalMinutes)
    {
        int minute = (int)(totalMinutes % (24 * 60));
        return $"{minute / 60:00}:{minute % 60:00}";
    }
}
