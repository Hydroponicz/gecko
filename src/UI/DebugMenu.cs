using System;
using System.Collections.Generic;
using System.Text;
using Gecko.Actors;
using Gecko.Core;
using Gecko.Production;
using Godot;

namespace Gecko.UI;

/// <summary>
/// Autoload. Debug menu for testing: F1 / R3 (right stick click) toggles it.
/// Navigate with arrow keys / D-pad up-down, change values with left-right, use with Enter / Cross / A.
/// Only exists in debug builds (running from the editor); exported release builds remove it.
/// To add a tool, add an entry in <see cref="BuildEntries"/>.
/// </summary>
public partial class DebugMenu : CanvasLayer
{
    public static bool IsOpen { get; private set; }

    private static readonly float[] TimeScales = [1f, 2f, 5f, 10f];
    private static readonly Color[] TestColors =
    [
        new(1f, 0.55f, 0.1f), new(1f, 0f, 0f), new(0f, 0.3f, 1f), new(0f, 1f, 0.3f), new(1f, 1f, 1f),
    ];

    private sealed record Entry(Func<string> Label, Action? OnAccept = null, Action<int>? OnChange = null);

    private readonly List<Entry> _entries = [];
    private Label _panel = null!;
    private Label _hint = null!;
    private int _selected;
    private int _itemIndex;
    private int _timeIndex;
    private int _colorIndex;
    private string _lastMessage = "";

    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        _panel = MakeLabel(16, new Vector2(16f, 16f));
        _panel.Visible = false;
        _hint = MakeLabel(13, new Vector2(16f, 16f));
        _hint.Modulate = new Color(1f, 1f, 1f, 0.5f);
        BuildEntries();
    }

    private void BuildEntries()
    {
        var items = (ItemType[])Enum.GetValues(typeof(ItemType));

        _entries.Add(new Entry(
            () => $"Give 5:  < {Items.Get(items[_itemIndex]).Name} >",
            () => GiveCarry(items[_itemIndex], 5),
            dir => _itemIndex = (_itemIndex + dir + items.Length) % items.Length));
        _entries.Add(new Entry(() => "Drop what you're carrying", () =>
        {
            if (PlayerCarry() is { } carry)
                carry.Remove(carry.Count);
            _lastMessage = "Hands empty";
        }));
        _entries.Add(new Entry(() => "Refill Stash (+30 Paper, +30 Ink)", () =>
        {
            foreach (var facility in Facilities())
            {
                facility.State.Stash.Add(ItemType.Paper, 30);
                facility.State.Stash.Add(ItemType.Ink, 30);
            }
            _lastMessage = "Stash refilled";
        }));
        _entries.Add(new Entry(
            () => $"Game speed:  < x{TimeScales[_timeIndex]} >",
            null,
            dir =>
            {
                _timeIndex = Mathf.Clamp(_timeIndex + dir, 0, TimeScales.Length - 1);
                Engine.TimeScale = TimeScales[_timeIndex];
            }));
        _entries.Add(new Entry(() => "Fast-forward machines 60s", () =>
        {
            foreach (var facility in Facilities())
                facility.State.Tick(60f);
            _lastMessage = "Machines advanced 60s";
        }));
        _entries.Add(new Entry(() => "Test lightbar + rumble", () =>
        {
            _colorIndex = (_colorIndex + 1) % TestColors.Length;
            Gamepad.Instance.SetLight(TestColors[_colorIndex]);
            Gamepad.Instance.Rumble(0.5f, 0f, 0.1f);
            _lastMessage = $"Lightbar color {_colorIndex + 1}/{TestColors.Length}";
        }));
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed(Actions.DebugMenu))
            IsOpen = !IsOpen;
        _panel.Visible = IsOpen;
        _hint.Visible = !IsOpen;
        _hint.Text = $"{Prompts.For(Actions.DebugMenu)} Debug";
        if (!IsOpen)
            return;

        if (Input.IsActionJustPressed(Actions.MenuDown))
            _selected = (_selected + 1) % _entries.Count;
        if (Input.IsActionJustPressed(Actions.MenuUp))
            _selected = (_selected - 1 + _entries.Count) % _entries.Count;
        var entry = _entries[_selected];
        if (Input.IsActionJustPressed(Actions.MenuRight))
            entry.OnChange?.Invoke(1);
        if (Input.IsActionJustPressed(Actions.MenuLeft))
            entry.OnChange?.Invoke(-1);
        if (Input.IsActionJustPressed(Actions.MenuAccept))
            entry.OnAccept?.Invoke();

        var text = new StringBuilder("DEBUG MENU\n\n");
        for (int i = 0; i < _entries.Count; i++)
            text.Append(i == _selected ? "> " : "   ").Append(_entries[i].Label()).Append('\n');
        text.Append($"\n{Prompts.ButtonName(Actions.MenuUp)}/{Prompts.ButtonName(Actions.MenuDown)}: select   " +
                    $"{Prompts.ButtonName(Actions.MenuLeft)}/{Prompts.ButtonName(Actions.MenuRight)}: change   " +
                    $"{Prompts.ButtonName(Actions.MenuAccept)}: use\n");
        text.Append(DeviceInfo());
        if (_lastMessage.Length > 0)
            text.Append($"\n{_lastMessage}");
        _panel.Text = text.ToString();
    }

    private void GiveCarry(ItemType type, int amount)
    {
        if (PlayerCarry() is not { } carry)
            return;
        carry.Remove(carry.Count); // one item type at a time
        carry.Add(type, amount);
        _lastMessage = $"Gave {amount} {Items.Get(type).Name}";
    }

    private CarryState? PlayerCarry() =>
        GetTree().GetFirstNodeInGroup(Player.Group) is Player player
            ? player.GetNode<PlayerInteraction>("PlayerInteraction").Carry
            : null;

    private IEnumerable<FacilityNode> Facilities()
    {
        foreach (var node in GetTree().GetNodesInGroup(FacilityNode.Group))
        {
            if (node is FacilityNode facility)
                yield return facility;
        }
    }

    private static string DeviceInfo()
    {
        var text = new StringBuilder($"\nInput: {(Gamepad.Instance.UsingGamepad ? $"Gamepad ({Gamepad.Instance.Family})" : "Keyboard & Mouse")}");
        foreach (int device in Input.GetConnectedJoypads())
        {
            text.Append($"\nPad {device}: {Input.GetJoyName(device)} | lightbar: {Input.HasJoyLight(device)}  " +
                        $"rumble: {Input.HasJoyVibration(device)}  motion: {Input.HasJoyMotionSensors(device)}");
        }
        return text.ToString();
    }

    private Label MakeLabel(int fontSize, Vector2 position)
    {
        var label = new Label { Position = position, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        AddChild(label);
        return label;
    }
}
