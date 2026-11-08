using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScoreRules
/// The one place that holds the score formulas of the mini-games. The result panel, My records and the achievements use them, and the database function
/// public.score_of (Documentation/Supabase/Leaderboard_Setup.sql) works the score out again with the same rules, so a change here must also be made there.
///  - Wordle: 100 for solving, + 20 per try left, + 1 per 5 seconds left (up to 60), - 15 per hint.
///  - Crossword: 20 per word, + 1 per 5 seconds under par (30 seconds per word, up to 60), - 10 per hint, - 5 per Check, - 40 per Reveal.
///  - Word Search: 15 per word, + 1 per 5 seconds under par (20 seconds per word, up to 60), - 10 per hint; x 1.5 in Meaning mode.
///  - Survival (prepared, the mode is not built yet): one correct round = 50 + seconds left + 5 per correct answer in a row.
/// A score is never below 0. Each formula returns the lines shown on the result panel and the total.
/// </summary>
public static class ScoreRules
{
    public const string GameWordle = "wordle";
    public const string GameCrossword = "crossword";
    public const string GameWordSearch = "wordsearch";
    public const string GameSurvival = "survival";

    private const int MaxTimeBonus = 60;

    public class Line
    {
        public string Label;
        public int Points;
    }

    public class Score
    {
        public readonly List<Line> Lines = new List<Line>();
        public int Total;

        public void Add(string label, int points)
        {
            Lines.Add(new Line { Label = label, Points = points });
        }
    }

    private static Score Finish(Score score)
    {
        int sum = 0;
        foreach (Line line in score.Lines)
        {
            sum += line.Points;
        }
        score.Total = Mathf.Max(0, sum);
        return score;
    }

    private static string Clock(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return (seconds / 60) + ":" + (seconds % 60).ToString("00");
    }

    private static int TimeBonus(int secondsCounted)
    {
        return Mathf.Clamp(secondsCounted / 5, 0, MaxTimeBonus);
    }

    private static void AddTools(Score score, string name, int count, int pointsEach)
    {
        if (count > 0)
        {
            score.Add(count + " " + name + (count == 1 ? " used" : "s used"), -count * pointsEach);
        }
    }

    /// <summary>normalSeconds is the time used when the puzzle was played in Normal mode (it only changes the line shown); -1 in Timed mode.</summary>
    public static Score Wordle(int triesLeft, int secondsLeft, int hints, int normalSeconds = -1)
    {
        var score = new Score();
        score.Add("Solved", 100);
        triesLeft = Mathf.Max(0, triesLeft);
        score.Add(triesLeft + (triesLeft == 1 ? " try left" : " tries left") + "  (" + triesLeft + " x 20)", triesLeft * 20);
        secondsLeft = Mathf.Max(0, secondsLeft);
        score.Add(normalSeconds >= 0 ? "Time " + Clock(normalSeconds) : "Time left " + Clock(secondsLeft), TimeBonus(secondsLeft));
        AddTools(score, "hint", hints, 15);
        return Finish(score);
    }

    public static Score Crossword(int words, int seconds, int hints, int checks, int reveals)
    {
        var score = new Score();
        score.Add(words + " words  (" + words + " x 20)", words * 20);
        score.Add("Time " + Clock(seconds), TimeBonus(words * 30 - seconds));
        AddTools(score, "hint", hints, 10);
        AddTools(score, "Check", checks, 5);
        AddTools(score, "Reveal", reveals, 40);
        return Finish(score);
    }

    public static Score WordSearch(int words, int seconds, int hints, bool meaningMode)
    {
        var score = new Score();
        score.Add(words + " words  (" + words + " x 15)", words * 15);
        score.Add("Time " + Clock(seconds), TimeBonus(words * 20 - seconds));
        AddTools(score, "hint", hints, 10);
        Finish(score);
        if (meaningMode)
        {
            int bonus = score.Total * 3 / 2 - score.Total;
            score.Add("Meaning mode  (x 1.5)", bonus);
            Finish(score);
        }
        return score;
    }

    /// <summary>Prepared for the Survival mode: the score of one correct round. correctInARow counts the correct answers before this one.</summary>
    public static int SurvivalRound(int secondsLeft, int correctInARow)
    {
        return 50 + Mathf.Max(0, secondsLeft) + 5 * Mathf.Max(0, correctInARow);
    }
}
