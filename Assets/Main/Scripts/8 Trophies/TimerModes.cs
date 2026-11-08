using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PuzzleTimerMode
{
    Normal,
    Timed
}

/// <summary>
/// TimerModes
/// The Normal / Timed choice of Wordle, Crossword and Word Search. Normal is a stopwatch with no limit; Timed is a countdown, and the puzzle ends with "Time's up" at zero
/// (nothing is saved for a puzzle that runs out of time). The choice is saved per game in SaveStore ("timer.mode.[game]"; it belongs to the device and is not uploaded).
/// Each game keeps its earlier behaviour until the player changes it: Wordle starts on Timed, Crossword and Word Search on Normal.
/// Time limits: Wordle keeps its existing countdown (5:00), Crossword allows 45 seconds per word and Word Search 30 seconds per word.
/// The score uses only the time spent, so both modes share one leaderboard and one set of best times. In Normal mode Wordle's time bonus is worked out as if the limit were 5:00.
/// AttachChips adds the two chips to a start screen while the game runs, beside the language chip (x 860 and 1018, y 60, 150 x 64 in the 1920x1080 layout), so the scenes do not need rebuilding.
/// </summary>
public static class TimerModes
{
    /// <summary>The limit that Wordle's time bonus is worked out from, in both modes.</summary>
    public const int WordleScoreSeconds = 300;

    private const int CrosswordSecondsPerWord = 45;
    private const int WordSearchSecondsPerWord = 30;

    private static readonly Color Cream = new Color32(253, 248, 238, 255);
    private static readonly Color Ink = new Color32(59, 42, 26, 255);

    private static string Key(string game)
    {
        return "timer.mode." + game;
    }

    private static PuzzleTimerMode DefaultFor(string game)
    {
        return game == ScoreRules.GameWordle ? PuzzleTimerMode.Timed : PuzzleTimerMode.Normal;
    }

    public static PuzzleTimerMode Get(string game)
    {
        return (PuzzleTimerMode)SaveStore.GetInt(Key(game), (int)DefaultFor(game));
    }

    public static void Set(string game, PuzzleTimerMode mode)
    {
        SaveStore.SetInt(Key(game), (int)mode);
        SaveStore.Save();
    }

    public static bool IsTimed(string game)
    {
        return Get(game) == PuzzleTimerMode.Timed;
    }

    public static int CrosswordLimit(int words)
    {
        return words * CrosswordSecondsPerWord;
    }

    public static int WordSearchLimit(int words)
    {
        return words * WordSearchSecondsPerWord;
    }

    /// <summary>Adds the Normal and Timed chips to a start screen. languageLabel is the text inside that screen's language chip.</summary>
    public static void AttachChips(string game, TMP_Text languageLabel)
    {
        if (languageLabel == null || languageLabel.transform.parent == null || languageLabel.transform.parent.parent == null)
        {
            return;
        }
        Transform parent = languageLabel.transform.parent.parent;
        Image normal = MakeChip(parent, "timer_normal", "Normal", 860);
        Image timed = MakeChip(parent, "timer_timed", "Timed", 1018);
        System.Action refresh = delegate
        {
            bool isTimed = IsTimed(game);
            Paint(normal, !isTimed);
            Paint(timed, isTimed);
        };
        LibraryUiKit.MakeButton(normal, delegate { Set(game, PuzzleTimerMode.Normal); refresh(); });
        LibraryUiKit.MakeButton(timed, delegate { Set(game, PuzzleTimerMode.Timed); refresh(); });
        refresh();
    }

    private static Image MakeChip(Transform parent, string name, string text, float x)
    {
        Image chip = TrophyFont.Apply(LibraryUiKit.Pill(parent, text, x, 60, 150, 64, Cream, Ink, 30));
        chip.gameObject.name = name;
        return chip;
    }

    private static void Paint(Image chip, bool selected)
    {
        chip.color = selected ? Ink : Cream;
        TextMeshProUGUI label = chip.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.color = selected ? Color.white : Ink;
            label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
