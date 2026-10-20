using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WordleSession
/// Static hand-over between the Wordle category scenes: which language, category and level (0-based) to play. The language hub sets the
/// language, the start screen sets the category and level, and the play scene ("Wordle - Play") reads them. Language index everywhere:
/// 0 Cebuano, 1 Ilonggo, 2 Tagalog. Also holds the scene names and finds the level that comes after the current one.
/// </summary>
public static class WordleSession
{
    public const string StartScene = "Wordle - Start";
    public const string PlayScene = "Wordle - Play";

    public static int Language;
    public static int Category;
    public static int Level;

    /// <summary>The level after (category, level): the next level of the category, then the first level of the next category. False after the very last one.</summary>
    public static bool TryGetNext(int language, int category, int level, out int nextCategory, out int nextLevel)
    {
        nextCategory = category;
        nextLevel = level + 1;
        if (nextLevel < WordleData.LevelCount(language, category))
        {
            return true;
        }
        nextCategory = category + 1;
        nextLevel = 0;
        return nextCategory < WordleData.CategoryCount(language);
    }
}

/// <summary>
/// WordleProgressStore
/// Remembers which Wordle category levels the player has solved, in SaveStore ("wordle.solved.v1", entries "language:category:level"
/// joined with "|"). The start screen shows "N of 6 solved" per category from it, and the hub shows the total.
/// </summary>
public static class WordleProgressStore
{
    private const string SolvedKey = "wordle.solved.v1";

    private static string KeyFor(int language, int category, int level)
    {
        return language + ":" + category + ":" + level;
    }

    private static List<string> Load()
    {
        var list = new List<string>();
        string saved = SaveStore.GetString(SolvedKey, string.Empty);
        foreach (string part in saved.Split('|'))
        {
            if (part.Length > 0)
            {
                list.Add(part);
            }
        }
        return list;
    }

    public static bool IsSolved(int language, int category, int level)
    {
        return Load().Contains(KeyFor(language, category, level));
    }

    /// <summary>How many of a category's levels are solved.</summary>
    public static int SolvedCount(int language, int category, int levelCount)
    {
        List<string> list = Load();
        int count = 0;
        for (int l = 0; l < levelCount; l++)
        {
            if (list.Contains(KeyFor(language, category, l)))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>How many levels are solved in one language (all categories).</summary>
    public static int SolvedTotal(int language)
    {
        int total = 0;
        for (int c = 0; c < WordleData.CategoryCount(language); c++)
        {
            total += SolvedCount(language, c, WordleData.LevelCount(language, c));
        }
        return total;
    }

    public static void MarkSolved(int language, int category, int level)
    {
        List<string> list = Load();
        string key = KeyFor(language, category, level);
        if (!list.Contains(key))
        {
            list.Add(key);
            SaveStore.SetString(SolvedKey, string.Join("|", list.ToArray()));
            SaveStore.Save();
        }
    }

    /// <summary>Forgets every solved level (used when the profile is reset).</summary>
    public static void Reset()
    {
        SaveStore.DeleteKey(SolvedKey);
        SaveStore.Save();
    }
}
