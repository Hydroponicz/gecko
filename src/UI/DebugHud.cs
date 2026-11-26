using System.Text;
using Gecko.Core;
using Godot;

namespace Gecko.UI;

/// <summary>Temporary on-screen info for testing input and controller features.</summary>
public partial class DebugHud : CanvasLayer
{
    private static readonly Color[] TestColors =
    [
        new(1f, 0.55f, 0.1f), // amber
        new(1f, 0f, 0f), // red
        new(0f, 0.3f, 1f), // blue
        new(0f, 1f, 0.3f), // green
        new(1f, 1f, 1f), // white
    ];

    private Label _label = null!;
    private int _colorIndex;

    public override void _Ready() => _label = GetNode<Label>("Label");

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(Actions.DebugLightbar))
            return;
        _colorIndex = (_colorIndex + 1) % TestColors.Length;
        Gamepad.Instance.SetLight(TestColors[_colorIndex]);
        Gamepad.Instance.Rumble(0.5f, 0f, 0.1f);
    }

    public override void _Process(double delta)
    {
        var text = new StringBuilder();
        text.AppendLine($"Input: {(Gamepad.Instance.UsingGamepad ? "Gamepad" : "Keyboard & Mouse")}");
        foreach (int device in Input.GetConnectedJoypads())
        {
            text.AppendLine(
                $"Pad {device}: {Input.GetJoyName(device)} | lightbar: {Input.HasJoyLight(device)}  " +
                $"rumble: {Input.HasJoyVibration(device)}  motion: {Input.HasJoyMotionSensors(device)}");
        }
        text.AppendLine();
        text.AppendLine("Move: WASD / Left stick    Look: Mouse / Right stick    Jump: Space / Cross");
        text.AppendLine("Sprint: hold Shift / click L3    Release mouse: Esc / Options");
        text.Append("Test lightbar + rumble: T / Triangle");
        _label.Text = text.ToString();
    }
}
