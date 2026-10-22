using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// PlayerDatabase
/// Singleton persistence manager for the player's save data (PlayerData): loads it from
/// disk on startup, exposes read/write access to identity and each game's progress, and
/// saves back to disk whenever a value changes. Survives scene loads (DontDestroyOnLoad),
/// so any scene (menu, profile screen, or a game itself) can reach it via
/// PlayerDatabase.Instance.
///
/// Instance is created on demand: the Profile scene still contains one, but Wordle and Crossword
/// can record a win even if the player never opened the Profile screen first.
///
/// To add a new game's progress later: add a field to PlayerData, then add a property
/// here following the same pattern as WordleProgress/CrosswordProgress/WordSearchProgress.
///
/// Added for the redesigned Profile screen: avatar (a built-in picture index, or a picture of the player's own, see AvatarLibrary), join date, solved counts per language, best crossword level and the
/// list of learned words. Wordle and Crossword call RecordWordleWin / RecordCrosswordWin when a puzzle is won.
/// ResetAll() starts a fresh profile (Library favorites are not touched). Word Search calls RecordWordSearchWin when a puzzle is finished.
/// </summary>
public class PlayerDatabase : MonoBehaviour
{
    private static PlayerDatabase _instance;

    /// <summary>The one PlayerDatabase; created automatically (and kept between scenes) the first time it is needed while playing.</summary>
    public static PlayerDatabase Instance
    {
        get
        {
            if (_instance == null && Application.isPlaying)
            {
                var go = new GameObject("PlayerDatabase");
                _instance = go.AddComponent<PlayerDatabase>();
            }
            return _instance;
        }
    }

    [SerializeField]
    private PlayerData data = new PlayerData();

    public PlayerData Data => data;

    /// <summary>player_data.json, in the save folder of SaveStore (Lakbay-Salita_Data/SaveData in a built game).</summary>
    private string SavePath => SaveStore.PlayerDataPath;

    /// <summary>The place of player_data.json in older versions of the game (the Windows user folder); copied once into the save folder.</summary>
    private static string LegacyPath => Path.Combine(Application.persistentDataPath, "player_data.json");

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public string PlayerNameOrId
    {
        get => data.playerNameOrId;
        set { data.playerNameOrId = value; Save(); }
    }

    public int WordleProgress
    {
        get => data.wordleProgress;
        set { data.wordleProgress = value; Save(); }
    }

    public int CrosswordProgress
    {
        get => data.crosswordProgress;
        set { data.crosswordProgress = value; Save(); }
    }

    public int WordSearchProgress
    {
        get => data.wordSearchProgress;
        set { data.wordSearchProgress = value; Save(); }
    }

    public int AvatarIndex
    {
        get => data.avatarIndex;
        set { data.avatarIndex = Mathf.Max(0, value); Save(); }
    }

    /// <summary>True when the profile picture is the one the player chose from their computer (see AvatarLibrary).</summary>
    public bool UseCustomAvatar
    {
        get => data.useCustomAvatar;
        set { data.useCustomAvatar = value; Save(); }
    }

    /// <summary>Day the profile was created (today if it is not known).</summary>
    public DateTime JoinedDate
    {
        get
        {
            DateTime parsed;
            if (DateTime.TryParseExact(data.joinedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                return parsed;
            }
            return DateTime.Now.Date;
        }
    }

    public int BestCrosswordLevel => data.bestCrosswordLevel;

    /// <summary>How many different words the player has learned so far.</summary>
    public int WordsLearned => data.learnedWords.Count;

    /// <summary>Wordle words solved in one language (0 Cebuano, 1 Ilonggo, 2 Tagalog).</summary>
    public int WordleSolved(int language)
    {
        return language >= 0 && language < data.wordleByLanguage.Length ? data.wordleByLanguage[language] : 0;
    }

    /// <summary>Crossword puzzles finished in one language (0 Cebuano, 1 Ilonggo, 2 Tagalog).</summary>
    public int CrosswordSolved(int language)
    {
        return language >= 0 && language < data.crosswordByLanguage.Length ? data.crosswordByLanguage[language] : 0;
    }

    /// <summary>Word Search puzzles finished in one language (0 Cebuano, 1 Ilonggo, 2 Tagalog).</summary>
    public int WordSearchSolved(int language)
    {
        return language >= 0 && language < data.wordSearchByLanguage.Length ? data.wordSearchByLanguage[language] : 0;
    }

    /// <summary>Counts one finished Word Search puzzle and remembers every word of it as learned.</summary>
    public void RecordWordSearchWin(int language, IEnumerable<string> words)
    {
        if (language < 0 || language > 2)
        {
            return;
        }
        data.wordSearchProgress++;
        data.wordSearchByLanguage[language]++;
        if (words != null)
        {
            foreach (string word in words)
            {
                AddLearned(language, word);
            }
        }
        Save();
    }

    /// <summary>Counts one solved Wordle word and remembers the word as learned.</summary>
    public void RecordWordleWin(int language, string word)
    {
        if (language < 0 || language > 2)
        {
            return;
        }
        data.wordleProgress++;
        data.wordleByLanguage[language]++;
        AddLearned(language, word);
        Save();
    }

    /// <summary>Counts one finished Crossword puzzle, remembers every answer as learned and updates the best level.</summary>
    public void RecordCrosswordWin(int language, int level, IEnumerable<string> words)
    {
        if (language < 0 || language > 2)
        {
            return;
        }
        data.crosswordProgress++;
        data.crosswordByLanguage[language]++;
        data.bestCrosswordLevel = Mathf.Max(data.bestCrosswordLevel, level);
        if (words != null)
        {
            foreach (string word in words)
            {
                AddLearned(language, word);
            }
        }
        Save();
    }

    private void AddLearned(int language, string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return;
        }
        string key = language + ":" + word.Trim().ToUpperInvariant();
        if (!data.learnedWords.Contains(key))
        {
            data.learnedWords.Add(key);
        }
    }

    /// <summary>Starts a fresh profile: name, avatar, solved counts, learned words and the day streak are cleared. Library favorites stay.</summary>
    public void ResetAll()
    {
        data = new PlayerData();
        data.joinedDate = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        AvatarLibrary.DeleteCustom();
        ResumeStore.Clear();
        StreakTracker.Reset();
        WordSearchProgressStore.Reset();
        WordleProgressStore.Reset();
        CrosswordProgressStore.Reset();
        PhraseProgressStore.Reset();
        Save();
    }

    /// <summary>Loads player_data.json from disk, or starts fresh defaults if none exists yet.</summary>
    public void Load()
    {
        if (!File.Exists(SavePath) && File.Exists(LegacyPath))
        {
            try
            {
                SaveStore.WriteAtomic(SavePath, File.ReadAllText(LegacyPath));   // take over the profile of an older version
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("PlayerDatabase: could not copy the old profile (" + ex.Message + ")");
            }
        }
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<PlayerData>(json);
        }
        else
        {
            data = new PlayerData();
        }

        // Older save files (and the first run) miss the newer fields.
        if (data.wordleByLanguage == null || data.wordleByLanguage.Length != 3) data.wordleByLanguage = new int[3];
        if (data.crosswordByLanguage == null || data.crosswordByLanguage.Length != 3) data.crosswordByLanguage = new int[3];
        if (data.wordSearchByLanguage == null || data.wordSearchByLanguage.Length != 3) data.wordSearchByLanguage = new int[3];
        if (data.learnedWords == null) data.learnedWords = new List<string>();
        if (string.IsNullOrEmpty(data.joinedDate))
        {
            data.joinedDate = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            Save();
        }
    }

    /// <summary>Writes the current PlayerData to disk as JSON.</summary>
    public void Save()
    {
        string json = JsonUtility.ToJson(data, true);
        SaveStore.WriteAtomic(SavePath, json);
    }
}
