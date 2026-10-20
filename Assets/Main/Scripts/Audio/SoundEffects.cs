using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// SoundEffects
/// Plays a sound for every button in the game. It creates itself before the first scene loads (RuntimeInitializeOnLoadMethod), survives scene
/// loads (DontDestroyOnLoad) and needs nothing placed in any scene, like BackgroundMusic. Every time the player presses on the screen it asks the
/// EventSystem what is under the pointer; if that is a Button (or a Toggle) that can be used, it plays a sound from
/// Assets/Main/Resources/Audio SFX, chosen by the button:
///  - Back arrows, "Back to ..." and "topics" buttons: ui_back; Cancel: ui_back
///  - the pause button and the settings gear: ui_open; Resume, Done and close buttons: ui_close
///  - Hint buttons: hint
///  - Wordle keypad: key_tap_1/2/3 (random) for letters, key_delete for Backspace, key_enter for Enter
///  - the Sound effects switch in the settings window plays toggle_on itself when it turns the effects back on
///  - every other button, including buttons the games create while they run (tiles, list rows, Library items): ui_click
/// Because it looks at the screen and not at each button, new buttons get their sound without any setup. SoundEffects.Play("clip name") plays
/// any clip of that folder from code. Sound effects can be switched off with SoundEffects.SetMuted(true) (saved in SaveStore "SfxMuted");
/// there is no switch for it in the menus yet. The sounds are described in Resources/Audio SFX/README - Sound Effects.md.
/// </summary>
public class SoundEffects : MonoBehaviour
{
    private const string Folder = "Audio SFX/";
    private const string MutedKey = "SfxMuted";
    private const float Volume = 0.9f;

    private static SoundEffects _instance;
    private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
    private AudioSource _source;
    private bool _muted;

    /// <summary>The name of the clip played last (for testing), for example "ui_click".</summary>
    public static string LastPlayed { get; private set; }

    /// <summary>True when the player turned the sound effects off.</summary>
    public static bool Muted
    {
        get { return _instance != null ? _instance._muted : SaveStore.GetInt(MutedKey, 0) == 1; }
    }

    /// <summary>Turns the sound effects off (true) or on (false) and saves the choice.</summary>
    public static void SetMuted(bool muted)
    {
        SaveStore.SetInt(MutedKey, muted ? 1 : 0);
        if (_instance != null)
        {
            _instance._muted = muted;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (_instance != null)
        {
            return;
        }
        var go = new GameObject("SoundEffects");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SoundEffects>();
        _instance._source = go.AddComponent<AudioSource>();
        _instance._source.playOnAwake = false;
        _instance._source.spatialBlend = 0f;
        _instance._muted = SaveStore.GetInt(MutedKey, 0) == 1;
    }

    /// <summary>Plays one clip of Resources/Audio SFX by name (for example "ui_click"), unless the effects are muted.</summary>
    public static void Play(string clipName, float volume = 1f)
    {
        if (_instance == null)
        {
            Create();
        }
        if (_instance._muted)
        {
            return;
        }
        AudioClip clip;
        if (!_instance._clips.TryGetValue(clipName, out clip))
        {
            clip = Resources.Load<AudioClip>(Folder + clipName);
            _instance._clips[clipName] = clip;
        }
        if (clip != null)
        {
            LastPlayed = clipName;
            _instance._source.PlayOneShot(clip, Volume * volume);
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PlayAt(Input.mousePosition);
        }
    }

    /// <summary>Plays the sound of the button under a screen position (nothing if there is none). Used on every press.</summary>
    public static void PlayAt(Vector2 screenPosition)
    {
        EventSystem es = EventSystem.current;
        if (es == null)
        {
            return;
        }
        var data = new PointerEventData(es) { position = screenPosition };
        var hits = new List<RaycastResult>();
        es.RaycastAll(data, hits);
        if (hits.Count == 0)
        {
            return;
        }
        // only the topmost UI element counts: a panel in front of a button blocks it
        Selectable selectable = hits[0].gameObject.GetComponentInParent<Selectable>();
        if (selectable == null || !selectable.IsInteractable())
        {
            return;
        }
        string clip = ClipFor(selectable);
        if (clip != null)
        {
            Play(clip);
        }
    }

    /// <summary>The sound for a button, by what it is; null = silent (the button makes its own sound).</summary>
    private static string ClipFor(Selectable selectable)
    {
        var key = selectable.GetComponent<WordleKeyButton>();
        if (key != null)
        {
            if (key.kind == WordleKeyKind.Backspace) return "key_delete";
            if (key.kind == WordleKeyKind.Enter) return "key_enter";
            return "key_tap_" + Random.Range(1, 4);
        }
        string name = selectable.gameObject.name.ToLowerInvariant();
        if (name.Contains("back") || name.Contains("topics") || name.Contains("cancel")) return "ui_back";
        if (name.Contains("pause") || name.Contains("settings")) return "ui_open";
        if (name.Contains("resume") || name.Contains("close") || name.Contains("done")) return "ui_close";
        if (name.Contains("hint")) return "hint";
        return "ui_click";
    }
}
