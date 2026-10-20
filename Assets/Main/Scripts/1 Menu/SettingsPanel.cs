using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SettingsPanel
/// The settings window of the main menu. The gear chip in the top bar (where the music chip was) opens it; it has these settings, all saved:
///  - Sound effects: an On / Off switch (SoundEffects.SetMuted), the button sounds of the whole game.
///  - Music volume: a slider from 0 to 100 % (BackgroundMusic.SetVolume). Dragging it to 0 turns the music off, anything above turns it
///    back on, so there is no separate music switch.
///  - Display: a Windowed / Fullscreen choice (ScreenFit.SetFullscreen).
///  - Window size: the three standard sizes 800 x 600, 1024 x 768 and 1920 x 1080 (ScreenFit.SetResolution). Choosing a size switches to
///    Windowed mode at that size. A size that is bigger than the player's screen is dimmed and can not be chosen.
/// It lives in its own scene, "5 Options" (Assets/Main/Scenes/1 Menu): the gear chip of the Main menu opens that scene (SceneNavigator.
/// LoadOptionsMenu), and Done, the back arrow and Esc return to the scene named in Return Scene ("1 Main_menu"). The scene is built by
/// SettingsPanelBuilder (Tools > Settings); the references are assigned there. The script also works as a window inside another scene:
/// leave Return Scene empty and the panel is hidden at start, opened by the Open Button and closed by Done or Esc.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backButton;

    [Header("Options scene")]
    [Tooltip("Scene to load when the player leaves (Done, back arrow, Esc). Empty = the panel is a window that just hides.")]
    [SerializeField] private string returnScene;
    [Tooltip("Hide the panel when the scene starts (a window). Off for the Options scene, where the panel is always shown.")]
    [SerializeField] private bool hideOnStart = true;

    [Header("Sound effects switch")]
    [SerializeField] private Button sfxButton;
    [SerializeField] private Image sfxTrack;
    [SerializeField] private TMP_Text sfxLabel;

    [Header("Music volume")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeLabel;

    [Header("Display mode")]
    [SerializeField] private Button windowedButton;
    [SerializeField] private Image windowedImage;
    [SerializeField] private TMP_Text windowedLabel;
    [SerializeField] private Button fullscreenButton;
    [SerializeField] private Image fullscreenImage;
    [SerializeField] private TMP_Text fullscreenLabel;

    [Header("Window size (800 x 600, 1024 x 768, 1920 x 1080)")]
    [SerializeField] private Button[] sizeButtons;
    [SerializeField] private Image[] sizeImages;
    [SerializeField] private TMP_Text[] sizeLabels;

    [Header("Colours")]
    [SerializeField] private Color onColor = new Color32(93, 160, 42, 255);
    [SerializeField] private Color offColor = new Color32(185, 183, 172, 255);
    [SerializeField] private Color chipOffColor = new Color32(239, 231, 212, 255);
    [SerializeField] private Color chipOnText = Color.white;
    [SerializeField] private Color chipOffText = new Color32(59, 42, 26, 255);

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backButton != null) backButton.onClick.AddListener(Close);
        if (sfxButton != null) sfxButton.onClick.AddListener(ToggleSfx);
        if (volumeSlider != null) volumeSlider.onValueChanged.AddListener(OnVolume);
        if (windowedButton != null) windowedButton.onClick.AddListener(delegate { SetFullscreen(false); });
        if (fullscreenButton != null) fullscreenButton.onClick.AddListener(delegate { SetFullscreen(true); });
        if (sizeButtons != null)
        {
            for (int i = 0; i < sizeButtons.Length; i++)
            {
                int chosen = i;
                if (sizeButtons[i] != null) sizeButtons[i].onClick.AddListener(delegate { ChooseSize(chosen); });
            }
        }
        if (panel != null && Application.isPlaying && hideOnStart) panel.SetActive(false);
    }

    private void Start()
    {
        if (!hideOnStart)
        {
            Refresh();   // the Options scene shows the saved settings at once
        }
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

    /// <summary>Leaves the Options scene (back to Return Scene), or hides the window when there is no Return Scene.</summary>
    public void Close()
    {
        if (!string.IsNullOrEmpty(returnScene) && Application.isPlaying)
        {
            SceneManager.LoadScene(returnScene);
            return;
        }
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Reads the saved settings into the switch, the slider and the display chips.</summary>
    public void Refresh()
    {
        bool sfxOn = !SoundEffects.Muted;
        if (sfxTrack != null) sfxTrack.color = sfxOn ? onColor : offColor;
        if (sfxLabel != null) sfxLabel.text = sfxOn ? "On" : "Off";
        float volume = BackgroundMusic.Muted ? 0f : BackgroundMusic.Volume;
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(volume);
        ShowPercent(volume);
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        bool fullscreen = ScreenFit.Fullscreen;
        SetChip(windowedButton, windowedImage, windowedLabel, !fullscreen, true);
        SetChip(fullscreenButton, fullscreenImage, fullscreenLabel, fullscreen, true);
        if (sizeButtons == null)
        {
            return;
        }
        for (int i = 0; i < sizeButtons.Length; i++)
        {
            Button b = sizeButtons[i];
            Image img = sizeImages != null && i < sizeImages.Length ? sizeImages[i] : null;
            TMP_Text label = sizeLabels != null && i < sizeLabels.Length ? sizeLabels[i] : null;
            SetChip(b, img, label, !fullscreen && ScreenFit.ResolutionIndex == i, ScreenFit.PresetFits(i));
        }
    }

    private void SetChip(Button button, Image image, TMP_Text label, bool selected, bool enabled)
    {
        if (button != null) button.interactable = enabled;
        if (image != null) image.color = selected ? onColor : chipOffColor;
        if (label != null) label.color = selected ? chipOnText : chipOffText;
    }

    private void SetFullscreen(bool fullscreen)
    {
        ScreenFit.SetFullscreen(fullscreen);
        RefreshDisplay();
    }

    private void ChooseSize(int index)
    {
        ScreenFit.SetResolution(index);
        RefreshDisplay();
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
