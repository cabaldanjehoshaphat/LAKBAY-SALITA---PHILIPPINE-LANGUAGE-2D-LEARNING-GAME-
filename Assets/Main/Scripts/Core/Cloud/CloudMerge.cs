using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>One saved value of save.json, as it is kept in the online row: key and text.</summary>
[Serializable]
public class CloudEntry
{
    public string key;
    public string value;
}

/// <summary>The saved values that are synced, as the "save" column of the table public.progress.</summary>
[Serializable]
public class CloudSaveBlob
{
    public List<CloudEntry> entries = new List<CloudEntry>();
}

/// <summary>
/// CloudMerge
/// Combines the progress of THIS device with the progress saved online, so that nothing is lost when a player used two devices, and so that a
/// player who signs in on a new device gets the old progress back. CloudSave calls Merge, then ApplyLocally to keep the result on this device.
/// What is synced (SyncedKeys of save.json, and the profile player_data.json):
///  - lists of solved / learned / favorite things (Wordle, Crossword, Word Search, Phrasebook, Library favorites): everything from both (union);
///  - recent Library searches: this device's first, then the others;
///  - the day streak: the best streak is the larger one; the current streak and last play day come from the side that played last;
///  - small choices (Phrasebook language, Crossword keypad, Word Search mode): this device's value, or the online one when this device has none;
///  - the "Continue" puzzle (resume.*): this device's when it has one, otherwise the online one;
///  - the profile: name and avatar come from the online copy when signing in for the first time on this device or when the online copy was changed on
///    another device after this device's last sync, otherwise from this device; solved counts and best Crossword level are the larger ones, learned words
///    are the union, the join date is the earlier one.
/// Not synced (they belong to the device): window size and fullscreen, music and sound settings, the login itself, the picture chosen from this computer.
/// LocalHash is a short fingerprint of what would be synced; CloudSave uses it to skip syncs when nothing changed.
/// </summary>
public static class CloudMerge
{
    /// <summary>The result of a merge: what to keep here and upload, and whether it differs from the online copy.</summary>
    public class Result
    {
        public CloudSaveBlob Save;
        public PlayerData Player;
        public bool DiffersFromRemote;
    }

    private static readonly string[] UnionPipe =
    {
        "wordle.solved.v1", "crossword.solved.v1", "wordsearch.solved.v1", "phrasebook.learned.v1", "library.favorites.v1"
    };
    private const string RecentKey = "library.recent.v1";
    private static readonly string[] StreakKeys = { "streak_count", "streak_last_play", "streak_best" };
    private static readonly string[] SmallChoices = { "phrasebook.language", "crossword.keypad", "wordsearch.mode" };
    private static readonly string[] ResumeKeys = { "resume.game", "resume.language", "resume.category", "resume.index", "resume.done" };

    /// <summary>Every key that is synced.</summary>
    public static IEnumerable<string> SyncedKeys()
    {
        foreach (string k in UnionPipe) yield return k;
        yield return RecentKey;
        foreach (string k in StreakKeys) yield return k;
        foreach (string k in SmallChoices) yield return k;
        foreach (string k in ResumeKeys) yield return k;
    }

    /// <summary>Deletes every synced value from this device (used when the account logs out or another account logs in, so two accounts never mix).</summary>
    public static void ClearSyncedKeys()
    {
        foreach (string key in new List<string>(SyncedKeys()))
        {
            SaveStore.DeleteKey(key);
        }
        SaveStore.Save();
    }

    private static Dictionary<string, string> LocalSave()
    {
        var map = new Dictionary<string, string>();
        foreach (string key in SyncedKeys())
        {
            if (SaveStore.HasKey(key))
            {
                map[key] = SaveStore.GetString(key, string.Empty);
            }
        }
        return map;
    }

    private static Dictionary<string, string> ToMap(CloudSaveBlob blob)
    {
        var map = new Dictionary<string, string>();
        if (blob != null && blob.entries != null)
        {
            foreach (CloudEntry e in blob.entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.key))
                {
                    map[e.key] = e.value ?? string.Empty;
                }
            }
        }
        return map;
    }

    private static CloudSaveBlob ToBlob(Dictionary<string, string> map)
    {
        var keys = new List<string>(map.Keys);
        keys.Sort(string.CompareOrdinal);
        var blob = new CloudSaveBlob();
        foreach (string k in keys)
        {
            blob.entries.Add(new CloudEntry { key = k, value = map[k] });
        }
        return blob;
    }

    private static string Get(Dictionary<string, string> map, string key)
    {
        string v;
        return map.TryGetValue(key, out v) ? v : string.Empty;
    }

    private static string Union(string a, string b, char separator)
    {
        var seen = new HashSet<string>();
        var list = new List<string>();
        foreach (string part in (a + separator + b).Split(separator))
        {
            if (part.Length > 0 && seen.Add(part))
            {
                list.Add(part);
            }
        }
        return string.Join(separator.ToString(), list.ToArray());
    }

    private static int ToInt(string text)
    {
        int n;
        return int.TryParse(text, out n) ? n : 0;
    }

    /// <summary>Merges the online copy (null = none yet) into this device's progress.</summary>
    public static Result Merge(CloudSaveBlob remoteSave, PlayerData remotePlayer, string remoteUpdatedAt, string lastSync)
    {
        Dictionary<string, string> local = LocalSave();
        Dictionary<string, string> remote = ToMap(remoteSave);
        var merged = new Dictionary<string, string>();

        foreach (string key in UnionPipe)
        {
            string value = Union(Get(local, key), Get(remote, key), '|');
            if (value.Length > 0 || local.ContainsKey(key) || remote.ContainsKey(key)) merged[key] = value;
        }
        string recent = Union(Get(local, RecentKey), Get(remote, RecentKey), '\n');
        if (recent.Length > 0) merged[RecentKey] = recent;

        // the day streak: the best one is kept; the current one comes from the side that played last
        int best = Math.Max(ToInt(Get(local, "streak_best")), ToInt(Get(remote, "streak_best")));
        string lastL = Get(local, "streak_last_play"), lastR = Get(remote, "streak_last_play");
        int cmp = string.CompareOrdinal(lastR, lastL);
        if (cmp > 0)
        {
            merged["streak_count"] = Get(remote, "streak_count");
            merged["streak_last_play"] = lastR;
        }
        else if (lastL.Length > 0)
        {
            merged["streak_count"] = cmp == 0 ? Math.Max(ToInt(Get(local, "streak_count")), ToInt(Get(remote, "streak_count"))).ToString() : Get(local, "streak_count");
            merged["streak_last_play"] = lastL;
        }
        if (best > 0) merged["streak_best"] = best.ToString();

        foreach (string key in SmallChoices)
        {
            if (local.ContainsKey(key)) merged[key] = local[key];
            else if (remote.ContainsKey(key)) merged[key] = remote[key];
        }
        bool useLocalResume = local.ContainsKey("resume.game");
        foreach (string key in ResumeKeys)
        {
            Dictionary<string, string> source = useLocalResume ? local : remote;
            if (source.ContainsKey(key)) merged[key] = source[key];
        }

        PlayerData player = MergePlayer(remotePlayer, remoteUpdatedAt, lastSync);

        var result = new Result { Save = ToBlob(merged), Player = player };
        result.DiffersFromRemote = remoteSave == null || remotePlayer == null || Fingerprint(result.Save, result.Player) != Fingerprint(remoteSave, remotePlayer);
        return result;
    }

    private static PlayerData MergePlayer(PlayerData remote, string remoteUpdatedAt, string lastSync)
    {
        PlayerDatabase db = PlayerDatabase.Instance;
        PlayerData local = db != null ? db.ExportData() : new PlayerData();
        if (remote == null || string.IsNullOrEmpty(remote.joinedDate))   // no real online profile yet
        {
            return local;
        }
        var merged = local;
        bool remoteWins = string.IsNullOrEmpty(lastSync) || IsNewer(remoteUpdatedAt, lastSync);
        if (remoteWins)
        {
            if (!string.IsNullOrEmpty(remote.playerNameOrId)) merged.playerNameOrId = remote.playerNameOrId;
            merged.avatarIndex = remote.avatarIndex;
            merged.useCustomAvatar = remote.useCustomAvatar && AvatarLibrary.HasCustom;
        }
        if (!string.IsNullOrEmpty(remote.joinedDate) && (string.IsNullOrEmpty(merged.joinedDate) || string.CompareOrdinal(remote.joinedDate, merged.joinedDate) < 0))
        {
            merged.joinedDate = remote.joinedDate;
        }
        merged.wordleProgress = Math.Max(merged.wordleProgress, remote.wordleProgress);
        merged.crosswordProgress = Math.Max(merged.crosswordProgress, remote.crosswordProgress);
        merged.wordSearchProgress = Math.Max(merged.wordSearchProgress, remote.wordSearchProgress);
        merged.wordleByLanguage = MaxEach(merged.wordleByLanguage, remote.wordleByLanguage);
        merged.crosswordByLanguage = MaxEach(merged.crosswordByLanguage, remote.crosswordByLanguage);
        merged.wordSearchByLanguage = MaxEach(merged.wordSearchByLanguage, remote.wordSearchByLanguage);
        merged.bestCrosswordLevel = Math.Max(merged.bestCrosswordLevel, remote.bestCrosswordLevel);
        merged.learnedWords = new List<string>(Union(string.Join("|", merged.learnedWords.ToArray()),
            remote.learnedWords != null ? string.Join("|", remote.learnedWords.ToArray()) : string.Empty, '|').Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
        return merged;
    }

    private static int[] MaxEach(int[] a, int[] b)
    {
        var result = new int[3];
        for (int i = 0; i < 3; i++)
        {
            int x = a != null && i < a.Length ? a[i] : 0;
            int y = b != null && i < b.Length ? b[i] : 0;
            result[i] = Math.Max(x, y);
        }
        return result;
    }

    private static bool IsNewer(string a, string b)
    {
        DateTimeOffset da, db;
        if (DateTimeOffset.TryParse(a, null, System.Globalization.DateTimeStyles.AssumeUniversal, out da) &&
            DateTimeOffset.TryParse(b, null, System.Globalization.DateTimeStyles.AssumeUniversal, out db))
        {
            return da > db;
        }
        return false;
    }

    /// <summary>Keeps the merged progress on this device (the saved values and the profile).</summary>
    public static void ApplyLocally(Result merged)
    {
        foreach (CloudEntry e in merged.Save.entries)
        {
            SaveStore.SetString(e.key, e.value);
        }
        SaveStore.Save();
        if (PlayerDatabase.Instance != null)
        {
            PlayerDatabase.Instance.ImportData(merged.Player);
        }
    }

    // ------------------------------------------------------------------ fingerprints

    private static string Fingerprint(CloudSaveBlob save, PlayerData player)
    {
        var sb = new StringBuilder();
        var map = ToMap(save);
        var keys = new List<string>(map.Keys);
        keys.Sort(string.CompareOrdinal);
        foreach (string k in keys)
        {
            sb.Append(k).Append('=').Append(map[k]).Append('\u001f');
        }
        PlayerData copy = player ?? new PlayerData();
        if (copy.learnedWords != null)
        {
            var words = new List<string>(copy.learnedWords);
            words.Sort(string.CompareOrdinal);
            sb.Append(string.Join("|", words.ToArray()));
        }
        sb.Append(copy.playerNameOrId).Append(copy.avatarIndex).Append(copy.joinedDate)
          .Append(copy.wordleProgress).Append(copy.crosswordProgress).Append(copy.wordSearchProgress).Append(copy.bestCrosswordLevel);
        foreach (int[] arr in new[] { copy.wordleByLanguage, copy.crosswordByLanguage, copy.wordSearchByLanguage })
        {
            for (int i = 0; arr != null && i < arr.Length; i++) sb.Append(',').Append(arr[i]);
        }
        return sb.ToString();
    }

    /// <summary>A fingerprint of what this device would sync now (changes when the progress changes).</summary>
    public static string LocalHash()
    {
        PlayerDatabase db = PlayerDatabase.Instance;
        string text = Fingerprint(ToBlob(LocalSave()), db != null ? db.ExportData() : null);
        unchecked
        {
            long h = 1469598103934665603L;
            foreach (char c in text)
            {
                h = (h ^ c) * 1099511628211L;
            }
            return h.ToString("x") + ":" + text.Length;
        }
    }
}
