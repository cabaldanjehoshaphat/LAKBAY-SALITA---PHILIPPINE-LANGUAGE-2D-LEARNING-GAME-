using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>One raw entry exactly as stored in Assets/Main/Data/Language Dictionary/*.json (JsonUtility target).</summary>
[Serializable]
public class RawDictEntry
{
    public string word;
    public string translation;
    public string definition;
    public int letters;
    public string language;
}

[Serializable]
internal class RawDictFile
{
    public RawDictEntry[] items;
}

/// <summary>One cleaned-up dictionary entry used by the Library screen. Lang: 0 Cebuano, 1 Hiligaynon, 2 Tagalog.</summary>
public class LibraryEntry
{
    public int Lang;
    public string Raw;
    public string Word;
    public string Meaning;
    public string MeaningLower;
    public string Definition;
    public bool HasMeaning;

    public string Key { get { return Lang + ":" + Raw; } }
}

/// <summary>
/// LibraryData
/// Pure-data side of the Library (dictionary) menu: parses the three dictionary JSON files
/// (cebuano.json, hiligaynon.json, tagalog.json - native word, English translation, definition),
/// cleans odd entries (leading dashes, stray replacement characters, empty meanings), keeps each
/// language sorted A-Z for the notebook list, and answers searches with a ranking: exact native
/// headword or exact English meaning first, then headword-prefix / meaning starts-with / whole-word
/// matches, then definition matches (so both "house" and "balay" work). Also provides "did you mean" spelling help, word of the day, the same word
/// in the other languages, and favorites / recent searches saved with SaveStore.
/// Used by LibraryController; has no UI code.
/// </summary>
public class LibraryData
{
    public static readonly string[] LanguageNames = { "Cebuano", "Hiligaynon", "Tagalog" };

    private const string FavoritesKey = "library.favorites.v1";
    private const string RecentKey = "library.recent.v1";

    public readonly List<LibraryEntry>[] Entries = { new List<LibraryEntry>(), new List<LibraryEntry>(), new List<LibraryEntry>() };

    private readonly Dictionary<string, LibraryEntry> _byKey = new Dictionary<string, LibraryEntry>();
    private readonly List<string> _favorites = new List<string>();
    private readonly List<string> _recent = new List<string>();
    private Dictionary<string, int> _vocabulary;

    public class SearchResult
    {
        public readonly List<LibraryEntry> Exact = new List<LibraryEntry>();
        public readonly List<LibraryEntry> Related = new List<LibraryEntry>();
        public int Count { get { return Exact.Count + Related.Count; } }
    }

    private struct Candidate
    {
        public int Rank;
        public int Sub;
        public LibraryEntry Entry;
    }

    public int Total
    {
        get { return Entries[0].Count + Entries[1].Count + Entries[2].Count; }
    }

    public LibraryData()
    {
        LoadPrefs();
    }

    public void Load(int lang, string json)
    {
        _vocabulary = null;
        string text = json.TrimStart('﻿', ' ', '\r', '\n', '\t');
        RawDictFile file = JsonUtility.FromJson<RawDictFile>("{\"items\":" + text + "}");
        List<LibraryEntry> list = Entries[lang];
        list.Clear();
        foreach (RawDictEntry raw in file.items)
        {
            if (raw == null || string.IsNullOrEmpty(raw.word))
            {
                continue;
            }
            string upper = raw.word.Trim().ToUpperInvariant();
            if (upper.Length == 0)
            {
                continue;
            }
            string meaning = CleanText(raw.translation);
            bool has = meaning.Length >= 2;
            var entry = new LibraryEntry
            {
                Lang = lang,
                Raw = upper,
                Word = char.ToUpperInvariant(upper[0]) + upper.Substring(1).ToLowerInvariant(),
                Meaning = has ? meaning : "No meaning listed",
                MeaningLower = has ? meaning.ToLowerInvariant() : string.Empty,
                Definition = CleanText(raw.definition),
                HasMeaning = has
            };
            list.Add(entry);
            _byKey[entry.Key] = entry;
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Raw, b.Raw));
    }

    private static string CleanText(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }
        s = s.Replace("�", string.Empty).Trim();
        int i = 0;
        while (i < s.Length && (s[i] == '—' || s[i] == '–' || s[i] == '-' || s[i] == '•' || s[i] == ' '))
        {
            i++;
        }
        var sb = new StringBuilder(s.Length);
        bool space = false;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space && sb.Length > 0)
            {
                sb.Append(' ');
            }
            space = false;
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static string NormalizeQuery(string q)
    {
        return CleanText(q).ToLowerInvariant();
    }

    // ---------------------------------------------------------------- search

    /// <summary>
    /// Search inside one language. q must already be normalized (NormalizeQuery). Matches English meanings
    /// (exact meaning first, then meaning whole-word/starts-with, then definitions) and, when includeNative
    /// is on, the native headword too: an exact headword is the very first result, then headwords that
    /// start with the query (2+ letters), then headwords containing it (3+ letters).
    /// </summary>
    public SearchResult Search(int lang, string q, int maxExact, int maxRelated, bool includeNative = true)
    {
        var result = new SearchResult();
        if (string.IsNullOrEmpty(q))
        {
            return result;
        }

        string qu = q.ToUpperInvariant();
        var found = new List<Candidate>();
        List<LibraryEntry> list = Entries[lang];
        for (int i = 0; i < list.Count; i++)
        {
            LibraryEntry e = list[i];
            int rank = -1;
            int sub = 1;
            if (e.HasMeaning)
            {
                int idx = e.MeaningLower.IndexOf(q, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    if (ExactSegment(e.MeaningLower, q))
                    {
                        rank = 0;
                    }
                    else
                    {
                        int whole = FindWholeWord(e.MeaningLower, q, StringComparison.Ordinal);
                        if (whole >= 0)
                        {
                            rank = whole == 0 ? 1 : 2;
                        }
                        else if (idx == 0)
                        {
                            rank = 3;
                        }
                    }
                }
            }
            if (rank < 0 && e.Definition.Length > 0 && FindWholeWord(e.Definition, q, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rank = 4;
            }
            if (includeNative)
            {
                if (e.Raw == qu)
                {
                    rank = 0;
                    sub = 0;
                }
                else if (qu.Length >= 2 && e.Raw.StartsWith(qu, StringComparison.Ordinal))
                {
                    if (rank < 0 || rank > 1) rank = 1;
                }
                else if (qu.Length >= 3 && e.Raw.IndexOf(qu, StringComparison.Ordinal) >= 0)
                {
                    if (rank < 0 || rank > 3) rank = 3;
                }
            }
            if (rank >= 0)
            {
                found.Add(new Candidate { Rank = rank, Sub = sub, Entry = e });
            }
        }

        found.Sort((a, b) =>
        {
            if (a.Rank != b.Rank) return a.Rank.CompareTo(b.Rank);
            if (a.Sub != b.Sub) return a.Sub.CompareTo(b.Sub);
            if (a.Entry.Raw.Length != b.Entry.Raw.Length) return a.Entry.Raw.Length.CompareTo(b.Entry.Raw.Length);
            return string.CompareOrdinal(a.Entry.Raw, b.Entry.Raw);
        });

        foreach (Candidate c in found)
        {
            if (c.Rank == 0)
            {
                if (result.Exact.Count < maxExact) result.Exact.Add(c.Entry);
            }
            else if (result.Related.Count < maxRelated)
            {
                result.Related.Add(c.Entry);
            }
        }
        return result;
    }

    private static bool ExactSegment(string ml, string q)
    {
        int start = 0;
        for (int i = 0; i <= ml.Length; i++)
        {
            if (i != ml.Length && ml[i] != ',' && ml[i] != ';')
            {
                continue;
            }
            int s = start, e = i;
            while (s < e && ml[s] == ' ') s++;
            while (e > s && ml[e - 1] == ' ') e--;
            int len = e - s;
            if (len == q.Length && string.CompareOrdinal(ml, s, q, 0, len) == 0) return true;
            if (len == q.Length + 3 && string.CompareOrdinal(ml, s, "to ", 0, 3) == 0 && string.CompareOrdinal(ml, s + 3, q, 0, q.Length) == 0) return true;
            start = i + 1;
        }
        return false;
    }

    private static int FindWholeWord(string text, string q, StringComparison cmp)
    {
        int from = 0;
        while (from <= text.Length - q.Length)
        {
            int idx = text.IndexOf(q, from, cmp);
            if (idx < 0)
            {
                return -1;
            }
            bool before = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            bool after = idx + q.Length >= text.Length || !char.IsLetterOrDigit(text[idx + q.Length]);
            if (before && after)
            {
                return idx;
            }
            from = idx + 1;
        }
        return -1;
    }

    /// <summary>Closest English word from the dictionary's own meanings for a misspelled single-word query, or null.</summary>
    public string Suggest(string q)
    {
        if (string.IsNullOrEmpty(q) || q.Length < 3 || q.IndexOf(' ') >= 0)
        {
            return null;
        }
        if (_vocabulary == null)
        {
            _vocabulary = new Dictionary<string, int>();
            for (int l = 0; l < 3; l++)
            {
                foreach (LibraryEntry e in Entries[l])
                {
                    if (!e.HasMeaning) continue;
                    int start = -1;
                    string m = e.MeaningLower;
                    for (int i = 0; i <= m.Length; i++)
                    {
                        bool letter = i < m.Length && char.IsLetter(m[i]);
                        if (letter && start < 0) start = i;
                        if (!letter && start >= 0)
                        {
                            if (i - start >= 3 && i - start <= 14)
                            {
                                string t = m.Substring(start, i - start);
                                int n;
                                _vocabulary.TryGetValue(t, out n);
                                _vocabulary[t] = n + 1;
                            }
                            start = -1;
                        }
                    }
                }
            }
        }

        int maxDist = q.Length <= 4 ? 1 : 2;
        string best = null;
        int bestDist = 99, bestFreq = 0;
        foreach (KeyValuePair<string, int> kv in _vocabulary)
        {
            if (Math.Abs(kv.Key.Length - q.Length) > maxDist || kv.Key == q) continue;
            int d = Distance(q, kv.Key, maxDist);
            if (d <= maxDist && (d < bestDist || (d == bestDist && kv.Value > bestFreq)))
            {
                best = kv.Key;
                bestDist = d;
                bestFreq = kv.Value;
            }
        }
        return best;
    }

    private static int Distance(string a, string b, int limit)
    {
        int[] prev = new int[b.Length + 1];
        int[] cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            int rowMin = cur[0];
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                if (cur[j] < rowMin) rowMin = cur[j];
            }
            if (rowMin > limit) return limit + 1;
            int[] t = prev; prev = cur; cur = t;
        }
        return prev[b.Length];
    }

    // ------------------------------------------------------- derived lookups

    /// <summary>Same meaning in the other two languages (at most one entry each).</summary>
    public List<LibraryEntry> OtherLanguages(LibraryEntry e)
    {
        var list = new List<LibraryEntry>();
        if (e == null || !e.HasMeaning)
        {
            return list;
        }
        var done = new bool[3];
        string[] parts = e.MeaningLower.Split(new[] { ',', ';' });
        for (int p = 0; p < parts.Length && p < 4; p++)
        {
            string m = parts[p].Trim();
            if (m.StartsWith("to ", StringComparison.Ordinal)) m = m.Substring(3);
            if (m.Length < 2 || m.Length > 30) continue;
            for (int l = 0; l < 3; l++)
            {
                if (l == e.Lang || done[l]) continue;
                SearchResult r = Search(l, m, 1, 0, false);
                if (r.Exact.Count > 0)
                {
                    list.Add(r.Exact[0]);
                    done[l] = true;
                }
            }
        }
        list.Sort((a, b) => a.Lang.CompareTo(b.Lang));
        return list;
    }

    public LibraryEntry WordOfTheDay(DateTime day)
    {
        long dayNumber = day.Year * 366L + day.DayOfYear;
        int lang = (int)(dayNumber % 3);
        List<LibraryEntry> list = Entries[lang];
        if (list.Count == 0)
        {
            return null;
        }
        int start = (int)((dayNumber * 7919L) % list.Count);
        for (int i = 0; i < list.Count; i++)
        {
            LibraryEntry e = list[(start + i) % list.Count];
            if (e.HasMeaning && e.Raw.Length >= 4 && e.Meaning.Length <= 40 && e.Definition.Length > 0 && e.Definition.Length <= 110)
            {
                return e;
            }
        }
        return list[start];
    }

    // --------------------------------------------------- favorites and recent

    public bool IsFavorite(LibraryEntry e)
    {
        return e != null && _favorites.Contains(e.Key);
    }

    public void ToggleFavorite(LibraryEntry e)
    {
        if (e == null) return;
        if (!_favorites.Remove(e.Key)) _favorites.Add(e.Key);
        SaveStore.SetString(FavoritesKey, string.Join("|", _favorites.ToArray()));
        SaveStore.Save();
    }

    public List<LibraryEntry> Favorites()
    {
        var list = new List<LibraryEntry>();
        foreach (string key in _favorites)
        {
            LibraryEntry e;
            if (_byKey.TryGetValue(key, out e)) list.Add(e);
        }
        return list;
    }

    public List<string> Recent { get { return _recent; } }

    public void AddRecent(string q)
    {
        if (string.IsNullOrEmpty(q) || q.Length < 2) return;
        _recent.Remove(q);
        _recent.Insert(0, q);
        if (_recent.Count > 8) _recent.RemoveAt(_recent.Count - 1);
        SaveStore.SetString(RecentKey, string.Join("\n", _recent.ToArray()));
        SaveStore.Save();
    }

    private void LoadPrefs()
    {
        string fav = SaveStore.GetString(FavoritesKey, string.Empty);
        if (fav.Length > 0) _favorites.AddRange(fav.Split('|'));
        string rec = SaveStore.GetString(RecentKey, string.Empty);
        if (rec.Length > 0) _recent.AddRange(rec.Split('\n'));
    }
}
