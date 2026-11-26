using Godot;

namespace Gecko.Core;

/// <summary>
/// Autoload that registers all input bindings (keyboard/mouse + gamepad) at startup.
/// Godot uses Xbox-style button names. On a PS5 DualSense:
/// A = Cross, B = Circle, X = Square, Y = Triangle, Start = Options, LeftStick = L3.
/// </summary>
public partial class InputSetup : Node
{
    private const float Deadzone = 0.2f;

    public override void _EnterTree()
    {
        // Movement
        Bind(Actions.MoveForward, KeyEvent(Key.W), AxisEvent(JoyAxis.LeftY, -1f));
        Bind(Actions.MoveBack, KeyEvent(Key.S), AxisEvent(JoyAxis.LeftY, 1f));
        Bind(Actions.MoveLeft, KeyEvent(Key.A), AxisEvent(JoyAxis.LeftX, -1f));
        Bind(Actions.MoveRight, KeyEvent(Key.D), AxisEvent(JoyAxis.LeftX, 1f));
        Bind(Actions.Jump, KeyEvent(Key.Space), ButtonEvent(JoyButton.A)); // Cross
        Bind(Actions.Sprint, KeyEvent(Key.Shift)); // hold
        Bind(Actions.SprintToggle, ButtonEvent(JoyButton.LeftStick)); // L3, toggles until you stop

        // Camera (mouse look is handled directly by the player)
        Bind(Actions.LookLeft, AxisEvent(JoyAxis.RightX, -1f));
        Bind(Actions.LookRight, AxisEvent(JoyAxis.RightX, 1f));
        Bind(Actions.LookUp, AxisEvent(JoyAxis.RightY, -1f));
        Bind(Actions.LookDown, AxisEvent(JoyAxis.RightY, 1f));

        // General
        Bind(Actions.Pause, KeyEvent(Key.Escape), ButtonEvent(JoyButton.Start)); // Options

        // Targeting: hold L2/LT or right mouse, then choose an option.
        Bind(Actions.Target, MouseEvent(MouseButton.Right), AxisEvent(JoyAxis.TriggerLeft, 1f));
        Bind(Actions.Option1, KeyEvent(Key.E), ButtonEvent(JoyButton.X)); // Square
        Bind(Actions.Option2, KeyEvent(Key.F), ButtonEvent(JoyButton.Y)); // Triangle
        Bind(Actions.Option3, KeyEvent(Key.Q), ButtonEvent(JoyButton.B)); // Circle
        Bind(Actions.Option4, KeyEvent(Key.G), ButtonEvent(JoyButton.A)); // Cross

        // Build mode. Touchpad on PlayStation, View on Xbox.
        Bind(Actions.BuildToggle, KeyEvent(Key.Tab), ButtonEvent(JoyButton.Touchpad), ButtonEvent(JoyButton.Back));
        Bind(Actions.BuildNext, MouseEvent(MouseButton.WheelUp), ButtonEvent(JoyButton.DpadRight));
        Bind(Actions.BuildPrev, MouseEvent(MouseButton.WheelDown), ButtonEvent(JoyButton.DpadLeft));
        Bind(Actions.BuildRotate, KeyEvent(Key.R), ButtonEvent(JoyButton.RightShoulder)); // R1
        Bind(Actions.BuildPlace, MouseEvent(MouseButton.Left), ButtonEvent(JoyButton.A)); // Cross
        Bind(Actions.BuildRemove, KeyEvent(Key.X), ButtonEvent(JoyButton.B)); // Circle
        Bind(Actions.BuildMove, KeyEvent(Key.E), ButtonEvent(JoyButton.X)); // Square

        // Debug
        Bind(Actions.DebugMenu, KeyEvent(Key.F1), ButtonEvent(JoyButton.RightStick)); // R3

        // Menu navigation (debug menu now, other menus later). Godot's built-in ui_accept
        // has no controller button, so menus use these instead.
        Bind(Actions.MenuUp, KeyEvent(Key.Up), ButtonEvent(JoyButton.DpadUp));
        Bind(Actions.MenuDown, KeyEvent(Key.Down), ButtonEvent(JoyButton.DpadDown));
        Bind(Actions.MenuLeft, KeyEvent(Key.Left), ButtonEvent(JoyButton.DpadLeft));
        Bind(Actions.MenuRight, KeyEvent(Key.Right), ButtonEvent(JoyButton.DpadRight));
        Bind(Actions.MenuAccept, KeyEvent(Key.Enter), KeyEvent(Key.KpEnter), ButtonEvent(JoyButton.A)); // Cross
    }

    private static void Bind(StringName action, params InputEvent[] events)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action, Deadzone);
        foreach (var inputEvent in events)
            InputMap.ActionAddEvent(action, inputEvent);
    }

    private static InputEventKey KeyEvent(Key key) => new() { Device = -1, PhysicalKeycode = key };

    private static InputEventMouseButton MouseEvent(MouseButton button) =>
        new() { Device = -1, ButtonIndex = button };

    private static InputEventJoypadButton ButtonEvent(JoyButton button) =>
        new() { Device = -1, ButtonIndex = button };

    private static InputEventJoypadMotion AxisEvent(JoyAxis axis, float direction) =>
        new() { Device = -1, Axis = axis, AxisValue = direction };
}
