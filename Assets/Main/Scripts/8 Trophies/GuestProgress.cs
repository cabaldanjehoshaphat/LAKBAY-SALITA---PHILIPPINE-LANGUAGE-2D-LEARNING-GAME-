using System;
using UnityEngine;

/// <summary>
/// GuestProgress
/// Handles progress that belongs to no account when a player logs in, and protects progress that could not be uploaded. CloudSave uses it during a sync:
///  - HasAnything / Stats: is there guest progress on this device, and the numbers shown on the "You have guest progress" prompt.
///  - SetAside ("Don't add"): keeps a copy of the guest progress in SaveStore (key "backup.guest", never uploaded) and clears the device, so the account's own
///    progress loads alone. RestoreSetAside brings it back after logging out.
///  - BackupFor / MergeBackupFor: when a different account logs in while the previous account's progress is still not online, that progress is copied to a
///    backup for its owner (key "backup.account.[id]") before the device is cleared, and combined back when the owner next logs in.
/// A picture chosen from the computer is not part of a copy: after a restore the avatar goes back to a built-in one.
/// </summary>
public static class GuestProgress
{
    private const string GuestKey = "backup.guest";
    private const string AccountPrefix = "backup.account.";

    private static int Count(string key)
    {
        string text = SaveStore.GetString(key, string.Empty);
        return text.Length == 0 ? 0 : text.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>The numbers of the prompt.</summary>
    public static void Stats(out int puzzles, out int words, out int bestStreak)
    {
        puzzles = Count("wordle.solved.v1") + Count("crossword.solved.v1") + Count("wordsearch.solved.v1");
        words = PlayerDatabase.Instance != null ? PlayerDatabase.Instance.WordsLearned : 0;
        bestStreak = SaveStore.GetInt("streak_best", 0);
    }

    /// <summary>True when the device holds progress that would be added to an account.</summary>
    public static bool HasAnything()
    {
        int puzzles, words, streak;
        Stats(out puzzles, out words, out streak);
        return puzzles > 0 || words > 0 || streak > 0 || Count("phrasebook.learned.v1") > 0 || Count("library.favorites.v1") > 0;
    }

    public static bool HasSetAside
    {
        get { return !string.IsNullOrEmpty(SaveStore.GetString(GuestKey, string.Empty)); }
    }

    /// <summary>"Don't add": the guest progress waits on this device and the device is cleared for the account.</summary>
    public static void SetAside()
    {
        SaveStore.SetString(GuestKey, CloudMerge.CaptureJson());
        SaveStore.Save();
        if (PlayerDatabase.Instance != null)
        {
            PlayerDatabase.Instance.ClearLocalProgress(true);
        }
    }

    /// <summary>After logging out: the guest progress that was set aside comes back.</summary>
    public static void RestoreSetAside()
    {
        string json = SaveStore.GetString(GuestKey, string.Empty);
        if (json.Length == 0)
        {
            return;
        }
        CloudMerge.ApplyJson(json);
        SaveStore.DeleteKey(GuestKey);
        SaveStore.Save();
    }

    /// <summary>Copies progress that is not online yet, for its owner, before another account clears the device.</summary>
    public static void BackupFor(string owner)
    {
        SaveStore.SetString(AccountPrefix + owner, CloudMerge.CaptureJson());
        SaveStore.Save();
    }

    /// <summary>Combines the owner's backup into this device. True when there was one.</summary>
    public static bool MergeBackupFor(string owner)
    {
        string key = AccountPrefix + owner;
        string json = SaveStore.GetString(key, string.Empty);
        if (json.Length == 0)
        {
            return false;
        }
        CloudMerge.MergeJson(json);
        SaveStore.DeleteKey(key);
        SaveStore.Save();
        return true;
    }
}
