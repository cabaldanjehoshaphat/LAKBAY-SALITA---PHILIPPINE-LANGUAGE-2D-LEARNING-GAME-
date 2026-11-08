using TMPro;
using UnityEngine;

// Countdown timer for a Wordle puzzle scene. The starting duration (minutes/seconds)
// comes from a shared WordleSceneTimerDurations asset, looked up by this GameObject's
// own scene name — one asset holds every puzzle scene's duration, editable from a
// single Inspector view instead of opening each scene individually. Text style, delay,
// and horizontal position come from a separate shared WordleTimerSettings asset (so
// those stay consistent across every scene from one place). If settings.delaySeconds >
// 0, the display first counts down that delay (e.g. a "starting in..." lead-in) before
// switching to the real minutes/seconds countdown; with delaySeconds at 0 (the
// default) or no settings assigned, the real timer starts immediately. Always displays
// MM:SS, clamped at 00:00. Editing either shared asset previews the change live in the
// Editor via OnValidate/ApplyIfUsing. Stops counting altogether once the puzzle's
// WordleKeyboardTyper reports the word solved (found automatically via the scene's
// "WordleGenerator" GameObject — every puzzle scene uses that same name, so no
// per-scene wiring is needed), freezing the display at whatever time was left.
// UseStopwatch switches it to Normal mode (see TimerModes): it counts up and never expires.
public class WordleTimer : MonoBehaviour
{
    [Header("Duration Lookup (one entry per scene)")]
    [SerializeField] private WordleSceneTimerDurations sceneDurations;

    [Header("Shared Style/Delay/Position")]
    [SerializeField] private WordleTimerSettings settings;
    [SerializeField] private TMP_Text timerText;

    [Tooltip("Apply the shared settings' font/size to the timer text. The redesigned screens turn this off and style the label themselves.")]
    [SerializeField] private bool applyTextStyle = true;

    [Tooltip("Apply the shared settings' horizontal position to this object. The redesigned screens turn this off.")]
    [SerializeField] private bool applyPosition = true;

    private enum Phase
    {
        Delay,
        Counting,
        Expired
    }

    private Phase phase;
    private float remainingSeconds;
    private float totalSeconds;
    private bool paused;
    private bool expiredRaised;
    private int overrideSeconds;
    private WordleKeyboardTyper typer;
    private bool stopwatch;
    private float stopwatchSeconds;

    /// <summary>Normal mode: the display counts up and the time never runs out. Call it before Start, like OverrideDuration.</summary>
    public void UseStopwatch()
    {
        stopwatch = true;
    }

    /// <summary>True in Normal mode.</summary>
    public bool IsStopwatch => stopwatch;

    /// <summary>
    /// Sets the duration of the real countdown from code (used by the Wordle category play scene, which has one scene for every level),
    /// instead of looking it up by scene name. Call it before Start (an early Awake is fine).
    /// </summary>
    public void OverrideDuration(int seconds)
    {
        overrideSeconds = Mathf.Max(0, seconds);
    }

    /// <summary>True once the real countdown (not the delay) has reached zero.</summary>
    public bool HasExpired => phase == Phase.Expired;

    /// <summary>Raised once when the real countdown reaches zero (used by WordleGameController for "time's up").</summary>
    public event System.Action Expired;

    /// <summary>True while the timer is held by the pause menu.</summary>
    public bool IsPaused => paused;

    /// <summary>True during the short lead-in delay, before the real countdown starts.</summary>
    public bool IsLeadIn => phase == Phase.Delay;

    /// <summary>True while the real countdown is running (not lead-in, not expired).</summary>
    public bool IsCounting => phase == Phase.Counting;

    /// <summary>Seconds left on the display (lead-in seconds during the lead-in).</summary>
    public float RemainingSeconds => Mathf.Max(0f, remainingSeconds);

    /// <summary>Seconds of the real countdown already used (0 during the lead-in).</summary>
    public float ElapsedSeconds => phase == Phase.Delay ? 0f : stopwatch ? stopwatchSeconds : Mathf.Max(0f, totalSeconds - Mathf.Max(0f, remainingSeconds));

    /// <summary>The MM:SS text currently shown.</summary>
    public string DisplayText => stopwatch && phase != Phase.Delay ? FormatTime(Mathf.FloorToInt(stopwatchSeconds)) : FormatTime(Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds)));

    /// <summary>Freezes the countdown (pause menu).</summary>
    public void Pause()
    {
        paused = true;
    }

    /// <summary>Continues the countdown after Pause.</summary>
    public void Resume()
    {
        paused = false;
    }

    /// <summary>Formats whole seconds as MM:SS.</summary>
    public static string FormatTime(int totalSecondsValue)
    {
        totalSecondsValue = Mathf.Max(0, totalSecondsValue);
        return $"{totalSecondsValue / 60:00}:{totalSecondsValue % 60:00}";
    }

    private void Start()
    {
        GameObject generatorObject = GameObject.Find("WordleGenerator");
        typer = generatorObject != null ? generatorObject.GetComponent<WordleKeyboardTyper>() : null;
        ResetTimer();
    }

    private void Update()
    {
        if (phase == Phase.Expired || paused || (typer != null && typer.Finished))
        {
            // Word guessed correctly (or tries used up, or paused) — freeze the countdown right where it is.
            return;
        }

        if (stopwatch && phase == Phase.Counting)
        {
            stopwatchSeconds += Time.deltaTime;
            UpdateDisplay();
            return;
        }

        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds <= 0f)
        {
            if (phase == Phase.Delay)
            {
                phase = Phase.Counting;
                remainingSeconds = MainDurationSeconds();
                totalSeconds = remainingSeconds;
            }
            else
            {
                remainingSeconds = 0f;
                phase = Phase.Expired;
            }
        }

        UpdateDisplay();

        if (phase == Phase.Expired && !expiredRaised)
        {
            expiredRaised = true;
            if (Expired != null)
            {
                Expired();
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplySettings();
        ResetTimer();
    }
#endif

    /// <summary>Called by WordleTimerSettings.OnValidate on every WordleTimer it can find
    /// whenever that shared asset's own values change; re-applies only if this instance
    /// actually uses that asset.</summary>
    public void ApplyIfUsing(WordleTimerSettings changedSettings)
    {
        if (settings == changedSettings)
        {
            ApplySettings();
            ResetTimer();
        }
    }

    /// <summary>Called by WordleSceneTimerDurations.OnValidate on every WordleTimer it
    /// can find whenever that shared asset's own values change; re-applies only if this
    /// instance actually uses that asset.</summary>
    public void ApplyIfUsingDurations(WordleSceneTimerDurations changedDurations)
    {
        if (sceneDurations == changedDurations)
        {
            ResetTimer();
        }
    }

    /// <summary>Restarts the countdown — from settings.delaySeconds if set, otherwise
    /// straight into this scene's own duration (looked up from sceneDurations).</summary>
    public void ResetTimer()
    {
        float delaySeconds = settings != null ? settings.delaySeconds : 0f;
        expiredRaised = false;
        stopwatchSeconds = 0f;
        if (delaySeconds > 0f)
        {
            phase = Phase.Delay;
            remainingSeconds = delaySeconds;
            totalSeconds = MainDurationSeconds();
        }
        else
        {
            phase = Phase.Counting;
            remainingSeconds = MainDurationSeconds();
            totalSeconds = remainingSeconds;
        }

        UpdateDisplay();
    }

    private float MainDurationSeconds()
    {
        if (overrideSeconds > 0)
        {
            return overrideSeconds;
        }

        if (sceneDurations != null && sceneDurations.TryGetDuration(gameObject.scene.name, out int minutes, out int seconds))
        {
            return minutes * 60 + seconds;
        }

        Debug.LogWarning($"WordleTimer: no duration entry found for scene \"{gameObject.scene.name}\" in {(sceneDurations != null ? sceneDurations.name : "NULL sceneDurations")}. Defaulting to 5:00.");
        return 5 * 60;
    }

    private void ApplySettings()
    {
        if (settings == null)
        {
            return;
        }

        if (timerText != null && applyTextStyle)
        {
            if (settings.font != null)
            {
                timerText.font = settings.font;
            }

            timerText.fontSize = settings.fontSize;
            timerText.fontStyle = settings.fontStyle;
        }

        var rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null && applyPosition)
        {
            Vector2 pos = rectTransform.anchoredPosition;
            pos.x = settings.horizontalPosition;
            rectTransform.anchoredPosition = pos;
        }
    }

    private void UpdateDisplay()
    {
        if (timerText == null)
        {
            return;
        }

        timerText.text = DisplayText;
    }
}
