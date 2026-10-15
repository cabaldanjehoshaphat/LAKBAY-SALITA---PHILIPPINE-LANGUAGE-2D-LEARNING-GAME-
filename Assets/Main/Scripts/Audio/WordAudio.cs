using System.Text;
using UnityEngine;

/// <summary>
/// WordAudio
/// "Tap to hear": plays the recording of a word or phrase. A recording is an audio file in Assets/Main/Resources/Audio Words/&lt;Language&gt;/
/// (Cebuano, Ilonggo, Tagalog), named after the text in lower case with letters and digits only and spaces as underscores: "Maayong buntag"
/// is maayong_buntag, "BAKI" is baki, "Ngalan?" is ngalan. Any audio format Unity imports works (wav, mp3, ogg). If the file does not exist the
/// player sees a small "No recording for this word yet" message instead, so the buttons can be in the game before the recordings are.
/// Usage: WordAudio.Play(languageIndex, "BAKI") from a button's click handler; language index 0 Cebuano, 1 Ilonggo, 2 Tagalog.
/// The list of files to record is in Mockups/audio_recording_list.md.
/// </summary>
public static class WordAudio
{
    /// <summary>
    /// Master switch for the Tap to hear buttons. False (the default for now): the buttons are hidden on every screen and nothing plays.
    /// Set it to true once the recordings are in Resources/Audio Words and the buttons should come back.
    /// </summary>
    public static bool Enabled = false;

    private const string Folder = "Audio Words/";
    private static readonly string[] LanguageFolders = { "Cebuano", "Ilonggo", "Tagalog" };

    /// <summary>The file name (without extension) of the recording of this text.</summary>
    public static string FileName(string text)
    {
        var sb = new StringBuilder();
        bool pendingSpace = false;
        foreach (char c in (text ?? string.Empty).ToLowerInvariant())
        {
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                if (pendingSpace && sb.Length > 0)
                {
                    sb.Append('_');
                }
                pendingSpace = false;
                sb.Append(c);
            }
            else if (c == ' ' || c == '-')
            {
                pendingSpace = true;
            }
        }
        return sb.ToString();
    }

    /// <summary>The recording of this text in this language, or null if there is none yet.</summary>
    public static AudioClip Find(int language, string text)
    {
        string name = FileName(text);
        if (name.Length == 0)
        {
            return null;
        }
        language = Mathf.Clamp(language, 0, LanguageFolders.Length - 1);
        return Resources.Load<AudioClip>(Folder + LanguageFolders[language] + "/" + name);
    }

    /// <summary>True if a recording of this text exists.</summary>
    public static bool Has(int language, string text)
    {
        return Find(language, text) != null;
    }

    /// <summary>Plays the recording, or shows "No recording for this word yet".</summary>
    public static void Play(int language, string text)
    {
        if (!Enabled)
        {
            return;
        }
        AudioClip clip = Find(language, text);
        if (clip == null)
        {
            WordAudioPlayer.Instance.ShowMessage("No recording for this word yet");
            return;
        }
        WordAudioPlayer.Instance.PlayClip(clip);
    }
}
