using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// WordSearchGameController
/// Runs the Word Search play screen (scene "WordSearch - Play"). It reads the chosen language, category, puzzle and mode from
/// WordSearchSession, hides the puzzle's words in a 10x10 grid (WordSearchGenerator) and shows them on the WordSearchBoardView:
///  - the top bar: pause button, language chip, stopwatch and "Found N of M",
///  - the word list, shortest word first. Classic mode lists the words ("LANGGAM"); Meaning mode lists their English meanings with one underscore
///    per letter ("bird · _ _ _ _ _ _ _") and fills in the native word when it is found. Found words are struck through and get the
///    colour of their capsule on the grid,
///  - Hint (3 per puzzle): pulses a ring on the first letter of a word that is not found yet,
///  - the pause window (Resume, Choose puzzle, Back to games; Esc or the app going to the background also pause) and the win card with
///    every word and its meaning, a star to save the word to the Library favorites, time, hints used and streak.
/// Finishing a puzzle is saved: WordSearchProgressStore marks it solved (gold chip on the start screen) and
/// PlayerDatabase.RecordWordSearchWin counts it on the Profile screen. All references are assigned in the scene by
/// WordSearchDesignBuilder.
/// </summary>
public class WordSearchGameController : MonoBehaviour
{
    private const string HubScene = "3 Language_Selection_Menu";
    private const string LibraryScene = "4 Library_menu";

    [Header("Game parts")]
    [SerializeField] private WordSearchBoardView board;
    [SerializeField] private HubToast toast;
    [SerializeField] private int hintsPerPuzzle = 3;

    [Header("Top bar")]
    [SerializeField] private TMP_Text languageLabel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private TMP_Text foundLabel;
    [SerializeField] private Button pauseButton;

    [Header("Board group (hidden while paused)")]
    [SerializeField] private CanvasGroup boardGroup;

    [Header("Word list")]
    [SerializeField] private TMP_Text listTitle;
    [SerializeField] private GameObject[] rowRoots;
    [SerializeField] private TMP_Text[] rowLabels;
    [SerializeField] private Image[] rowDots;

    [Header("Tools")]
    [SerializeField] private Button hintButton;
    [SerializeField] private TMP_Text hintLabel;

    [Header("Pause window")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private TMP_Text pauseTimeLabel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseChooseButton;
    [SerializeField] private Button pauseBackButton;

    [Header("Result card")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private RectTransform resultCard;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private RectTransform resultListContent;
    [SerializeField] private GameObject resultRowTemplate;
    [SerializeField] private TMP_Text statTime;
    [SerializeField] private TMP_Text statHints;
    [SerializeField] private TMP_Text statStreak;
    [SerializeField] private Button resultNextButton;
    [SerializeField] private Button resultLibraryButton;
    [SerializeField] private Button resultBackButton;
    [SerializeField] private RectTransform confettiRoot;

    [Header("Colours")]
    [SerializeField] private Color textColor = new Color32(59, 42, 26, 255);
    [SerializeField] private Color dimColor = new Color32(138, 134, 120, 255);
    [SerializeField] private Color dotOffColor = new Color32(214, 205, 183, 255);
    [SerializeField] private Color starOnColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color starOffColor = new Color32(185, 183, 172, 255);

    private WordSearchGrid grid;
    private readonly List<WordSearchPlacement> listOrder = new List<WordSearchPlacement>();
    private WordSearchMode mode;
    private bool ready;
    private bool paused;
    private bool resultShown;
    private float elapsed;
    private int hintsLeft;
    private int hintsUsed;
    private int foundCount;

    // the description card (built in code between the timer and the "Found" chip): shows what the last found or tapped word means
    private TMP_Text descriptionTitle;
    private TMP_Text descriptionBody;

    private void Awake()
    {
        Hook(pauseButton, OpenPause);
        Hook(resumeButton, ClosePause);
        Hook(pauseChooseButton, ChoosePuzzle);
        Hook(pauseBackButton, BackToGames);
        Hook(resultNextButton, NextPuzzle);
        Hook(resultLibraryButton, OpenLibrary);
        Hook(resultBackButton, BackToGames);
        Hook(hintButton, UseHint);

        if (pausePanel != null) pausePanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (resultRowTemplate != null) resultRowTemplate.SetActive(false);
    }

    private static void Hook(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private void Start()
    {
        StreakTracker.RecordPlay();
        if (board != null)
        {
            board.SelectionReleased += OnSelectionReleased;
        }
        BuildDescriptionCard();
        StartPuzzle();
        MakeRowsTappable();
    }

    private void OnDestroy()
    {
        if (board != null)
        {
            board.SelectionReleased -= OnSelectionReleased;
        }
    }

    /// <summary>Edit-mode helper used by the design builder: shows the first puzzle so the saved scene displays the design before Play.</summary>
    [ContextMenu("Refresh preview")]
    public void RefreshPreview()
    {
        StartPuzzle();
        ready = false;
        if (timerLabel != null) timerLabel.text = "00:00";
    }

    private void StartPuzzle()
    {
        int language = Mathf.Clamp(WordSearchSession.Language, 0, 2);
        mode = WordSearchSession.Mode;
        WordSearchPuzzleDef def = WordSearchData.Puzzle(language, WordSearchSession.Category, WordSearchSession.Puzzle);
        if (def == null)
        {
            WordSearchSession.Category = 0;
            WordSearchSession.Puzzle = 0;
            def = WordSearchData.Puzzle(language, 0, 0);
        }
        if (def == null)
        {
            Debug.LogError("WordSearchGameController: no puzzle data.");
            return;
        }

        ResumeStore.Record(ResumeStore.GameWordSearch, language, WordSearchSession.Category, WordSearchSession.Puzzle);   // the Continue card of the Main menu opens this puzzle again
        int seed = (language * 100 + WordSearchSession.Category * 10 + WordSearchSession.Puzzle) * 7919 + 13;
        grid = WordSearchGenerator.Generate(def.words, seed);
        if (grid == null)
        {
            Debug.LogError("WordSearchGameController: could not fit the words in the grid.");
            return;
        }

        // the word list runs from the shortest word to the longest
        listOrder.Clear();
        listOrder.AddRange(grid.placements);
        listOrder.Sort(delegate (WordSearchPlacement a, WordSearchPlacement b)
        {
            int byLength = a.word.Length.CompareTo(b.word.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(a.word, b.word);
        });

        if (board != null) board.Build(grid);
        if (languageLabel != null) languageLabel.text = WordSearchData.LanguageNames[language];
        if (listTitle != null)
        {
            listTitle.text = mode == WordSearchMode.Meaning ? "Find the " + WordSearchData.LanguageNames[language] + " word" : "Words";
        }

        foundCount = 0;
        hintsLeft = hintsPerPuzzle;
        hintsUsed = 0;
        elapsed = 0f;
        ready = true;
        RefreshHintLabel();
        RefreshList();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !resultShown)
        {
            if (paused) ClosePause(); else OpenPause();
        }

        if (ready && !paused && !resultShown)
        {
            elapsed += Time.unscaledDeltaTime;
            if (timerLabel != null)
            {
                timerLabel.text = FormatTime(Mathf.FloorToInt(elapsed));
            }
        }

        if (resultShown && confettiRoot != null && confettiRoot.gameObject.activeSelf)
        {
            AnimateConfetti();
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            OpenPause();
        }
    }

    private static string FormatTime(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }

    // ------------------------------------------------------------------ selecting words

    private void OnSelectionReleased(int r1, int c1, int r2, int c2)
    {
        if (!ready || paused || resultShown || grid == null)
        {
            board.ClearSelection();
            return;
        }

        string line = WordSearchBoardView.ReadLine(grid, r1, c1, r2, c2);
        char[] reversed = line.ToCharArray();
        System.Array.Reverse(reversed);
        string backwards = new string(reversed);

        foreach (WordSearchPlacement p in grid.placements)
        {
            if (p.found)
            {
                continue;
            }
            bool forward = p.word == line;
            if (forward || p.word == backwards)
            {
                // the capsule goes where the player drew it (a word may appear by chance in more than one place)
                int sr = forward ? r1 : r2, sc = forward ? c1 : c2;
                int er = forward ? r2 : r1, ec = forward ? c2 : c1;
                p.row = sr;
                p.col = sc;
                p.dRow = System.Math.Sign(er - sr);
                p.dCol = System.Math.Sign(ec - sc);
                p.found = true;
                p.colorIndex = foundCount;
                foundCount++;
                board.MarkFound(p.row, p.col, p.dRow, p.dCol, p.word.Length, board.FoundColor(p.colorIndex));
                RefreshList();
                ShowDescription(p);
                if (foundCount >= grid.placements.Count)
                {
                    StartCoroutine(ShowResultAfterDelay());
                }
                return;
            }
        }

        board.ClearSelection();
    }

    private IEnumerator ShowResultAfterDelay()
    {
        ready = false;
        board.InputEnabled = false;
        yield return new WaitForSecondsRealtime(0.9f);
        ShowResult();
    }

    // ------------------------------------------------------------------ word list

    // ------------------------------------------------------------------ description card

    /// <summary>Builds the description card in the free space of the top bar, between the timer and the "Found" chip.</summary>
    private void BuildDescriptionCard()
    {
        if (descriptionTitle != null || foundLabel == null || foundLabel.transform.parent == null || foundLabel.transform.parent.parent == null)
        {
            return;
        }
        Transform parent = foundLabel.transform.parent.parent;
        Image card = LibraryUiKit.Box(parent, "description_card", new Color32(253, 248, 238, 255), true);
        LibraryUiKit.Place(card.rectTransform, 800, 30, 680, 116);
        descriptionTitle = LibraryUiKit.Label(card.transform, string.Empty, 32, textColor, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        LibraryUiKit.Place(descriptionTitle.rectTransform, 24, 4, 632, 40);
        descriptionBody = LibraryUiKit.Label(card.transform, string.Empty, 28, new Color32(107, 82, 55, 255), FontStyles.Normal, TextAlignmentOptions.TopLeft, true);
        LibraryUiKit.Place(descriptionBody.rectTransform, 24, 44, 632, 66);
        descriptionBody.enableAutoSizing = true;
        descriptionBody.fontSizeMin = 20f;
        descriptionBody.fontSizeMax = 28f;
        foreach (TMP_Text t in new[] { descriptionTitle, descriptionBody })
        {
            if (foundLabel.fontSharedMaterial != null) t.fontSharedMaterial = foundLabel.fontSharedMaterial;
        }
        ShowDescription(null);
    }

    /// <summary>Lets the player tap a word in the list to read what it means.</summary>
    private void MakeRowsTappable()
    {
        if (rowRoots == null)
        {
            return;
        }
        for (int i = 0; i < rowRoots.Length; i++)
        {
            if (rowRoots[i] == null)
            {
                continue;
            }
            int index = i;
            Image rowImage = rowRoots[i].GetComponent<Image>();
            if (rowImage == null)
            {
                continue;
            }
            rowImage.raycastTarget = true;
            Button rowButton = rowRoots[i].GetComponent<Button>();
            if (rowButton == null)
            {
                rowButton = rowRoots[i].AddComponent<Button>();
                rowButton.targetGraphic = rowImage;
                rowButton.transition = Selectable.Transition.None;
            }
            rowButton.onClick.RemoveAllListeners();
            rowButton.onClick.AddListener(delegate
            {
                if (index < listOrder.Count) ShowDescription(listOrder[index]);
            });
        }
    }

    /// <summary>Shows the word and its description in the card (null: the hint to tap a word).</summary>
    private void ShowDescription(WordSearchPlacement p)
    {
        if (descriptionTitle == null || descriptionBody == null)
        {
            return;
        }
        if (p == null)
        {
            descriptionTitle.text = "What does it mean?";
            descriptionBody.text = "Tap a word in the list, or find one in the grid.";
            return;
        }
        descriptionTitle.text = p.word;
        descriptionBody.text = string.IsNullOrEmpty(p.description) ? p.meaning : p.description;
    }

    private void RefreshList()
    {
        if (grid == null)
        {
            return;
        }
        int total = grid.placements.Count;
        if (foundLabel != null)
        {
            foundLabel.text = "Found " + foundCount + " of " + total;
        }

        // one row per word, shortest word first
        for (int i = 0; i < rowRoots.Length; i++)
        {
            bool used = i < total;
            if (rowRoots[i] != null) rowRoots[i].SetActive(used);
            if (!used)
            {
                continue;
            }
            WordSearchPlacement p = listOrder[i];
            string hexDim = ColorUtility.ToHtmlStringRGB(dimColor);
            string text;
            if (mode == WordSearchMode.Meaning)
            {
                text = p.found
                    ? "<color=#" + hexDim + "><s>" + p.meaning + "</s> → " + p.word + "</color>"
                    : p.meaning + " · " + Blanks(p.word.Length);
            }
            else
            {
                text = p.found
                    ? "<color=#" + hexDim + "><s>" + p.word + "</s> · " + p.meaning + "</color>"
                    : p.word;
            }
            if (rowLabels[i] != null) rowLabels[i].text = text;
            if (rowDots[i] != null)
            {
                Color dot = p.found ? board.FoundColor(p.colorIndex) : dotOffColor;
                dot.a = 1f;
                rowDots[i].color = dot;
            }
        }
    }

    private static string Blanks(int count)
    {
        var parts = new string[count];
        for (int i = 0; i < count; i++)
        {
            parts[i] = "_";
        }
        return string.Join(" ", parts);
    }

    // ------------------------------------------------------------------ hint

    private void RefreshHintLabel()
    {
        if (hintLabel != null)
        {
            hintLabel.text = "Hint · " + hintsLeft;
        }
        if (hintButton != null)
        {
            hintButton.interactable = hintsLeft > 0;
        }
    }

    private void UseHint()
    {
        if (!ready || paused || resultShown || grid == null)
        {
            return;
        }
        if (hintsLeft <= 0)
        {
            ShowToast("No hints left");
            return;
        }

        var open = new List<WordSearchPlacement>();
        foreach (WordSearchPlacement p in grid.placements)
        {
            if (!p.found) open.Add(p);
        }
        if (open.Count == 0)
        {
            return;
        }
        WordSearchPlacement pick = open[Random.Range(0, open.Count)];
        hintsLeft--;
        hintsUsed++;
        RefreshHintLabel();
        board.FlashCell(pick.row, pick.col, 2.6f);
        ShowToast(mode == WordSearchMode.Meaning ? "A word for \"" + pick.meaning + "\" starts here" : "A word starts here");
    }

    private void ShowToast(string message)
    {
        if (toast != null)
        {
            toast.Show(message);
        }
    }

    // ------------------------------------------------------------------ pause window

    /// <summary>Opens the pause window: stops the stopwatch, blocks the grid and hides the board.</summary>
    public void OpenPause()
    {
        if (paused || resultShown || !ready)
        {
            return;
        }
        paused = true;
        if (board != null) board.InputEnabled = false;
        if (pauseTimeLabel != null) pauseTimeLabel.text = FormatTime(Mathf.FloorToInt(elapsed));
        if (boardGroup != null)
        {
            boardGroup.alpha = 0f;
            boardGroup.blocksRaycasts = false;
        }
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    /// <summary>Closes the pause window and continues the puzzle.</summary>
    public void ClosePause()
    {
        if (!paused)
        {
            return;
        }
        paused = false;
        if (board != null) board.InputEnabled = true;
        if (boardGroup != null)
        {
            boardGroup.alpha = 1f;
            boardGroup.blocksRaycasts = true;
        }
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // ------------------------------------------------------------------ result card

    private void ShowResult()
    {
        if (resultShown)
        {
            return;
        }
        resultShown = true;
        if (board != null) board.InputEnabled = false;

        var words = new List<string>();
        foreach (WordSearchPlacement p in grid.placements)
        {
            words.Add(p.word);
        }
        int language = Mathf.Clamp(WordSearchSession.Language, 0, 2);
        WordSearchProgressStore.MarkSolved(language, WordSearchSession.Category, WordSearchSession.Puzzle);
        ResumeStore.MarkFinished(ResumeStore.GameWordSearch, language, WordSearchSession.Category, WordSearchSession.Puzzle);
        if (PlayerDatabase.Instance != null)
        {
            PlayerDatabase.Instance.RecordWordSearchWin(language, words);
        }

        if (resultTitle != null)
        {
            resultTitle.text = (language == 2 ? "Tama!" : "Husto!") + " All found";
        }
        if (statTime != null) statTime.text = FormatTime(Mathf.FloorToInt(elapsed));
        if (statHints != null) statHints.text = hintsUsed.ToString();
        if (statStreak != null)
        {
            int streak = Mathf.Max(1, StreakTracker.GetStreak());
            statStreak.text = streak == 1 ? "1 day" : streak + " days";
        }

        BuildLearnedList(language);
        if (confettiRoot != null) confettiRoot.gameObject.SetActive(true);
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            StartCoroutine(PopIn(resultCard));
        }
    }

    private void BuildLearnedList(int language)
    {
        if (resultListContent == null || resultRowTemplate == null)
        {
            return;
        }
        for (int i = resultListContent.childCount - 1; i >= 0; i--)
        {
            Transform child = resultListContent.GetChild(i);
            if (child.gameObject != resultRowTemplate)
            {
                Destroy(child.gameObject);
            }
        }

        foreach (WordSearchPlacement p in grid.placements)
        {
            GameObject row = Instantiate(resultRowTemplate, resultListContent);
            row.SetActive(true);
            row.name = "Row_" + p.word;
            SetText(row.transform.Find("Word"), p.word);
            SetText(row.transform.Find("Meaning"), p.meaning);

            // Tap to hear: the speaker beside the star plays the recording of the word
            Transform speaker = row.transform.Find("Speaker");
            Button speakerButton = speaker != null ? speaker.GetComponent<Button>() : null;
            if (speakerButton != null)
            {
                if (WordAudio.Enabled)
                {
                    speaker.gameObject.SetActive(true);
                    string spoken = p.word;
                    speakerButton.onClick.AddListener(delegate { WordAudio.Play(language, spoken); });
                }
                else
                {
                    speaker.gameObject.SetActive(false);
                }
            }

            Transform star = row.transform.Find("Star");
            if (star != null)
            {
                Image starImage = star.GetComponent<Image>();
                Button starButton = star.GetComponent<Button>();
                string word = p.word;
                if (starImage != null) starImage.color = LibraryFavoritesStore.IsFavorite(language, word) ? starOnColor : starOffColor;
                if (starButton != null)
                {
                    starButton.onClick.AddListener(delegate
                    {
                        bool now = LibraryFavoritesStore.Toggle(language, word);
                        if (starImage != null) starImage.color = now ? starOnColor : starOffColor;
                    });
                }
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(resultListContent);
    }

    private static void SetText(Transform t, string text)
    {
        if (t == null)
        {
            return;
        }
        TMP_Text label = t.GetComponent<TMP_Text>();
        if (label != null)
        {
            label.text = text;
        }
    }

    private IEnumerator PopIn(RectTransform card)
    {
        if (card == null)
        {
            yield break;
        }
        float t = 0f;
        while (t < 0.28f)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
            float s = Mathf.Lerp(0.85f, 1f, u);
            card.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        card.localScale = Vector3.one;
    }

    private void AnimateConfetti()
    {
        for (int i = 0; i < confettiRoot.childCount; i++)
        {
            RectTransform piece = (RectTransform)confettiRoot.GetChild(i);
            float speed = 90f + (i % 5) * 35f;
            Vector2 p = piece.anchoredPosition;
            p.y -= speed * Time.unscaledDeltaTime;
            if (p.y < -1120f)
            {
                p.y = 60f;
            }
            piece.anchoredPosition = p;
            piece.Rotate(0f, 0f, (40f + (i % 4) * 25f) * Time.unscaledDeltaTime);
        }
    }

    // ------------------------------------------------------------------ navigation

    /// <summary>Plays the puzzle after this one (next letter, then the next category); after the last one it returns to the start screen.</summary>
    public void NextPuzzle()
    {
        int nextCategory, nextPuzzle;
        if (WordSearchSession.TryGetNext(WordSearchSession.Language, WordSearchSession.Category, WordSearchSession.Puzzle, out nextCategory, out nextPuzzle))
        {
            WordSearchSession.Category = nextCategory;
            WordSearchSession.Puzzle = nextPuzzle;
            SceneManager.LoadScene(WordSearchSession.PlayScene);
            return;
        }
        ChoosePuzzle();
    }

    /// <summary>Back to the start screen to pick another category, puzzle or mode.</summary>
    public void ChoosePuzzle()
    {
        SceneManager.LoadScene(WordSearchSession.StartScene);
    }

    /// <summary>Goes back to the language hub with the three minigames.</summary>
    public void BackToGames()
    {
        SceneManager.LoadScene(HubScene);
    }

    /// <summary>Opens the Library.</summary>
    public void OpenLibrary()
    {
        SceneManager.LoadScene(LibraryScene);
    }
}
