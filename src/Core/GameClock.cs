using System;

namespace Gecko.Core;

/// <summary>
/// In-game time. 1 game day = 48 real minutes. Raises <see cref="Payday"/> every day at 6:00 AM.
/// Plain C#: ticked by <see cref="ClockNode"/>.
/// </summary>
public sealed class GameClock(double startMinutes = 8 * 60)
{
    public const double RealSecondsPerDay = 48 * 60;
    public const int MinutesPerDay = 24 * 60;
    public const int PaydayMinute = 6 * 60;

    /// <summary>Game minutes since Day 1, 00:00.</summary>
    public double TotalMinutes { get; private set; } = startMinutes;

    public int Day => (int)(TotalMinutes / MinutesPerDay) + 1;
    public int MinuteOfDay => (int)(TotalMinutes % MinutesPerDay);
    public string TimeText => $"Day {Day}  {MinuteOfDay / 60:00}:{MinuteOfDay % 60:00}";

    public event Action? Payday;

    public void Advance(double realSeconds)
    {
        double before = TotalMinutes;
        TotalMinutes += realSeconds * MinutesPerDay / RealSecondsPerDay;
        long paydaysBefore = (long)Math.Floor((before - PaydayMinute) / MinutesPerDay);
        long paydaysAfter = (long)Math.Floor((TotalMinutes - PaydayMinute) / MinutesPerDay);
        for (long i = paydaysBefore; i < paydaysAfter; i++)
            Payday?.Invoke();
    }

    /// <summary>Debug: jumps forward to the next time the clock shows <paramref name="minuteOfDay"/>.</summary>
    public void SkipTo(int minuteOfDay)
    {
        double dayStart = Math.Floor(TotalMinutes / MinutesPerDay) * MinutesPerDay;
        double target = dayStart + minuteOfDay;
        if (target <= TotalMinutes)
            target += MinutesPerDay;
        Advance((target - TotalMinutes) * RealSecondsPerDay / MinutesPerDay);
    }
}
