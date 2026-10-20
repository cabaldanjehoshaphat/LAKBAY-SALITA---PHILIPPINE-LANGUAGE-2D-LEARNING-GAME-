using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// StreakTracker
/// Static helper that keeps the player's day-streak in SaveStore (no permissions needed).
/// Call RecordPlay() whenever the player starts a minigame (SceneNavigator does this for Wordle,
/// Crossword and Word Search). A day counts once; playing on consecutive local calendar days grows
/// the streak, and missing a whole day resets it to 1 on the next play.
/// GetStreak() returns the streak to display: it is 0 once more than a day has passed without playing.
/// StreakChip reads it for the Main Menu.
/// </summary>
public static class StreakTracker
{
    private const string CountKey = "streak_count";
    private const string LastPlayKey = "streak_last_play";
    private const string BestKey = "streak_best";
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Marks today as played and updates the saved streak.</summary>
    public static void RecordPlay()
    {
        DateTime today = DateTime.Now.Date;
        DateTime last;
        bool hasLast = TryGetLastPlay(out last);

        if (hasLast && last == today)
        {
            return;
        }

        int count = SaveStore.GetInt(CountKey, 0);
        count = hasLast && last == today.AddDays(-1) ? count + 1 : 1;

        SaveStore.SetInt(CountKey, count);
        SaveStore.SetString(LastPlayKey, today.ToString(DateFormat, CultureInfo.InvariantCulture));
        if (count > SaveStore.GetInt(BestKey, 0))
        {
            SaveStore.SetInt(BestKey, count);
        }
        SaveStore.Save();
    }

    /// <summary>The longest streak the player ever reached (never lower than the current streak).</summary>
    public static int GetBestStreak()
    {
        return Mathf.Max(SaveStore.GetInt(BestKey, 0), GetStreak());
    }

    /// <summary>Forgets the streak and the best streak (used when the profile is reset).</summary>
    public static void Reset()
    {
        SaveStore.DeleteKey(CountKey);
        SaveStore.DeleteKey(LastPlayKey);
        SaveStore.DeleteKey(BestKey);
        SaveStore.Save();
    }

    /// <summary>The streak to show: 0 if the player has not played today or yesterday.</summary>
    public static int GetStreak()
    {
        DateTime last;
        if (!TryGetLastPlay(out last))
        {
            return 0;
        }

        DateTime today = DateTime.Now.Date;
        if (last == today || last == today.AddDays(-1))
        {
            return SaveStore.GetInt(CountKey, 0);
        }
        return 0;
    }

    private static bool TryGetLastPlay(out DateTime last)
    {
        return DateTime.TryParseExact(SaveStore.GetString(LastPlayKey, string.Empty), DateFormat,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out last);
    }
}
