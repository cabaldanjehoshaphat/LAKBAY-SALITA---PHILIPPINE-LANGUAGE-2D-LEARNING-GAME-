using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>One finished puzzle, as the play screens report it to ResultsStore.</summary>
public class PuzzleResult
{
    public string Game;
    public int Language;
    public int Category;
    public int Level;
    public int Score;
    public int Seconds;
    public int SecondsLeft;
    public int TriesLeft;
    public int Hints;
    public int Checks;
    public int Reveals;
    public int Words;
    public bool FirstTry;
    public bool Meaning;
}

/// <summary>What is saved for one puzzle: its best result of all time (Week = ResultsStore.AllTime) or of one week.</summary>
public class PuzzleRecord
{
    public string Key;
    public string Week;
    public int Score;
    public int Seconds;
    public int Fastest;
    public int TriesLeft;
    public int SecondsLeft;
    public int Hints;
    public int Checks;
    public int Reveals;
    public int Words;
    public bool FirstTry;
    public bool Meaning;

    public string Game { get { int i = Key.IndexOf(':'); return i > 0 ? Key.Substring(0, i) : Key; } }
    public int Language { get { return Part(1); } }
    public int Category { get { return Part(2); } }
    public int Level { get { return Part(3); } }

    private int Part(int index)
    {
        string[] parts = Key.Split(':');
        int value;
        return index < parts.Length && int.TryParse(parts[index], out value) ? value : 0;
    }
}

/// <summary>
/// ResultsStore
/// Saves the results of finished puzzles in SaveStore (key "records.v1"), so scores and personal bests work with no internet and no account. For each puzzle
/// it keeps the best result of all time and the best result of each of the last 8 weeks. A week starts on Monday. Text format, one entry per puzzle and week:
/// "wordle:0:1:3@2026-11-02=score,seconds,fastest,triesLeft,secondsLeft,hints,checks,reveals,words,firstTry,meaning", entries joined with "|".
/// Playing a puzzle again can raise its score but never counts twice. CloudMerge combines two copies with MergeText (the higher score of each entry wins).
/// LeaderboardService uploads the entries; My records, the achievements and the start screens read them. Puzzle keys are "game:language:category:level".
/// </summary>
public static class ResultsStore
{
    public const string SaveKey = "records.v1";
    public const string AllTime = "all";

    /// <summary>A hidden entry that remembers the best week ever, even after that week is older than the 8 weeks kept.</summary>
    private const string BestWeekKey = "_bestweek";
    private const int WeeksKept = 8;

    /// <summary>The week of uploaded all-time records whose own week is no longer kept (a Monday long ago, so it never counts for a weekly board).</summary>
    public const string ArchiveWeek = "2000-01-03";

    public class Outcome
    {
        public bool IsNewBest;
        public int PreviousBest;
    }

    public class GameSummary
    {
        public int BestScore;
        public string BestKey;
        public int Fastest;
        public string FastestKey;
        public int NoHintWins;
        public int FirstTries;
        public int Puzzles;
    }

    public class WeekPoints
    {
        public DateTime Monday;
        public int Points;
    }

    // ------------------------------------------------------------------ weeks

    public static DateTime MondayOf(DateTime day)
    {
        day = day.Date;
        int fromMonday = ((int)day.DayOfWeek + 6) % 7;
        return day.AddDays(-fromMonday);
    }

    public static string WeekText(DateTime monday)
    {
        return monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static string ThisWeek
    {
        get { return WeekText(MondayOf(DateTime.Now)); }
    }

    /// <summary>Days until the current week ends (1 on Sunday, 7 on Monday).</summary>
    public static int DaysLeftInWeek
    {
        get { return 7 - ((int)DateTime.Now.DayOfWeek + 6) % 7; }
    }

    public static string KeyFor(string game, int language, int category, int level)
    {
        return game + ":" + language + ":" + category + ":" + level;
    }

    // ------------------------------------------------------------------ text

    private static string Format(PuzzleRecord r)
    {
        return r.Key + "@" + r.Week + "=" + r.Score + "," + r.Seconds + "," + r.Fastest + "," + r.TriesLeft + "," + r.SecondsLeft + "," + r.Hints + "," + r.Checks + "," +
               r.Reveals + "," + r.Words + "," + (r.FirstTry ? 1 : 0) + "," + (r.Meaning ? 1 : 0);
    }

    private static PuzzleRecord Parse(string line)
    {
        int at = line.IndexOf('@');
        int eq = line.IndexOf('=');
        if (at <= 0 || eq <= at)
        {
            return null;
        }
        string[] v = line.Substring(eq + 1).Split(',');
        if (v.Length < 11)
        {
            return null;
        }
        int[] n = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            if (!int.TryParse(v[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i]))
            {
                return null;
            }
        }
        return new PuzzleRecord
        {
            Key = line.Substring(0, at),
            Week = line.Substring(at + 1, eq - at - 1),
            Score = n[0], Seconds = n[1], Fastest = n[2], TriesLeft = n[3], SecondsLeft = n[4], Hints = n[5], Checks = n[6], Reveals = n[7], Words = n[8],
            FirstTry = n[9] != 0, Meaning = n[10] != 0
        };
    }

    private static SortedDictionary<string, PuzzleRecord> ParseAll(string text)
    {
        var map = new SortedDictionary<string, PuzzleRecord>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(text))
        {
            return map;
        }
        foreach (string line in text.Split('|'))
        {
            PuzzleRecord r = line.Length > 0 ? Parse(line) : null;
            if (r != null)
            {
                Upsert(map, r);
            }
        }
        return map;
    }

    private static void Upsert(SortedDictionary<string, PuzzleRecord> map, PuzzleRecord r)
    {
        string id = r.Key + "@" + r.Week;
        PuzzleRecord old;
        if (!map.TryGetValue(id, out old))
        {
            map[id] = r;
            return;
        }
        int fastest = old.Fastest > 0 && r.Fastest > 0 ? Mathf.Min(old.Fastest, r.Fastest) : Mathf.Max(old.Fastest, r.Fastest);
        bool first = old.FirstTry || r.FirstTry;
        PuzzleRecord keep = r.Score > old.Score ? r : old;
        keep.Fastest = fastest;
        keep.FirstTry = first;
        map[id] = keep;
    }

    private static string Join(SortedDictionary<string, PuzzleRecord> map)
    {
        DateTime oldest = MondayOf(DateTime.Now).AddDays(-7 * (WeeksKept - 1));
        string oldestText = WeekText(oldest);
        var lines = new List<string>();
        foreach (PuzzleRecord r in map.Values)
        {
            if (r.Week == AllTime || string.CompareOrdinal(r.Week, oldestText) >= 0)
            {
                lines.Add(Format(r));
            }
        }
        return string.Join("|", lines.ToArray());
    }

    /// <summary>Combines two saved copies: for each puzzle and week, the higher score; the fastest time and a first-try flag are kept from either.</summary>
    public static string MergeText(string a, string b)
    {
        SortedDictionary<string, PuzzleRecord> map = ParseAll(a);
        foreach (PuzzleRecord r in ParseAll(b).Values)
        {
            Upsert(map, r);
        }
        return Join(map);
    }

    private static SortedDictionary<string, PuzzleRecord> Load()
    {
        return ParseAll(SaveStore.GetString(SaveKey, string.Empty));
    }

    // ------------------------------------------------------------------ saving

    /// <summary>Saves a finished puzzle and says whether it beat the player's best score for that puzzle.</summary>
    public static Outcome Record(PuzzleResult result)
    {
        SortedDictionary<string, PuzzleRecord> map = Load();
        string key = KeyFor(result.Game, result.Language, result.Category, result.Level);
        PuzzleRecord previous;
        map.TryGetValue(key + "@" + AllTime, out previous);
        string week = ThisWeek;
        Upsert(map, Make(result, key, AllTime));
        Upsert(map, Make(result, key, week));

        int points = SumWeek(map, week, null, -1);
        Upsert(map, new PuzzleRecord { Key = BestWeekKey, Week = AllTime, Score = points });

        SaveStore.SetString(SaveKey, Join(map));
        SaveStore.Save();
        return new Outcome { IsNewBest = previous == null || result.Score > previous.Score, PreviousBest = previous != null ? previous.Score : 0 };
    }

    private static PuzzleRecord Make(PuzzleResult r, string key, string week)
    {
        return new PuzzleRecord
        {
            Key = key, Week = week, Score = r.Score, Seconds = r.Seconds, Fastest = r.Seconds, TriesLeft = r.TriesLeft, SecondsLeft = r.SecondsLeft,
            Hints = r.Hints, Checks = r.Checks, Reveals = r.Reveals, Words = r.Words, FirstTry = r.FirstTry, Meaning = r.Meaning
        };
    }

    public static void Reset()
    {
        SaveStore.DeleteKey(SaveKey);
        SaveStore.Save();
    }

    // ------------------------------------------------------------------ reading

    private static int SumWeek(SortedDictionary<string, PuzzleRecord> map, string week, string game, int language)
    {
        int sum = 0;
        foreach (PuzzleRecord r in map.Values)
        {
            if (r.Week == week && !r.Key.StartsWith("_", StringComparison.Ordinal) && (game == null || r.Game == game) && (language < 0 || r.Language == language))
            {
                sum += r.Score;
            }
        }
        return sum;
    }

    /// <summary>The points of one week (the sum of the best score of each puzzle that week).</summary>
    public static int PointsOfWeek(string week, string game = null, int language = -1)
    {
        return SumWeek(Load(), week, game, language);
    }

    /// <summary>The player's best week ever (never lower than this week).</summary>
    public static int BestWeek()
    {
        SortedDictionary<string, PuzzleRecord> map = Load();
        PuzzleRecord best;
        int saved = map.TryGetValue(BestWeekKey + "@" + AllTime, out best) ? best.Score : 0;
        return Mathf.Max(saved, SumWeek(map, ThisWeek, null, -1));
    }

    /// <summary>The last weeks, oldest first, the current week last.</summary>
    public static List<WeekPoints> RecentWeeks(int count)
    {
        SortedDictionary<string, PuzzleRecord> map = Load();
        var list = new List<WeekPoints>();
        DateTime monday = MondayOf(DateTime.Now);
        for (int i = count - 1; i >= 0; i--)
        {
            DateTime m = monday.AddDays(-7 * i);
            list.Add(new WeekPoints { Monday = m, Points = SumWeek(map, WeekText(m), null, -1) });
        }
        return list;
    }

    /// <summary>The all-time record of each puzzle played.</summary>
    public static List<PuzzleRecord> AllRecords()
    {
        var list = new List<PuzzleRecord>();
        foreach (PuzzleRecord r in Load().Values)
        {
            if (r.Week == AllTime && !r.Key.StartsWith("_", StringComparison.Ordinal))
            {
                list.Add(r);
            }
        }
        return list;
    }

    public static PuzzleRecord Best(string game, int language, int category, int level)
    {
        PuzzleRecord r;
        return Load().TryGetValue(KeyFor(game, language, category, level) + "@" + AllTime, out r) ? r : null;
    }

    public static GameSummary Summary(string game)
    {
        var s = new GameSummary();
        foreach (PuzzleRecord r in AllRecords())
        {
            if (r.Game != game)
            {
                continue;
            }
            s.Puzzles++;
            if (r.Score > s.BestScore)
            {
                s.BestScore = r.Score;
                s.BestKey = r.Key;
            }
            if (r.Fastest > 0 && (s.Fastest == 0 || r.Fastest < s.Fastest))
            {
                s.Fastest = r.Fastest;
                s.FastestKey = r.Key;
            }
            if (r.Hints == 0 && r.Reveals == 0)
            {
                s.NoHintWins++;
            }
            if (r.FirstTry)
            {
                s.FirstTries++;
            }
        }
        return s;
    }

    /// <summary>A puzzle worth playing again: the slowest one where help was used, otherwise the slowest one of all. Null when nothing was played.</summary>
    public static PuzzleRecord BeatYourBest()
    {
        PuzzleRecord pick = null;
        bool pickHelped = false;
        foreach (PuzzleRecord r in AllRecords())
        {
            bool helped = r.Hints > 0 || r.Reveals > 0;
            if (pick == null || (helped && !pickHelped) || (helped == pickHelped && r.Fastest > pick.Fastest))
            {
                pick = r;
                pickHelped = helped;
            }
        }
        return pick;
    }

    /// <summary>Every record for the online board: the kept weeks, and the all-time records whose week is gone (sent under ArchiveWeek).</summary>
    public static List<PuzzleRecord> ForUpload()
    {
        SortedDictionary<string, PuzzleRecord> map = Load();
        var list = new List<PuzzleRecord>();
        var hasWeek = new HashSet<string>();
        foreach (PuzzleRecord r in map.Values)
        {
            if (r.Week != AllTime)
            {
                list.Add(r);
                hasWeek.Add(r.Key);
            }
        }
        foreach (PuzzleRecord r in map.Values)
        {
            if (r.Week == AllTime && !r.Key.StartsWith("_", StringComparison.Ordinal) && !hasWeek.Contains(r.Key))
            {
                r.Week = ArchiveWeek;
                list.Add(r);
            }
        }
        return list;
    }

    // ------------------------------------------------------------------ text for the screens

    public static string Clock(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return (seconds / 60) + ":" + (seconds % 60).ToString("00");
    }

    public static string GameName(string game)
    {
        switch (game)
        {
            case ScoreRules.GameWordle: return "Wordle";
            case ScoreRules.GameCrossword: return "Crossword";
            case ScoreRules.GameWordSearch: return "Word Search";
            case ScoreRules.GameSurvival: return "Survival";
        }
        return game;
    }

    /// <summary>"Animals · Lv 2", "Animals · Medium" or "Family · Level 2".</summary>
    public static string Describe(PuzzleRecord r)
    {
        if (r == null)
        {
            return string.Empty;
        }
        string[] sizes = { "Small", "Medium", "Large" };
        switch (r.Game)
        {
            case ScoreRules.GameWordle:
                WordleCategoryDef wc = WordleData.Category(r.Language, r.Category);
                return (wc != null ? wc.name : "Wordle") + " · Lv " + (r.Level + 1);
            case ScoreRules.GameCrossword:
                CrosswordCategoryDef cc = CrosswordCategoryData.Category(r.Language, r.Category);
                return (cc != null ? cc.name : "Crossword") + " · " + sizes[Mathf.Clamp(r.Level, 0, 2)];
            case ScoreRules.GameWordSearch:
                WordSearchCategory wsc = WordSearchData.Category(r.Language, r.Category);
                return (wsc != null ? wsc.name : "Word Search") + " · Level " + (r.Level + 1);
        }
        return r.Key;
    }
}
