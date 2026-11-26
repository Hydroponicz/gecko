using System.Collections.Generic;
using Gecko.Core;
using Godot;

namespace Gecko.UI;

/// <summary>
/// Turns an action name into a button label for whatever device the player is using,
/// e.g. Interact -> "[E]", "[Square]" or "[X]". Text for now; icon glyphs later.
/// </summary>
public static class Prompts
{
    private static readonly Dictionary<JoyButton, string> XboxNames = new()
    {
        [JoyButton.A] = "A", [JoyButton.B] = "B", [JoyButton.X] = "X", [JoyButton.Y] = "Y",
        [JoyButton.LeftShoulder] = "LB", [JoyButton.RightShoulder] = "RB",
        [JoyButton.Back] = "View", [JoyButton.Start] = "Menu",
        [JoyButton.LeftStick] = "LS", [JoyButton.RightStick] = "RS",
        [JoyButton.DpadUp] = "D-Pad Up", [JoyButton.DpadDown] = "D-Pad Down",
        [JoyButton.DpadLeft] = "D-Pad Left", [JoyButton.DpadRight] = "D-Pad Right",
    };

    private static readonly Dictionary<JoyButton, string> PlayStationNames = new()
    {
        [JoyButton.A] = "Cross", [JoyButton.B] = "Circle", [JoyButton.X] = "Square", [JoyButton.Y] = "Triangle",
        [JoyButton.LeftShoulder] = "L1", [JoyButton.RightShoulder] = "R1",
        [JoyButton.Back] = "Create", [JoyButton.Start] = "Options",
        [JoyButton.LeftStick] = "L3", [JoyButton.RightStick] = "R3",
        [JoyButton.DpadUp] = "D-Pad Up", [JoyButton.DpadDown] = "D-Pad Down",
        [JoyButton.DpadLeft] = "D-Pad Left", [JoyButton.DpadRight] = "D-Pad Right",
        [JoyButton.Touchpad] = "Touchpad",
    };

    /// <summary>"[Button] Text", or just the bracketed button when text is empty.</summary>
    public static string For(StringName action, string text = "") =>
        text.Length == 0 ? $"[{ButtonName(action)}]" : $"[{ButtonName(action)}] {text}";

    public static string ButtonName(StringName action)
    {
        var events = InputMap.ActionGetEvents(action);
        var gamepad = Gamepad.Instance;
        if (!gamepad.UsingGamepad)
        {
            foreach (var inputEvent in events)
            {
                switch (inputEvent)
                {
                    case InputEventKey key:
                        return key.AsTextPhysicalKeycode();
                    case InputEventMouseButton mouse:
                        return MouseName(mouse.ButtonIndex);
                }
            }
            return "?";
        }

        bool playStation = gamepad.Family == ControllerFamily.PlayStation;
        var names = playStation ? PlayStationNames : XboxNames;
        string? fallback = null;
        foreach (var inputEvent in events)
        {
            if (inputEvent is not InputEventJoypadButton button)
                continue;
            // PlayStation prefers the touchpad; other pads have no touchpad.
            if (playStation && button.ButtonIndex == JoyButton.Touchpad)
                return names[JoyButton.Touchpad];
            if (names.TryGetValue(button.ButtonIndex, out var name))
                fallback ??= name;
        }
        return fallback ?? "?";
    }

    private static string MouseName(MouseButton button) => button switch
    {
        MouseButton.Left => "LMB",
        MouseButton.Right => "RMB",
        MouseButton.Middle => "MMB",
        MouseButton.WheelUp => "Wheel Up",
        MouseButton.WheelDown => "Wheel Down",
        _ => "Mouse",
    };
}
