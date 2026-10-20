using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LibraryFavoritesStore
/// Small static helper that reads and writes the Library's favorite words directly in SaveStore, using the exact same
/// key and format as LibraryData ("library.favorites.v1", entries "LanguageIndex:WORD" joined with "|"; language index 0 =
/// Cebuano, 1 = Hiligaynon/Ilonggo, 2 = Tagalog). It lets other screens (the Crossword result card) star a word without
/// loading the whole dictionary; the Library shows the favorite the next time it opens.
/// </summary>
public static class LibraryFavoritesStore
{
    private const string FavoritesKey = "library.favorites.v1";

    private static string KeyFor(int language, string word)
    {
        return language + ":" + (word ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static List<string> Load()
    {
        var list = new List<string>();
        string saved = SaveStore.GetString(FavoritesKey, string.Empty);
        if (saved.Length > 0)
        {
            foreach (string part in saved.Split('|'))
            {
                if (part.Length > 0)
                {
                    list.Add(part);
                }
            }
        }
        return list;
    }

    /// <summary>How many words are saved as favorites (all languages).</summary>
    public static int Count()
    {
        return Load().Count;
    }

    /// <summary>True if this word is saved as a favorite in that language.</summary>
    public static bool IsFavorite(int language, string word)
    {
        return Load().Contains(KeyFor(language, word));
    }

    /// <summary>Adds the word to the favorites, or removes it if it was already there. Returns the new state (true = favorite).</summary>
    public static bool Toggle(int language, string word)
    {
        List<string> list = Load();
        string key = KeyFor(language, word);
        bool nowFavorite;
        if (list.Remove(key))
        {
            nowFavorite = false;
        }
        else
        {
            list.Add(key);
            nowFavorite = true;
        }
        SaveStore.SetString(FavoritesKey, string.Join("|", list.ToArray()));
        SaveStore.Save();
        return nowFavorite;
    }
}
