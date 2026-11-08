using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>What the Survival mode reports when a run ends (the mode itself is not built yet).</summary>
public class SurvivalRun
{
    public int Rounds;
    public int Score;
    public int LongestStreak;
    /// <summary>Highest stage reached: 0 Luzon, 1 Visayas, 2 Mindanao, 3 Open sea.</summary>
    public int Stage;
    public bool JourneyLanguage;
    public bool SecondChance;
}

/// <summary>
/// SurvivalRecords
/// Prepared for the Survival mode, so that it can join the leaderboards and achievements without changing them. Saves in SaveStore (key "survival.v1", text
/// "runs,bestRounds,bestScore,longestStreak,bestStage,journeyPastMindanao,secondChance"). Nothing calls RecordRun until the mode exists: at the end of a run the
/// Survival mode calls RecordRun, and sets GameFeatures.SurvivalEnabled to true to show the Survival tab, card and badges. CloudMerge combines two copies by
/// keeping the larger value of each number, and the records follow the same "Add to account" choice as all guest progress.
/// </summary>
public static class SurvivalRecords
{
    public const string SaveKey = "survival.v1";
    public const int Fields = 7;

    public class Data
    {
        public int Runs;
        public int BestRounds;
        public int BestScore;
        public int LongestStreak;
        public int BestStage = -1;
        public bool JourneyPastMindanao;
        public bool SecondChance;
    }

    private static int[] Numbers(string text)
    {
        var n = new int[Fields];
        n[4] = -1;
        if (string.IsNullOrEmpty(text))
        {
            return n;
        }
        string[] parts = text.Split(',');
        for (int i = 0; i < Fields && i < parts.Length; i++)
        {
            int value;
            if (int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                n[i] = value;
            }
        }
        return n;
    }

    private static string Text(int[] n)
    {
        var parts = new List<string>();
        foreach (int value in n)
        {
            parts.Add(value.ToString(CultureInfo.InvariantCulture));
        }
        return string.Join(",", parts.ToArray());
    }

    public static Data Load()
    {
        int[] n = Numbers(SaveStore.GetString(SaveKey, string.Empty));
        return new Data { Runs = n[0], BestRounds = n[1], BestScore = n[2], LongestStreak = n[3], BestStage = n[4], JourneyPastMindanao = n[5] != 0, SecondChance = n[6] != 0 };
    }

    /// <summary>Called by the Survival mode when a run ends.</summary>
    public static void RecordRun(SurvivalRun run)
    {
        Data d = Load();
        int[] n =
        {
            d.Runs + 1, Mathf.Max(d.BestRounds, run.Rounds), Mathf.Max(d.BestScore, run.Score), Mathf.Max(d.LongestStreak, run.LongestStreak), Mathf.Max(d.BestStage, run.Stage),
            d.JourneyPastMindanao || (run.JourneyLanguage && run.Stage >= 3) ? 1 : 0, d.SecondChance || run.SecondChance ? 1 : 0
        };
        SaveStore.SetString(SaveKey, Text(n));
        SaveStore.Save();
    }

    /// <summary>Combines two saved copies, keeping the larger value of each number.</summary>
    public static string MergeText(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
        {
            return string.Empty;
        }
        int[] x = Numbers(a), y = Numbers(b);
        var n = new int[Fields];
        for (int i = 0; i < Fields; i++)
        {
            n[i] = Mathf.Max(x[i], y[i]);
        }
        return Text(n);
    }

    public static void Reset()
    {
        SaveStore.DeleteKey(SaveKey);
        SaveStore.Save();
    }
}
