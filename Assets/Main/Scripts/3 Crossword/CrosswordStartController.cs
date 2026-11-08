using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// CrosswordStartController
/// Runs the Crossword start screen (scene "Crossword - Start"): the language chosen on the hub, a "Random puzzle" button and one card per category
/// (Animals, Food and drink, Body, Family and people, Home and things, Nature and weather, Numbers). Each card shows
///  - a tag with how many of its 3 puzzles are solved ("1 of 3 solved", gold once one is solved),
///  - the size navigator: an arrow stepper "Medium" with the number of words (or "Solved") under it. It starts on the first size that is not
///    solved yet; the arrows move through Small, Medium and Large,
///  - a Play button that starts the shown puzzle.
/// Starting a puzzle stores language, category and size in CrosswordSession and opens the play scene. "Random puzzle" starts a random puzzle that is not
/// solved yet (any puzzle once everything is solved). The back button and Esc return to the language hub. References are assigned in the scene
/// by CrosswordCategoryBuilder.
/// </summary>
public class CrosswordStartController : MonoBehaviour
{
    private const string HubScene = "3 Language_Selection_Menu";

    [Header("Top bar")]
    [SerializeField] private TMP_Text languageLabel;
    [SerializeField] private Button backButton;
    [SerializeField] private Button randomButton;

    [Header("Category cards (one entry per category)")]
    [SerializeField] private TMP_Text[] categoryNames;
    [SerializeField] private TMP_Text[] solvedLabels;
    [SerializeField] private Image[] solvedPills;
    [SerializeField] private TMP_Text[] sizeLabels;
    [SerializeField] private Button[] previousButtons;
    [SerializeField] private Button[] nextButtons;
    [SerializeField] private Button[] playButtons;

    [Header("Colours")]
    [SerializeField] private Color solvedTagColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color openTagColor = new Color32(239, 231, 212, 255);

    private int language;
    private int[] shownSize;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(BackToGames);
        TimerModes.AttachChips(ScoreRules.GameCrossword, languageLabel);
        if (randomButton != null) randomButton.onClick.AddListener(PlayRandom);

        int count = playButtons != null ? playButtons.Length : 0;
        shownSize = new int[count];
        for (int i = 0; i < count; i++)
        {
            int category = i;
            if (previousButtons != null && i < previousButtons.Length && previousButtons[i] != null)
            {
                previousButtons[i].onClick.AddListener(delegate { Step(category, -1); });
            }
            if (nextButtons != null && i < nextButtons.Length && nextButtons[i] != null)
            {
                nextButtons[i].onClick.AddListener(delegate { Step(category, 1); });
            }
            if (playButtons[i] != null)
            {
                playButtons[i].onClick.AddListener(delegate { Play(category, shownSize[category]); });
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

    /// <summary>Re-reads the saved progress: tags, size navigators (reset to the first size not solved yet) and the language chip.</summary>
    [ContextMenu("Refresh preview")]
    public void Refresh()
    {
        language = Mathf.Clamp(CrosswordSession.Language, 0, 2);
        if (languageLabel != null) languageLabel.text = CrosswordCategoryData.LanguageNames[language];

        int count = playButtons != null ? playButtons.Length : 0;
        if (shownSize == null || shownSize.Length != count)
        {
            shownSize = new int[count];
        }
        for (int c = 0; c < count; c++)
        {
            CrosswordCategoryDef category = CrosswordCategoryData.Category(language, c);
            if (categoryNames != null && c < categoryNames.Length && categoryNames[c] != null && category != null)
            {
                categoryNames[c].text = category.name;
            }
            shownSize[c] = FirstOpenSize(c);
            RefreshCard(c);
        }
    }

    private int SizeCount(int category)
    {
        int n = CrosswordCategoryData.SizeCount(language, category);
        return n > 0 ? n : 3;
    }

    private bool IsSolved(int category, int size)
    {
        return Application.isPlaying && CrosswordProgressStore.IsSolved(language, category, size);
    }

    private int FirstOpenSize(int category)
    {
        int n = SizeCount(category);
        for (int s = 0; s < n; s++)
        {
            if (!IsSolved(category, s))
            {
                return s;
            }
        }
        return 0;
    }

    private void RefreshCard(int category)
    {
        int n = SizeCount(category);
        int solved = Application.isPlaying ? CrosswordProgressStore.SolvedCount(language, category, n) : 0;
        int size = Mathf.Clamp(shownSize[category], 0, n - 1);
        shownSize[category] = size;

        if (solvedLabels != null && category < solvedLabels.Length && solvedLabels[category] != null)
        {
            solvedLabels[category].text = solved + " of " + n + " solved";
        }
        if (solvedPills != null && category < solvedPills.Length && solvedPills[category] != null)
        {
            solvedPills[category].color = solved > 0 ? solvedTagColor : openTagColor;
        }
        if (sizeLabels != null && category < sizeLabels.Length && sizeLabels[category] != null)
        {
            CrosswordPuzzleDef def = CrosswordCategoryData.Puzzle(language, category, size);
            string sizeName = def != null ? def.size : (size == 0 ? "Small" : (size == 1 ? "Medium" : "Large"));
            int words = def != null ? def.words.Count : 0;
            string status = IsSolved(category, size) ? "Solved" : (words + " words");
            PuzzleRecord best = Application.isPlaying ? ResultsStore.Best(ScoreRules.GameCrossword, language, category, size) : null;
            if (best != null) status = "Best " + best.Score + " · " + ResultsStore.Clock(best.Fastest);
            sizeLabels[category].text = sizeName + "\n<size=54%>" + status + "</size>";
        }
        if (previousButtons != null && category < previousButtons.Length && previousButtons[category] != null)
        {
            previousButtons[category].interactable = size > 0;
        }
        if (nextButtons != null && category < nextButtons.Length && nextButtons[category] != null)
        {
            nextButtons[category].interactable = size < n - 1;
        }
    }

    private void Step(int category, int direction)
    {
        shownSize[category] += direction;
        RefreshCard(category);
    }

    private void Play(int category, int size)
    {
        CrosswordSession.Language = language;
        CrosswordSession.Category = category;
        CrosswordSession.Size = size;
        StreakTracker.RecordPlay();
        SceneManager.LoadScene(CrosswordSession.PlayScene);
    }

    /// <summary>Starts a random puzzle that is not solved yet (any puzzle if every puzzle is solved).</summary>
    private void PlayRandom()
    {
        var open = new System.Collections.Generic.List<int[]>();
        var all = new System.Collections.Generic.List<int[]>();
        int categories = CrosswordCategoryData.CategoryCount(language);
        for (int c = 0; c < categories; c++)
        {
            int n = SizeCount(c);
            for (int s = 0; s < n; s++)
            {
                all.Add(new[] { c, s });
                if (!IsSolved(c, s))
                {
                    open.Add(new[] { c, s });
                }
            }
        }
        System.Collections.Generic.List<int[]> pool = open.Count > 0 ? open : all;
        if (pool.Count == 0)
        {
            return;
        }
        int[] pick = pool[Random.Range(0, pool.Count)];
        Play(pick[0], pick[1]);
    }

    /// <summary>Returns to the language hub.</summary>
    public void BackToGames()
    {
        SceneManager.LoadScene(HubScene);
    }
}
