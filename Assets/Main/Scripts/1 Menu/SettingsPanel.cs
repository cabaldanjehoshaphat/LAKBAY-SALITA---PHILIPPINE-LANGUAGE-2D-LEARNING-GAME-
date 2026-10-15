using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SettingsPanel
/// The settings window of the main menu. The gear chip in the top bar (where the music chip was) opens it; it has two settings, both saved:
///  - Sound effects: an On / Off switch (SoundEffects.SetMuted), the button sounds of the whole game.
///  - Music volume: a slider from 0 to 100 % (BackgroundMusic.SetVolume). Dragging it to 0 turns the music off, anything above turns it
///    back on, so there is no separate music switch.
/// "Done", Esc and the gear chip close it. The window is built into the main menu scene by SettingsPanelBuilder (Tools > Settings); the
/// references are assigned there.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Sound effects switch")]
    [SerializeField] private Button sfxButton;
    [SerializeField] private Image sfxTrack;
    [SerializeField] private TMP_Text sfxLabel;

    [Header("Music volume")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeLabel;

    [Header("Colours")]
    [SerializeField] private Color onColor = new Color32(93, 160, 42, 255);
    [SerializeField] private Color offColor = new Color32(185, 183, 172, 255);

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (sfxButton != null) sfxButton.onClick.AddListener(ToggleSfx);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolume);
        if (panel != null && Application.isPlaying) panel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && panel != null && panel.activeSelf)
        {
            Close();
        }
    }

    private void Toggle()
    {
        if (panel != null && panel.activeSelf) Close(); else Open();
    }

    /// <summary>Opens the window and shows the saved settings.</summary>
    public void Open()
    {
        Refresh();
        if (panel != null) panel.SetActive(true);
    }

    /// <summary>Closes the window.</summary>
    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Reads the saved settings into the switch and the slider.</summary>
    public void Refresh()
    {
        bool sfxOn = !SoundEffects.Muted;
        if (sfxTrack != null) sfxTrack.color = sfxOn ? onColor : offColor;
        if (sfxLabel != null) sfxLabel.text = sfxOn ? "On" : "Off";
        float volume = BackgroundMusic.Muted ? 0f : BackgroundMusic.Volume;
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(volume);
        ShowPercent(volume);
    }

    private void ToggleSfx()
    {
        bool nowMuted = !SoundEffects.Muted;
        SoundEffects.SetMuted(nowMuted);
        if (!nowMuted)
        {
            SoundEffects.Play("toggle_on");   // the press itself was silent while the effects were off
        }
        Refresh();
    }

    private void OnVolume(float value)
    {
        if (value <= 0.001f)
        {
            if (!BackgroundMusic.Muted) BackgroundMusic.SetMuted(true);
        }
        else
        {
            if (BackgroundMusic.Muted) BackgroundMusic.SetMuted(false);
            BackgroundMusic.SetVolume(value);
        }
        ShowPercent(value);
    }

    private void ShowPercent(float value)
    {
        if (volumeLabel != null) volumeLabel.text = Mathf.RoundToInt(value * 100f) + "%";
    }
}
