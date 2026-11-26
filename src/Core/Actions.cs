using Godot;

namespace Gecko.Core;

/// <summary>
/// Every input action name in one place. Gameplay code only uses these,
/// never specific keys or buttons. Bindings live in <see cref="InputSetup"/>.
/// </summary>
public static class Actions
{
    public static readonly StringName MoveForward = "move_forward";
    public static readonly StringName MoveBack = "move_back";
    public static readonly StringName MoveLeft = "move_left";
    public static readonly StringName MoveRight = "move_right";
    public static readonly StringName Jump = "jump";
    public static readonly StringName Sprint = "sprint";
    public static readonly StringName SprintToggle = "sprint_toggle";

    public static readonly StringName LookLeft = "look_left";
    public static readonly StringName LookRight = "look_right";
    public static readonly StringName LookUp = "look_up";
    public static readonly StringName LookDown = "look_down";

    public static readonly StringName Pause = "pause";

    public static readonly StringName DebugLightbar = "debug_lightbar";
}
