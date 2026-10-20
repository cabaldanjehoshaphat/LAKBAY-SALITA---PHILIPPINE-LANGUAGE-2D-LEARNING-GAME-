using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// SaveStore
/// Where the game keeps everything it saves: solved levels, learned phrases, favorites, recent searches, the day streak, the music and sound
/// settings, the display settings and the player's profile. It has the same methods as PlayerPrefs (GetInt, SetInt, GetFloat, SetFloat,
/// GetString, SetString, HasKey, DeleteKey, Save), but instead of the Windows registry it writes ordinary files into one folder:
///  - in a built game: the game's own data folder, "Lakbay-Salita_Data/SaveData" (next to Lakbay-Salita.exe), so the whole game and its saves
///    can be copied, backed up or deleted together. If that folder can not be written (for example the game is installed in Program Files)
///    it falls back to Application.persistentDataPath;
///  - in the Unity editor: the "SaveData" folder in the project root (never inside Assets).
/// The files are save.json (all the settings and lists above) and player_data.json (the profile, written by PlayerDatabase). The first time
/// the game runs with no save.json it copies the progress lists of an older version from PlayerPrefs (the audio and display settings are not
/// copied) and PlayerDatabase copies an older player_data.json from persistentDataPath. Changes are kept in memory and written to disk by Save()
/// at once, and by a small background runner every half second and when the game closes, so a slider that changes every frame does not
/// write the file every frame. Files are written to a temporary file first and then moved over the old one, so a crash can not leave half a file.
/// </summary>
public static class SaveStore
{
    private const string FileName = "save.json";
    private const string PlayerFileName = "player_data.json";
    private const string FolderName = "SaveData";

    /// <summary>The progress lists and small choices copied from PlayerPrefs the first time (audio and display settings are left out).</summary>
    private static readonly string[] MigrateStrings = { "wordle.solved.v1", "crossword.solved.v1", "wordsearch.solved.v1", "phrasebook.learned.v1", "library.favorites.v1", "library.recent.v1", "streak_last_play" };
    private static readonly string[] MigrateInts = { "phrasebook.language", "crossword.keypad", "wordsearch.mode", "streak_count", "streak_best" };

    [Serializable]
    private class Entry
    {
        public string key;
        public string value;
    }

    [Serializable]
    private class FileData
    {
        public int version = 1;
        public List<Entry> entries = new List<Entry>();
    }

    private static Dictionary<string, string> _map;
    private static string _folder;
    private static bool _dirty;
    private static float _lastWrite;

    /// <summary>The folder that holds the save files (created when needed).</summary>
    public static string Folder
    {
        get
        {
            if (_folder == null)
            {
                _folder = ResolveFolder();
            }
            return _folder;
        }
    }

    /// <summary>The full path of the profile file (player_data.json).</summary>
    public static string PlayerDataPath
    {
        get { return Path.Combine(Folder, PlayerFileName); }
    }

    /// <summary>True when there are changes that are not on disk yet.</summary>
    public static bool Dirty
    {
        get { return _dirty; }
    }

    private static string ResolveFolder()
    {
#if UNITY_EDITOR
        string root = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(root, FolderName);
#else
        string folder = Path.Combine(Application.dataPath, FolderName);
#endif
        if (CanWrite(folder))
        {
            return folder;
        }
        string fallback = Path.Combine(Application.persistentDataPath, FolderName);
        Debug.LogWarning("SaveStore: can not write to " + folder + ", saving to " + fallback + " instead.");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static bool CanWrite(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, ".write_test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Load()
    {
        if (_map != null)
        {
            return;
        }
        _map = new Dictionary<string, string>();
        string path = Path.Combine(Folder, FileName);
        try
        {
            if (File.Exists(path))
            {
                FileData data = JsonUtility.FromJson<FileData>(File.ReadAllText(path));
                if (data != null && data.entries != null)
                {
                    foreach (Entry e in data.entries)
                    {
                        if (!string.IsNullOrEmpty(e.key))
                        {
                            _map[e.key] = e.value ?? string.Empty;
                        }
                    }
                }
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("SaveStore: could not read " + path + " (" + ex.Message + "), starting with an empty save.");
        }
        Migrate();
    }

    /// <summary>First run with no save.json: takes over the progress of an older version that used PlayerPrefs.</summary>
    private static void Migrate()
    {
        bool any = false;
        foreach (string key in MigrateStrings)
        {
            if (PlayerPrefs.HasKey(key))
            {
                _map[key] = PlayerPrefs.GetString(key, string.Empty);
                any = true;
            }
        }
        foreach (string key in MigrateInts)
        {
            if (PlayerPrefs.HasKey(key))
            {
                _map[key] = PlayerPrefs.GetInt(key, 0).ToString(CultureInfo.InvariantCulture);
                any = true;
            }
        }
        if (any)
        {
            _dirty = true;
            Save();
        }
    }

    // ------------------------------------------------------------------ the PlayerPrefs-like methods

    public static bool HasKey(string key)
    {
        Load();
        return _map.ContainsKey(key);
    }

    public static string GetString(string key, string defaultValue = "")
    {
        Load();
        string v;
        return _map.TryGetValue(key, out v) ? v : defaultValue;
    }

    public static void SetString(string key, string value)
    {
        Load();
        string old;
        if (_map.TryGetValue(key, out old) && old == value)
        {
            return;
        }
        _map[key] = value ?? string.Empty;
        _dirty = true;
    }

    public static int GetInt(string key, int defaultValue = 0)
    {
        Load();
        string v;
        int n;
        if (_map.TryGetValue(key, out v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
        {
            return n;
        }
        return defaultValue;
    }

    public static void SetInt(string key, int value)
    {
        SetString(key, value.ToString(CultureInfo.InvariantCulture));
    }

    public static float GetFloat(string key, float defaultValue = 0f)
    {
        Load();
        string v;
        float f;
        if (_map.TryGetValue(key, out v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
        {
            return f;
        }
        return defaultValue;
    }

    public static void SetFloat(string key, float value)
    {
        SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
    }

    public static void DeleteKey(string key)
    {
        Load();
        if (_map.Remove(key))
        {
            _dirty = true;
        }
    }

    /// <summary>Forgets every saved value (used by the tests; the profile file is not touched).</summary>
    public static void DeleteAll()
    {
        Load();
        _map.Clear();
        _dirty = true;
        Save();
    }

    /// <summary>Writes the settings and lists to save.json now.</summary>
    public static void Save()
    {
        if (_map == null || !_dirty)
        {
            return;
        }
        var data = new FileData();
        foreach (KeyValuePair<string, string> kv in _map)
        {
            data.entries.Add(new Entry { key = kv.Key, value = kv.Value });
        }
        data.entries.Sort(delegate (Entry a, Entry b) { return string.CompareOrdinal(a.key, b.key); });
        try
        {
            WriteAtomic(Path.Combine(Folder, FileName), JsonUtility.ToJson(data, true));
            _dirty = false;
            _lastWrite = Time.realtimeSinceStartup;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("SaveStore: could not write save.json (" + ex.Message + ")");
        }
    }

    /// <summary>Writes a text file safely: to a temporary file first, then over the old file. Used for save.json and player_data.json.</summary>
    public static void WriteAtomic(string path, string text)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(tmp, path);
    }

    /// <summary>Called by the runner: writes pending changes at most every half second.</summary>
    internal static void FlushIfDue()
    {
        if (_dirty && Time.realtimeSinceStartup - _lastWrite > 0.5f)
        {
            Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateRunner()
    {
        var go = new GameObject("SaveStoreRunner");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<SaveStoreRunner>();
    }
}

/// <summary>Background part of SaveStore: writes pending changes shortly after they happen and when the game pauses or closes.</summary>
public class SaveStoreRunner : MonoBehaviour
{
    private void Update()
    {
        SaveStore.FlushIfDue();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveStore.Save();
        }
    }

    private void OnApplicationQuit()
    {
        SaveStore.Save();
    }

    private void OnDestroy()
    {
        SaveStore.Save();
    }
}
