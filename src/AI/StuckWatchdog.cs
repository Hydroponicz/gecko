namespace Gecko.AI;

/// <summary>
/// Universal NPC fail-safe, used by every NPC that moves (workers now; civilians, police, gangs later).
/// The NPC reports <see cref="Progress"/> whenever it does something meaningful (reaches a new spot,
/// finishes an action, or is legitimately idle). If it goes <see cref="TimeoutSeconds"/> without progress,
/// <see cref="Tick"/> returns true and the NPC must be teleported to a nearby safe, navigable spot and
/// have its task reset.
/// </summary>
public sealed class StuckWatchdog(float timeoutSeconds = StuckWatchdog.DefaultTimeoutSeconds)
{
    /// <summary>2 minutes: the agreed worst-case limit for any NPC being stuck.</summary>
    public const float DefaultTimeoutSeconds = 120f;

    public float TimeoutSeconds { get; } = timeoutSeconds;
    public float SecondsWithoutProgress { get; private set; }

    public void Progress() => SecondsWithoutProgress = 0f;

    /// <summary>Returns true once when the NPC has been stuck too long (and restarts the count).</summary>
    public bool Tick(float delta)
    {
        SecondsWithoutProgress += delta;
        if (SecondsWithoutProgress < TimeoutSeconds)
            return false;
        SecondsWithoutProgress = 0f;
        return true;
    }
}
