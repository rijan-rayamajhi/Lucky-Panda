using System;
using System.Globalization;
using UnityEngine;

// One definition of "what day is it" for the wheel, quest resets, the login
// streak and the club stipend, plus the guard that keeps a moved device clock
// from locking any of them.
public static class DailyService
{
    static readonly DateTime Epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static int Today => (int)(DateTime.UtcNow.Date - Epoch.Date).TotalDays;
    public static int WeekIndex => Today / 7;

    public static void Boot()
    {
        var s = GameState.I;
        if (s == null) return;
        var d = s.Data;

        RepairClockDrift(d);

        int today = Today;
        bool newDay = false;

        if (d.lastDayIndex < 0)
        {
            d.lastDayIndex = today;
            d.loginStreak = 1;
            newDay = true;
        }
        else if (today > d.lastDayIndex)
        {
            d.loginStreak = (today - d.lastDayIndex == 1) ? d.loginStreak + 1 : 1;
            d.lastDayIndex = today;
            newDay = true;
        }

        if (d.loginStreak < 1) d.loginStreak = 1;

        d.lastSeenUtc = DateTime.UtcNow.ToString("o");
        s.Save();

        if (newDay) GameEvents.Raise(GameEventType.DayRollover, today);
        GameEvents.Raise(GameEventType.Login, d.loginStreak);
    }

    // A timestamp in the future can only come from the device clock having been
    // moved, and it would otherwise leave the wheel counting down for hundreds
    // of hours with no way out. Clamping is the whole fix — this is a single
    // player offline game, so there is nobody to cheat but the player.
    static void RepairClockDrift(PlayerData d)
    {
        var now = DateTime.UtcNow;

        if (TryParseUtc(d.lastWheelUtc, out var wheel) && wheel > now)
            d.lastWheelUtc = now.ToString("o");

        if (TryParseUtc(d.lastSeenUtc, out var seen) && seen > now.AddMinutes(5))
            d.lastSeenUtc = now.ToString("o");

        if (d.lastDayIndex > Today) d.lastDayIndex = Today;
        if (d.questDayIndex > Today) d.questDayIndex = -1;
        if (d.questWeekIndex > WeekIndex) d.questWeekIndex = -1;
    }

    public static bool TryParseUtc(string iso, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrEmpty(iso)) return false;
        if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            return false;
        utc = parsed.ToUniversalTime();
        return true;
    }

    /// Shared by the wheel panel and by the nav badge, so the lock rule has
    /// exactly one implementation.
    public static bool WheelReady(out TimeSpan wait)
    {
        wait = TimeSpan.Zero;
        var s = GameState.I;
        if (s == null) return true;
        if (!TryParseUtc(s.Data.lastWheelUtc, out var last)) return true;

        var next = last.AddHours(24);
        var now = DateTime.UtcNow;
        if (now >= next) return true;
        wait = next - now;
        return false;
    }

    public static string FormatWait(TimeSpan wait)
    {
        return wait.TotalHours >= 1
            ? $"{(int)wait.TotalHours:D2}h {wait.Minutes:D2}m {wait.Seconds:D2}s"
            : $"{wait.Minutes:D2}m {wait.Seconds:D2}s";
    }

    /// Days since the save was created — drives scheduled news mail.
    public static int AccountAgeDays()
    {
        var s = GameState.I;
        if (s == null) return 0;
        if (!TryParseUtc(s.Data.createdUtc, out var created)) return 0;
        return Mathf.Max(0, (int)(DateTime.UtcNow.Date - created.Date).TotalDays);
    }
}
