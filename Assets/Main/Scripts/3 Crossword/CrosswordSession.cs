using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CrosswordSession
/// Static hand-over between the Crossword category scenes: which language, category and size (0 Small, 1 Medium, 2 Large) to play. The language
/// hub sets the language, the start screen sets the category and size, and the play scene ("Crossword - Play") reads them. Language index
/// everywhere: 0 Cebuano, 1 Ilonggo, 2 Tagalog. Also holds the scene names and finds the puzzle that comes after the current one.
/// </summary>
public static class CrosswordSession
{
    public const string StartScene = "Crossword - Start";
    public const string PlayScene = "Crossword - Play";

    public static int Language;
    public static int Category;
    public static int Size;

    /// <summary>The puzzle after (category, size): the next size of the category, then the first size of the next category. False after the very last one.</summary>
    public static bool TryGetNext(int language, int category, int size, out int nextCategory, out int nextSize)
    {
        nextCategory = category;
        nextSize = size + 1;
        if (nextSize < CrosswordCategoryData.SizeCount(language, category))
        {
            return true;
        }
        nextCategory = category + 1;
        nextSize = 0;
        return nextCategory < CrosswordCategoryData.CategoryCount(language);
    }
}

/// <summary>
/// CrosswordProgressStore
/// Remembers which Crossword category puzzles the player has solved, in SaveStore ("crossword.solved.v1", entries "language:category:size"
/// joined with "|"). The start screen shows "N of 3 solved" per category from it, and the hub shows the total.
/// </summary>
public static class CrosswordProgressStore
{
    private const string SolvedKey = "crossword.solved.v1";

    private static string KeyFor(int language, int category, int size)
    {
        return language + ":" + category + ":" + size;
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

    public static bool IsSolved(int language, int category, int size)
    {
        return Load().Contains(KeyFor(language, category, size));
    }

    /// <summary>How many of a category's puzzles are solved.</summary>
    public static int SolvedCount(int language, int category, int sizeCount)
    {
        List<string> list = Load();
        int count = 0;
        for (int s = 0; s < sizeCount; s++)
        {
            if (list.Contains(KeyFor(language, category, s)))
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
        for (int c = 0; c < CrosswordCategoryData.CategoryCount(language); c++)
        {
            total += SolvedCount(language, c, CrosswordCategoryData.SizeCount(language, c));
        }
        return total;
    }

    public static void MarkSolved(int language, int category, int size)
    {
        List<string> list = Load();
        string key = KeyFor(language, category, size);
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
