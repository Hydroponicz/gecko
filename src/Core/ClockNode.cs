using Godot;

namespace Gecko.Core;

/// <summary>Autoload that runs the in-game clock (<see cref="Clock"/>). Follows game speed (debug time scale).</summary>
public partial class ClockNode : Node
{
    public static GameClock Clock { get; } = new();

    public override void _Process(double delta) => Clock.Advance(delta);
}
