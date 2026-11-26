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

    // Targeting (RDR2-style): hold to target, then pick an option.
    public static readonly StringName Target = "target";
    public static readonly StringName Option1 = "option_1";
    public static readonly StringName Option2 = "option_2";
    public static readonly StringName Option3 = "option_3";
    public static readonly StringName Option4 = "option_4";
    public static readonly StringName[] Options = [Option1, Option2, Option3, Option4];

    // Build mode (only available inside facilities)
    public static readonly StringName BuildToggle = "build_toggle";
    public static readonly StringName BuildNext = "build_next";
    public static readonly StringName BuildPrev = "build_prev";
    public static readonly StringName BuildRotate = "build_rotate";
    public static readonly StringName BuildPlace = "build_place";
    public static readonly StringName BuildRemove = "build_remove";
    public static readonly StringName BuildMove = "build_move";

    public static readonly StringName DebugMenu = "debug_menu";
}
