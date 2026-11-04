using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// WordSearchStartController
/// Runs the Word Search start screen (scene "WordSearch - Start"): the language chosen on the hub and one card per category (Animals,
/// Food and drink, Body, Family and people, Home and things, Nature and weather, Numbers) with three level chips (Level 1, 2 and 3). A solved
/// puzzle's chip is gold and each card says how many of its puzzles are solved. The "Random puzzle" button (top right, like Wordle's "Random word") starts a random
/// puzzle that is not solved yet. Tapping a chip stores language, category and puzzle in WordSearchSession and opens the play scene. The
/// Classic / Meaning buttons were removed from the scene (the game plays in Classic mode); the optional mode buttons in this script stay
/// dormant until they are assigned again and WordSearchSession.MeaningModeAvailable is turned on. The back button and Esc return to the
/// language hub. References are assigned in the scene by WordSearchDesignBuilder.
/// </summary>
public class WordSearchStartController : MonoBehaviour
{
    private const string HubScene = "3 Language_Selection_Menu";
    private const int ChipsPerCategory = 3;

    [Header("Top bar")]
    [SerializeField] private TMP_Text languageLabel;
    [SerializeField] private Button backButton;
    [SerializeField] private Button classicButton;
    [SerializeField] private Image classicImage;
    [SerializeField] private TMP_Text classicLabel;
    [SerializeField] private Button meaningButton;
    [SerializeField] private Image meaningImage;
    [SerializeField] private TMP_Text meaningLabel;
    [SerializeField] private TMP_Text modeHint;
    [SerializeField] private Button randomButton;

    [Header("Category cards (7) and their chips (7 x 3)")]
    [SerializeField] private TMP_Text[] categoryNames;
    [SerializeField] private TMP_Text[] categoryProgress;
    [SerializeField] private Button[] chipButtons;
    [SerializeField] private Image[] chipImages;

    [Header("Colours")]
    [SerializeField] private Color modeOnColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color modeOffColor = new Color32(253, 248, 238, 255);
    [SerializeField] private Color solvedColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color openColor = new Color32(239, 231, 212, 255);
    [SerializeField] private Color lockedColor = new Color32(253, 248, 238, 160);
    [SerializeField] private Color lockedTextColor = new Color32(59, 42, 26, 90);
    [SerializeField] private Color enabledTextColor = new Color32(59, 42, 26, 255);

    private int language;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(BackToGames);
        if (randomButton != null) randomButton.onClick.AddListener(PlayRandom);
        if (classicButton != null) classicButton.onClick.AddListener(delegate { SetMode(WordSearchMode.Classic); });
        if (meaningButton != null) meaningButton.onClick.AddListener(delegate { SetMode(WordSearchMode.Meaning); });

        if (chipButtons != null)
        {
            for (int i = 0; i < chipButtons.Length; i++)
            {
                if (chipButtons[i] == null)
                {
                    continue;
                }
                int category = i / ChipsPerCategory;
                int puzzle = i % ChipsPerCategory;
                chipButtons[i].onClick.AddListener(delegate { StartPuzzle(category, puzzle); });
            }
        }
    }

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BackToGames();
        }
    }

    /// <summary>Edit-mode and runtime refresh: language chip, category names and solved chips.</summary>
    [ContextMenu("Refresh preview")]
    public void Refresh()
    {
        language = Mathf.Clamp(WordSearchSession.Language, 0, 2);
        if (languageLabel != null) languageLabel.text = WordSearchData.LanguageNames[language];

        for (int c = 0; c < categoryNames.Length; c++)
        {
            WordSearchCategory category = WordSearchData.Category(language, c);
            if (categoryNames[c] != null && category != null)
            {
                categoryNames[c].text = category.name;
            }
            int puzzleCount = category != null ? category.puzzles.Count : ChipsPerCategory;
            int solved = Application.isPlaying ? WordSearchProgressStore.SolvedCount(language, c, puzzleCount) : 0;
            if (categoryProgress != null && c < categoryProgress.Length && categoryProgress[c] != null)
            {
                categoryProgress[c].text = solved + " of " + puzzleCount + " solved";
            }
            for (int p = 0; p < ChipsPerCategory; p++)
            {
                int index = c * ChipsPerCategory + p;
                if (chipImages != null && index < chipImages.Length && chipImages[index] != null)
                {
                    bool done = Application.isPlaying && WordSearchProgressStore.IsSolved(language, c, p);
                    chipImages[index].color = done ? solvedColor : openColor;
                }
            }
        }
        ApplyMode(WordSearchSession.Mode);
    }

    private void SetMode(WordSearchMode mode)
    {
        WordSearchSession.Mode = mode;
        ApplyMode(mode);
    }

    private void ApplyMode(WordSearchMode mode)
    {
        bool locked = !WordSearchSession.MeaningModeAvailable;
        if (classicImage != null) classicImage.color = mode == WordSearchMode.Classic ? modeOnColor : modeOffColor;
        if (meaningImage != null) meaningImage.color = mode == WordSearchMode.Meaning ? modeOnColor : modeOffColor;

        // while Meaning mode is off its button is locked: not clickable, pale background and dim text
        if (meaningButton != null) meaningButton.interactable = !locked;
        if (locked)
        {
            if (meaningImage != null) meaningImage.color = lockedColor;
            if (meaningLabel != null) meaningLabel.color = lockedTextColor;
        }
        else if (meaningLabel != null)
        {
            meaningLabel.color = enabledTextColor;
        }

        if (modeHint != null)
        {
            modeHint.text = locked
                ? "Meaning mode is locked for now"
                : mode == WordSearchMode.Meaning
                    ? "Meaning mode: the list shows English meanings"
                    : "Classic mode: the list shows the words";
        }
    }

    private void StartPuzzle(int category, int puzzle)
    {
        WordSearchSession.Language = language;
        WordSearchSession.Category = category;
        WordSearchSession.Puzzle = puzzle;
        SceneManager.LoadScene(WordSearchSession.PlayScene);
    }

    /// <summary>
    /// Random: starts a random puzzle that is not solved yet, from any category (any puzzle once every puzzle is solved), in the
    /// mode that is switched on.
    /// </summary>
    private void PlayRandom()
    {
        var open = new System.Collections.Generic.List<int[]>();
        var all = new System.Collections.Generic.List<int[]>();
        int categories = WordSearchData.CategoryCount(language);
        for (int c = 0; c < categories; c++)
        {
            WordSearchCategory category = WordSearchData.Category(language, c);
            int puzzles = category != null ? category.puzzles.Count : 0;
            for (int p = 0; p < puzzles; p++)
            {
                all.Add(new[] { c, p });
                if (!WordSearchProgressStore.IsSolved(language, c, p))
                {
                    open.Add(new[] { c, p });
                }
            }
        }
        System.Collections.Generic.List<int[]> pool = open.Count > 0 ? open : all;
        if (pool.Count == 0)
        {
            return;
        }
        int[] pick = pool[Random.Range(0, pool.Count)];
        StartPuzzle(pick[0], pick[1]);
    }

    /// <summary>Returns to the language hub.</summary>
    public void BackToGames()
    {
        SceneManager.LoadScene(HubScene);
    }
}
