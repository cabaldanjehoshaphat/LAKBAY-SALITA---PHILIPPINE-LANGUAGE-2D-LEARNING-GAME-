using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// WordleStartController
/// Runs the Wordle start screen (scene "Wordle - Start"): the language chosen on the hub, a "Random word" button and one card per
/// category (Animals, Food and drink, Body, Family and people, Home and things, Nature and weather, Numbers). Each card shows
///  - a tag with how many of its 6 levels are solved ("3 of 6 solved", gold once one is solved),
///  - the level navigator: an arrow stepper "Level 4" with a line under it ("Solved" or "Not solved yet"). It starts on the first level not
///    solved yet; the arrows move through the levels,
///  - a Play button that starts the shown level.
/// Starting a level stores language, category and level in WordleSession and opens the play scene. Random word starts a random level that
/// is not solved yet (any level once everything is solved). The back button and Esc return to the language hub. References are assigned in
/// the scene by WordleCategoryBuilder.
/// </summary>
public class WordleStartController : MonoBehaviour
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
    [SerializeField] private TMP_Text[] levelLabels;
    [SerializeField] private Button[] previousButtons;
    [SerializeField] private Button[] nextButtons;
    [SerializeField] private Button[] playButtons;

    [Header("Colours")]
    [SerializeField] private Color solvedTagColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color openTagColor = new Color32(239, 231, 212, 255);

    private int language;
    private int[] shownLevel;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(BackToGames);
        TimerModes.AttachChips(ScoreRules.GameWordle, languageLabel);
        if (randomButton != null) randomButton.onClick.AddListener(PlayRandom);

        int count = playButtons != null ? playButtons.Length : 0;
        shownLevel = new int[count];
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
                playButtons[i].onClick.AddListener(delegate { Play(category, shownLevel[category]); });
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

    /// <summary>Re-reads the saved progress: tags, level navigators (reset to the first level not solved yet) and the language chip.</summary>
    [ContextMenu("Refresh preview")]
    public void Refresh()
    {
        language = Mathf.Clamp(WordleSession.Language, 0, 2);
        if (languageLabel != null) languageLabel.text = WordleData.LanguageNames[language];

        int count = playButtons != null ? playButtons.Length : 0;
        if (shownLevel == null || shownLevel.Length != count)
        {
            shownLevel = new int[count];
        }
        for (int c = 0; c < count; c++)
        {
            WordleCategoryDef category = WordleData.Category(language, c);
            if (categoryNames != null && c < categoryNames.Length && categoryNames[c] != null && category != null)
            {
                categoryNames[c].text = category.name;
            }
            shownLevel[c] = FirstOpenLevel(c);
            RefreshCard(c);
        }
    }

    private int LevelCount(int category)
    {
        int n = WordleData.LevelCount(language, category);
        return n > 0 ? n : 6;
    }

    private bool IsSolved(int category, int level)
    {
        return Application.isPlaying && WordleProgressStore.IsSolved(language, category, level);
    }

    private int FirstOpenLevel(int category)
    {
        int n = LevelCount(category);
        for (int l = 0; l < n; l++)
        {
            if (!IsSolved(category, l))
            {
                return l;
            }
        }
        return 0;
    }

    private void RefreshCard(int category)
    {
        int n = LevelCount(category);
        int solved = Application.isPlaying ? WordleProgressStore.SolvedCount(language, category, n) : 0;
        int level = Mathf.Clamp(shownLevel[category], 0, n - 1);
        shownLevel[category] = level;

        if (solvedLabels != null && category < solvedLabels.Length && solvedLabels[category] != null)
        {
            solvedLabels[category].text = solved + " of " + n + " solved";
        }
        if (solvedPills != null && category < solvedPills.Length && solvedPills[category] != null)
        {
            solvedPills[category].color = solved > 0 ? solvedTagColor : openTagColor;
        }
        if (levelLabels != null && category < levelLabels.Length && levelLabels[category] != null)
        {
            string status = IsSolved(category, level) ? "Solved" : "Not solved yet";
            PuzzleRecord best = Application.isPlaying ? ResultsStore.Best(ScoreRules.GameWordle, WordleSession.Language, category, level) : null;
            if (best != null) status = "Solved · Best " + best.Score;
            levelLabels[category].text = "Level " + (level + 1) + "\n<size=54%>" + status + "</size>";
        }
        if (previousButtons != null && category < previousButtons.Length && previousButtons[category] != null)
        {
            previousButtons[category].interactable = level > 0;
        }
        if (nextButtons != null && category < nextButtons.Length && nextButtons[category] != null)
        {
            nextButtons[category].interactable = level < n - 1;
        }
    }

    private void Step(int category, int direction)
    {
        shownLevel[category] += direction;
        RefreshCard(category);
    }

    private void Play(int category, int level)
    {
        WordleSession.Language = language;
        WordleSession.Category = category;
        WordleSession.Level = level;
        StreakTracker.RecordPlay();
        SceneManager.LoadScene(WordleSession.PlayScene);
    }

    /// <summary>Starts a random level that is not solved yet (any level if every level is solved).</summary>
    private void PlayRandom()
    {
        var open = new System.Collections.Generic.List<int[]>();
        var all = new System.Collections.Generic.List<int[]>();
        int categories = WordleData.CategoryCount(language);
        for (int c = 0; c < categories; c++)
        {
            int n = LevelCount(c);
            for (int l = 0; l < n; l++)
            {
                all.Add(new[] { c, l });
                if (!IsSolved(c, l))
                {
                    open.Add(new[] { c, l });
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
