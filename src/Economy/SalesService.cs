using System;

namespace Gecko.Economy;

/// <summary>Global access to the running sales state (set by the SalesDirector in the level).</summary>
public static class SalesService
{
    public static SalesState? Current { get; set; }

    /// <summary>Current game time in minutes (wired to the game clock).</summary>
    public static Func<double> Now { get; set; } = () => 0;
}
