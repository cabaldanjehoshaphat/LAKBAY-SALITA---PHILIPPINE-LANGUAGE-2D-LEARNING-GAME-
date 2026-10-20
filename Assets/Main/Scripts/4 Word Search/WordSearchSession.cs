using System.Collections.Generic;
using UnityEngine;

/// <summary>How a Word Search puzzle shows its word list: the words themselves, or their English meanings.</summary>
public enum WordSearchMode
{
    Classic,
    Meaning
}

/// <summary>
/// WordSearchSession
/// Static hand-over between the Word Search scenes: which language, category and puzzle (A, B, C = 0, 1, 2) to play and in which
/// mode. The language hub sets the language, the start screen sets the category, puzzle and mode, and the play scene reads them.
/// The chosen mode is remembered in SaveStore ("wordsearch.mode"). Language index everywhere: 0 Cebuano, 1 Ilonggo, 2 Tagalog.
/// Also holds the scene names and the helper that finds the puzzle after the current one.
/// </summary>
public static class WordSearchSession
{
    public const string StartScene = "WordSearch - Start";
    public const string PlayScene = "WordSearch - Play";
    private const string ModeKey = "wordsearch.mode";

    public static int Language;
    public static int Category;
    public static int Puzzle;

    /// <summary>
    /// Meaning mode is switched off for now (its button on the start screen is locked and dimmed, and Mode always reads Classic).
    /// Set this to true to turn it back on.
    /// </summary>
    public static bool MeaningModeAvailable = false;

    /// <summary>The mode for the next puzzle; saved between sessions. Always Classic while MeaningModeAvailable is false.</summary>
    public static WordSearchMode Mode
    {
        get
        {
            if (!MeaningModeAvailable)
            {
                return WordSearchMode.Classic;
            }
            return SaveStore.GetInt(ModeKey, 0) == 1 ? WordSearchMode.Meaning : WordSearchMode.Classic;
        }
        set
        {
            SaveStore.SetInt(ModeKey, value == WordSearchMode.Meaning ? 1 : 0);
            SaveStore.Save();
        }
    }

    /// <summary>The puzzle after (category, puzzle): next letter in the category, then the next category. False after the last one.</summary>
    public static bool TryGetNext(int language, int category, int puzzle, out int nextCategory, out int nextPuzzle)
    {
        WordSearchCategory c = WordSearchData.Category(language, category);
        nextCategory = category;
        nextPuzzle = puzzle + 1;
        if (c != null && nextPuzzle < c.puzzles.Count)
        {
            return true;
        }
        nextCategory = category + 1;
        nextPuzzle = 0;
        return nextCategory < WordSearchData.CategoryCount(language);
    }
}

/// <summary>
/// WordSearchProgressStore
/// Remembers which Word Search puzzles the player has solved, in SaveStore ("wordsearch.solved.v1", entries
/// "language:category:puzzle" joined with "|"). The start screen shows solved puzzles as gold chips.
/// </summary>
public static class WordSearchProgressStore
{
    private const string SolvedKey = "wordsearch.solved.v1";

    private static string KeyFor(int language, int category, int puzzle)
    {
        return language + ":" + category + ":" + puzzle;
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

    public static bool IsSolved(int language, int category, int puzzle)
    {
        return Load().Contains(KeyFor(language, category, puzzle));
    }

    /// <summary>How many of a category's puzzles are solved.</summary>
    public static int SolvedCount(int language, int category, int puzzleCount)
    {
        List<string> list = Load();
        int count = 0;
        for (int p = 0; p < puzzleCount; p++)
        {
            if (list.Contains(KeyFor(language, category, p)))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>How many puzzles are solved in one language (all categories).</summary>
    public static int SolvedTotal(int language)
    {
        int total = 0;
        for (int c = 0; c < WordSearchData.CategoryCount(language); c++)
        {
            WordSearchCategory category = WordSearchData.Category(language, c);
            total += SolvedCount(language, c, category != null ? category.puzzles.Count : 0);
        }
        return total;
    }

    public static void MarkSolved(int language, int category, int puzzle)
    {
        List<string> list = Load();
        string key = KeyFor(language, category, puzzle);
        if (!list.Contains(key))
        {
            list.Add(key);
            SaveStore.SetString(SolvedKey, string.Join("|", list.ToArray()));
            SaveStore.Save();
        }
    }

    /// <summary>Forgets every solved puzzle (used when the profile is reset).</summary>
    public static void Reset()
    {
        SaveStore.DeleteKey(SolvedKey);
        SaveStore.Save();
    }
}
