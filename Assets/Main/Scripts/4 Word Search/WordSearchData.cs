using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WordSearchData
/// The Word Search puzzle data model and its loader. The puzzles live in one JSON file per language, Assets/Main/Data/Word Search/Puzzles/
/// wordsearch_puzzles_<language>.json, found through LevelDataRegistry: 3 languages (0 Cebuano, 1 Ilonggo, 2 Tagalog) x 7 categories x 3 puzzles (A, B, C) of 7-8 words,
/// each word with its English meaning and a short description. WordSearchData.Puzzle(language, category, puzzle) returns one puzzle; the file is read
/// once and cached. To change a word, edit the JSON (keep words 3-8 letters so they fit the 10x10 grid).
/// </summary>
[Serializable]
public class WordSearchWord
{
    public string word;
    public string meaning;
    /// <summary>A short, child-friendly sentence about the word (shown in the description card of the game).</summary>
    public string description;
}

[Serializable]
public class WordSearchPuzzleDef
{
    public string id;
    public List<WordSearchWord> words = new List<WordSearchWord>();
}

[Serializable]
public class WordSearchCategory
{
    public string name;
    public List<WordSearchPuzzleDef> puzzles = new List<WordSearchPuzzleDef>();
}

[Serializable]
public class WordSearchLanguage
{
    public string name;
    public List<WordSearchCategory> categories = new List<WordSearchCategory>();
}

[Serializable]
public class WordSearchFile
{
    public string purpose;
    public string status;
    public List<WordSearchLanguage> languages = new List<WordSearchLanguage>();
}

public static class WordSearchData
{
    private static WordSearchFile _file;

    /// <summary>Language names in the order of the language index (0 Cebuano, 1 Ilonggo, 2 Tagalog).</summary>
    public static readonly string[] LanguageNames = { "Cebuano", "Ilonggo", "Tagalog" };

    /// <summary>The whole puzzle file (null if it is missing).</summary>
    public static WordSearchFile Data
    {
        get
        {
            if (_file == null)
            {
                LevelDataRegistry registry = LevelDataRegistry.Instance;
                if (registry == null)
                {
                    return null;
                }
                // one JSON per language, in language order (see LevelDataRegistry)
                _file = new WordSearchFile();
                _file.languages.AddRange(LevelDataRegistry.Parse<WordSearchLanguage>(registry.wordSearchFiles, "Word Search"));
            }
            return _file;
        }
    }

    /// <summary>Forgets the cached file so the next call reads the JSON again (used after editing it in the editor).</summary>
    public static void Reload()
    {
        _file = null;
    }

    public static WordSearchLanguage Language(int language)
    {
        WordSearchFile file = Data;
        if (file == null || language < 0 || language >= file.languages.Count)
        {
            return null;
        }
        return file.languages[language];
    }

    public static int CategoryCount(int language)
    {
        WordSearchLanguage l = Language(language);
        return l != null ? l.categories.Count : 0;
    }

    public static WordSearchCategory Category(int language, int category)
    {
        WordSearchLanguage l = Language(language);
        if (l == null || category < 0 || category >= l.categories.Count)
        {
            return null;
        }
        return l.categories[category];
    }

    /// <summary>How many puzzles a language has in total (all categories).</summary>
    public static int TotalPuzzles(int language)
    {
        int total = 0;
        for (int c = 0; c < CategoryCount(language); c++)
        {
            WordSearchCategory category = Category(language, c);
            total += category != null ? category.puzzles.Count : 0;
        }
        return total;
    }

    public static WordSearchPuzzleDef Puzzle(int language, int category, int puzzle)
    {
        WordSearchCategory c = Category(language, category);
        if (c == null || puzzle < 0 || puzzle >= c.puzzles.Count)
        {
            return null;
        }
        return c.puzzles[puzzle];
    }
}
