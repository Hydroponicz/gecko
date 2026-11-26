using Godot;

namespace Gecko.Core;

/// <summary>Which button glyphs to show for a controller.</summary>
public enum ControllerFamily
{
    Xbox,
    PlayStation,
    Steam,
}

/// <summary>
/// Autoload. The single place the game talks to controllers through:
/// tracks keyboard-vs-gamepad (for button prompts) and wraps controller features.
/// Today it uses Godot's built-in lightbar and rumble. A custom DualSense layer
/// (adaptive triggers, touchpad, HD haptics) will plug in here later without
/// changing any gameplay code.
/// </summary>
public partial class Gamepad : Node
{
    public static Gamepad Instance { get; private set; } = null!;

    [Signal]
    public delegate void InputModeChangedEventHandler(bool usingGamepad);

    public static readonly Color DefaultLight = new(1f, 0.55f, 0.1f); // amber

    public bool UsingGamepad { get; private set; }
    public int ActiveDevice { get; private set; }
    public ControllerFamily Family { get; private set; } = ControllerFamily.Xbox;
    public Color LightColor { get; private set; } = DefaultLight;

    public override void _EnterTree() => Instance = this;

    public override void _Ready()
    {
        Input.JoyConnectionChanged += OnJoyConnectionChanged;
        foreach (int device in Input.GetConnectedJoypads())
            OnJoyConnectionChanged(device, true);
    }

    public override void _ExitTree() => Input.JoyConnectionChanged -= OnJoyConnectionChanged;

    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventJoypadButton:
            case InputEventJoypadMotion motion when Mathf.Abs(motion.AxisValue) > 0.5f:
                if (@event.Device != ActiveDevice || !UsingGamepad)
                {
                    ActiveDevice = @event.Device;
                    Family = DetectFamily(ActiveDevice);
                }
                SetMode(true);
                break;
            case InputEventKey:
            case InputEventMouseButton:
            case InputEventMouseMotion mouse when mouse.Relative.Length() > 4f:
                SetMode(false);
                break;
        }
    }

    /// <summary>Sets the lightbar color on every connected controller that has one.</summary>
    public void SetLight(Color color)
    {
        LightColor = color;
        foreach (int device in Input.GetConnectedJoypads())
        {
            if (Input.HasJoyLight(device))
                Input.SetJoyLight(device, color);
        }
    }

    /// <summary>Weak/strong are 0..1 motor strengths, duration in seconds.</summary>
    public void Rumble(float weak, float strong, float duration)
    {
        if (Input.HasJoyVibration(ActiveDevice))
            Input.StartJoyVibration(ActiveDevice, weak, strong, duration);
    }

    /// <summary>Guesses the controller brand from its name (Godot reports names like "PS5 Controller").</summary>
    public static ControllerFamily DetectFamily(int device)
    {
        string name = Input.GetJoyName(device).ToLowerInvariant();
        string[] playStation = ["ps3", "ps4", "ps5", "dualsense", "dualshock", "playstation", "sony"];
        foreach (string hint in playStation)
        {
            if (name.Contains(hint))
                return ControllerFamily.PlayStation;
        }
        return name.Contains("steam") ? ControllerFamily.Steam : ControllerFamily.Xbox;
    }

    private void SetMode(bool gamepad)
    {
        if (gamepad == UsingGamepad)
            return;
        UsingGamepad = gamepad;
        EmitSignal(SignalName.InputModeChanged, gamepad);
    }

    private void OnJoyConnectionChanged(long device, bool connected)
    {
        if (connected && Input.HasJoyLight((int)device))
            Input.SetJoyLight((int)device, LightColor);
    }
}
