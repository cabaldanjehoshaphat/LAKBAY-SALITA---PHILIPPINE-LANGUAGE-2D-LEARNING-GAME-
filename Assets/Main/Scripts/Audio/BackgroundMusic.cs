using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// BackgroundMusic
/// Plays the background music for every screen. When a scene with a different track loads, the old track stops right away (0.08 s
/// anti-click fade) and only then does the new scene's track start from the beginning (0.3 s fade-in), so two tracks never play over
/// each other. Panels and popups inside a scene don't change the music. It creates itself before the
/// first scene loads (RuntimeInitializeOnLoadMethod), survives scene loads (DontDestroyOnLoad), and needs nothing placed in any scene.
/// The tracks are AudioClips in Assets/Main/Resources/Audio Soundtrack/. Which track a scene gets is decided by its name (see Tracks):
///   "1 Main_menu" -> Lakbay, "2 Profile Icon" -> Ako Ni, "3 Language_Selection_Menu" -> Tulay, "4 Library_menu" -> Tahimik,
///   any Wordle scene -> Hula-Hula, any Crossword scene -> Pahalang Pababa, any Word Search scene -> Pangita.
/// Moving between two scenes with the same track (e.g. "Wordle - Start" to "Wordle - Play") keeps the music playing without a restart.
/// A scene that matches no rule keeps whatever is already playing. The volume is saved in PlayerPrefs ("MusicVolume"); call
/// BackgroundMusic.SetVolume(0..1) from a settings slider. Music on/off is saved too ("MusicMuted"): SetMuted fades the music out or
/// back in, and MutedChanged tells other scripts when music is turned on or off (the main menu's SettingsPanel sets it from its volume slider: 0 = off). While muted the track keeps
/// playing silently, so turning music back on continues where it was. If a scene has no AudioListener, this object enables its own so
/// the music is still heard.
/// </summary>
public class BackgroundMusic : MonoBehaviour
{
    private const string Folder = "Audio Soundtrack/";
    private const string VolumeKey = "MusicVolume";
    private const string MutedKey = "MusicMuted";
    private const float DefaultVolume = 0.6f;
    private const float StopSeconds = 0.08f;  // old track: near-instant stop when a new scene loads
    private const float StartSeconds = 0.3f;  // new track: short fade-in after the old one has stopped
    private const float MuteFadeSeconds = 0.35f;

    /// <summary>Scene-name keyword (lower case) -> clip name in Resources/Audio Soundtrack. The first match wins.</summary>
    private static readonly string[,] Tracks =
    {
        { "main_menu", "01_main_menu_lakbay" },
        { "profile", "02_profile_ako_ni" },
        { "language_selection", "03_language_selection_tulay" },
        { "library", "04_library_tahimik" },
        { "wordle", "05_wordle_hula" },
        { "crossword", "06_crossword_pahalang_pababa" },
        { "wordsearch", "07_word_search_pangita" },
        { "word search", "07_word_search_pangita" }
    };

    private static BackgroundMusic _instance;

    private AudioSource _current;
    private AudioSource _next;
    private AudioListener _fallbackListener;
    private Coroutine _fade;
    private Coroutine _volumeFade;
    private float _volume;
    private bool _muted;

    /// <summary>Raised with the new state whenever music is turned on (false) or off (true).</summary>
    public static event System.Action<bool> MutedChanged;

    /// <summary>The saved music volume, 0 to 1.</summary>
    public static float Volume
    {
        get { return _instance != null ? _instance._volume : PlayerPrefs.GetFloat(VolumeKey, DefaultVolume); }
    }

    /// <summary>True when the player turned the music off.</summary>
    public static bool Muted
    {
        get { return _instance != null ? _instance._muted : PlayerPrefs.GetInt(MutedKey, 0) == 1; }
    }

    /// <summary>Sets and saves the music volume (0 = silent, 1 = full).</summary>
    public static void SetVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VolumeKey, volume);
        if (_instance != null)
        {
            _instance._volume = volume;
            _instance.FadeCurrentToTarget(0f);
        }
    }

    /// <summary>Turns the music off (true) or on (false) with a short fade, and saves the choice.</summary>
    public static void SetMuted(bool muted)
    {
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        if (_instance != null)
        {
            _instance._muted = muted;
            _instance.FadeCurrentToTarget(MuteFadeSeconds);
        }
        if (MutedChanged != null)
        {
            MutedChanged(muted);
        }
    }

    /// <summary>The volume the current track should end up at: 0 while muted, otherwise the saved volume.</summary>
    private float TargetVolume
    {
        get { return _muted ? 0f : _volume; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }
        var go = new GameObject("BackgroundMusic");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<BackgroundMusic>();
    }

    private void Awake()
    {
        _volume = PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
        _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        _current = MakeSource();
        _next = MakeSource();
        _fallbackListener = gameObject.AddComponent<AudioListener>();
        _fallbackListener.enabled = false;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private AudioSource MakeSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 0;
        source.volume = 0f;
        return source;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive)
        {
            return;
        }
        UpdateFallbackListener();

        string clipName = ClipFor(scene.name);
        if (clipName == null)
        {
            return; // unknown scene: keep the current music
        }
        if (_current.clip != null && _current.clip.name == clipName && _current.isPlaying)
        {
            return; // same track: keep playing
        }

        AudioClip clip = Resources.Load<AudioClip>(Folder + clipName);
        if (clip == null)
        {
            Debug.LogWarning("BackgroundMusic: Resources/" + Folder + clipName + " is missing.");
            return;
        }
        SwitchTo(clip);
    }

    private static string ClipFor(string sceneName)
    {
        string name = sceneName.ToLowerInvariant();
        for (int i = 0; i < Tracks.GetLength(0); i++)
        {
            if (name.Contains(Tracks[i, 0]))
            {
                return Tracks[i, 1];
            }
        }
        return null;
    }

    // Turns on this object's own AudioListener only when the loaded scene has no other enabled listener.
    private void UpdateFallbackListener()
    {
        _fallbackListener.enabled = false;
        foreach (AudioListener listener in FindObjectsOfType<AudioListener>())
        {
            if (listener != _fallbackListener && listener.enabled)
            {
                return;
            }
        }
        _fallbackListener.enabled = true;
    }

    private void SwitchTo(AudioClip clip)
    {
        if (_fade != null)
        {
            StopCoroutine(_fade);
            // a switch was still running: the track it was stopping goes silent right away
            _next.Stop();
            _next.clip = null;
        }
        if (_volumeFade != null)
        {
            StopCoroutine(_volumeFade);
            _volumeFade = null;
        }
        AudioSource outgoing = _current;
        _current = _next;
        _next = outgoing;

        _current.clip = clip;
        _current.volume = 0f;
        _fade = StartCoroutine(StopThenPlay(_current, _next));
    }

    // Stops the old scene's track first, then starts the new scene's track from the beginning, so the two never overlap.
    private IEnumerator StopThenPlay(AudioSource incoming, AudioSource outgoing)
    {
        // 1. stop the old track (a very short fade-out avoids a click)
        float startOut = outgoing.volume;
        float t = 0f;
        while (outgoing.isPlaying && t < StopSeconds)
        {
            t += Time.unscaledDeltaTime; // unscaled: still works while the game is paused (timeScale 0)
            outgoing.volume = startOut * (1f - Mathf.Clamp01(t / StopSeconds));
            yield return null;
        }
        outgoing.Stop();
        outgoing.clip = null;

        // 2. play the new track (short fade-in so it doesn't start with a jolt)
        incoming.Play();
        t = 0f;
        while (t < StartSeconds)
        {
            t += Time.unscaledDeltaTime;
            incoming.volume = TargetVolume * Mathf.Clamp01(t / StartSeconds); // read every frame, so muting now still works
            yield return null;
        }
        incoming.volume = TargetVolume;
        _fade = null;
    }

    // Moves the current track's volume to TargetVolume (after mute/unmute or a volume change). A running track switch already
    // reads TargetVolume every frame, so it is left alone.
    private void FadeCurrentToTarget(float seconds)
    {
        if (_fade != null || _current == null)
        {
            return;
        }
        if (_volumeFade != null)
        {
            StopCoroutine(_volumeFade);
        }
        if (seconds <= 0f)
        {
            _current.volume = TargetVolume;
            _volumeFade = null;
            return;
        }
        _volumeFade = StartCoroutine(FadeVolume(_current, seconds));
    }

    private IEnumerator FadeVolume(AudioSource source, float seconds)
    {
        float from = source.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(from, TargetVolume, Mathf.Clamp01(t / seconds));
            yield return null;
        }
        source.volume = TargetVolume;
        _volumeFade = null;
    }
}
