using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// CrosswordGameController
/// Runs the redesigned Crossword screen on top of the existing CrosswordInputController / CrosswordCluePanel:
///  - the active clue card (number, direction, letter count, the full description) with previous / next arrows,
///  - the Across / Down tabs of the clue list (the tab follows the active word, the list scrolls to it),
///  - a stopwatch, the language chip and a pause button (Esc and the app going to the background also pause),
///  - Hint (reveal one letter, 3 per puzzle), Check (mark wrong letters in the active word) and Reveal (fill the active word, once),
///  - an optional on-screen keypad, hidden by default and switched on and off with the Keypad switch (remembered in SaveStore;
///    the physical keyboard always works). With the keypad on the clue list becomes shorter to make room for the keys,
///  - the pause window (Resume, New puzzle, Back to games) and a win card listing every word the player learned, with a star to
///    save each word to the Library favorites, plus time, hints used and streak.
/// There are no tries or time limit: the puzzle ends when every word is solved. "New puzzle" and "Next puzzle" load another level of
/// the same language (scene "puzzle-crossword-generator N Language"). References are assigned in the scene by CrosswordDesignBuilder.
/// In category mode (scene "Crossword - Play", set up by CrosswordCategoryBootstrap) the puzzles of a category are played in order (Small,
/// Medium, Large, then the next category), solved puzzles are saved (CrosswordProgressStore) and the pause window offers "Choose puzzle".
/// </summary>
public class CrosswordGameController : MonoBehaviour
{
    private const string HubScene = "3 Language_Selection_Menu";
    private const string LibraryScene = "4 Library_menu";
    private const string KeypadPrefKey = "crossword.keypad";

    [Header("Game parts")]
    [SerializeField] private CrosswordInputController input;
    [SerializeField] private string languageName;
    [SerializeField] private int levelsPerLanguage = 10;
    [SerializeField] private int hintsPerPuzzle = 3;
    [SerializeField] private int revealsPerPuzzle = 1;

    [Header("Top bar")]
    [SerializeField] private TMP_Text languageLabel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Toggle keypadToggle;
    [SerializeField] private Image keypadTrack;
    [SerializeField] private RectTransform keypadKnob;

    [Header("Board")]
    [SerializeField] private CanvasGroup board;
    [SerializeField] private HubToast toast;

    [Header("Active clue card")]
    [SerializeField] private TMP_Text clueLabel;
    [SerializeField] private TMP_Text clueText;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Tools")]
    [SerializeField] private Button hintButton;
    [SerializeField] private TMP_Text hintLabel;
    [SerializeField] private Button checkButton;
    [SerializeField] private Button revealButton;
    [SerializeField] private TMP_Text revealLabel;

    [Header("Clue list")]
    [SerializeField] private RectTransform listCard;
    [SerializeField] private float listTallHeight = 592f;
    [SerializeField] private float listShortHeight = 292f;
    [SerializeField] private Button acrossTab;
    [SerializeField] private Image acrossTabImage;
    [SerializeField] private TMP_Text acrossTabLabel;
    [SerializeField] private Button downTab;
    [SerializeField] private Image downTabImage;
    [SerializeField] private TMP_Text downTabLabel;
    [SerializeField] private ScrollRect acrossScroll;
    [SerializeField] private ScrollRect downScroll;

    [Header("Keypad")]
    [SerializeField] private GameObject keypadRoot;
    [SerializeField] private WordleKeyButton[] keys;

    [Header("Pause window")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private TMP_Text pauseTimeLabel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseNewButton;
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
    [SerializeField] private Color tabOnColor = new Color32(27, 58, 140, 255);
    [SerializeField] private Color tabOffColor = new Color32(239, 231, 212, 255);
    [SerializeField] private Color switchOnColor = new Color32(93, 160, 42, 255);
    [SerializeField] private Color switchOffColor = new Color32(185, 183, 172, 255);
    [SerializeField] private Color starOnColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color starOffColor = new Color32(185, 183, 172, 255);

    private bool ready;
    private bool paused;
    private bool resultShown;
    private float elapsed;
    private int hintsLeft;
    private int revealsLeft;
    private int hintsUsed;
    private bool keypadOn;
    private Coroutine clearWrongRoutine;
    private CrosswordDirection currentTab = CrosswordDirection.Across;

    // category mode (scene "Crossword - Play"): the puzzle is one size of a category, set up by CrosswordCategoryBootstrap
    private bool categoryMode;
    private int categoryLanguage;
    private int categoryIndex;
    private int categorySize;

    /// <summary>
    /// Turns on category mode (called by CrosswordCategoryBootstrap before Start): the language comes from the session instead of the scene
    /// name, solved puzzles are saved, "Next puzzle" moves to the next size or category and the pause window offers "Choose puzzle".
    /// </summary>
    public void ConfigureCategory(int language, int category, int size)
    {
        categoryMode = true;
        categoryLanguage = language;
        categoryIndex = category;
        categorySize = size;
        ResumeStore.Record(ResumeStore.GameCrossword, language, category, size);   // the Continue card of the Main menu opens this puzzle again
        languageName = CrosswordCategoryData.LanguageNames[Mathf.Clamp(language, 0, 2)];
    }

    private void Awake()
    {
        Hook(pauseButton, OpenPause);
        Hook(resumeButton, ClosePause);
        Hook(pauseNewButton, NextPuzzle);
        Hook(pauseBackButton, BackToGames);
        Hook(resultNextButton, NextPuzzle);
        Hook(resultLibraryButton, OpenLibrary);
        Hook(resultBackButton, BackToGames);
        Hook(previousButton, delegate { if (input != null) input.GoToPreviousClue(); });
        Hook(nextButton, delegate { if (input != null) input.GoToNextClue(); });
        Hook(hintButton, UseHint);
        Hook(checkButton, UseCheck);
        Hook(revealButton, UseReveal);
        Hook(acrossTab, delegate { SetTab(CrosswordDirection.Across); });
        Hook(downTab, delegate { SetTab(CrosswordDirection.Down); });

        if (keypadToggle != null)
        {
            keypadToggle.onValueChanged.AddListener(OnKeypadToggled);
        }
        if (keys != null)
        {
            foreach (WordleKeyButton key in keys)
            {
                if (key != null)
                {
                    key.Pressed += OnKeyPressed;
                }
            }
        }

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

        if (string.IsNullOrEmpty(languageName))
        {
            Match m = Regex.Match(gameObject.scene.name, @"^(.*\D)\s(\d+)\s(\S+)$");
            languageName = m.Success ? m.Groups[3].Value : "Cebuano";
        }
        if (languageLabel != null) languageLabel.text = languageName;
        if (categoryMode && pauseNewButton != null)
        {
            TMP_Text pauseNewLabel = pauseNewButton.GetComponentInChildren<TMP_Text>();
            if (pauseNewLabel != null) pauseNewLabel.text = "Choose puzzle";
        }

        hintsLeft = hintsPerPuzzle;
        revealsLeft = revealsPerPuzzle;
        RefreshToolLabels();

        keypadOn = SaveStore.GetInt(KeypadPrefKey, 0) == 1;
        if (keypadToggle != null) keypadToggle.SetIsOnWithoutNotify(keypadOn);
        ApplyKeypadState();

        if (input != null)
        {
            input.PuzzleReady += OnPuzzleReady;
            input.ActiveClueChanged += OnActiveClueChanged;
            input.PuzzleCompleted += OnPuzzleCompleted;
        }
    }

    private void OnDestroy()
    {
        if (input != null)
        {
            input.PuzzleReady -= OnPuzzleReady;
            input.ActiveClueChanged -= OnActiveClueChanged;
            input.PuzzleCompleted -= OnPuzzleCompleted;
        }
    }

    /// <summary>
    /// Edit-mode helper used by the design builder (and the context menu): fills the language chip, the active clue card,
    /// the tool labels and the clue list size from the puzzle currently built in the scene, so the saved scene shows the design
    /// before pressing Play.
    /// </summary>
    [ContextMenu("Refresh preview")]
    public void RefreshPreview()
    {
        if (languageLabel != null)
        {
            Match m = Regex.Match(gameObject.scene.name, @"^(.*\D)\s(\d+)\s(\S+)$");
            languageLabel.text = m.Success ? m.Groups[3].Value : "Cebuano";
        }
        hintsLeft = hintsPerPuzzle;
        revealsLeft = revealsPerPuzzle;
        RefreshToolLabels();
        if (timerLabel != null) timerLabel.text = "00:00";
        FitContent(acrossScroll);
        FitContent(downScroll);
        if (input != null && input.ActiveEntry != null)
        {
            OnActiveClueChanged(input.ActiveEntry);
        }
        else
        {
            SetTab(CrosswordDirection.Across);
        }
        keypadOn = false;
        ApplyKeypadState();
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

    // ------------------------------------------------------------------ puzzle events

    private void OnPuzzleReady()
    {
        ready = true;
        elapsed = 0f;
        FitContent(acrossScroll);
        FitContent(downScroll);
    }

    private void OnActiveClueChanged(CrosswordWordEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        if (clueLabel != null)
        {
            clueLabel.text = entry.number + " " + entry.direction + " · " + entry.answer.Length + " letters";
        }
        if (clueText != null)
        {
            clueText.text = entry.clue;
        }

        SetTab(entry.direction);
        ScrollRect scroll = entry.direction == CrosswordDirection.Across ? acrossScroll : downScroll;
        EnsureVisible(scroll, entry);
    }

    private void OnPuzzleCompleted()
    {
        StartCoroutine(ShowResultAfterDelay());
    }

    private IEnumerator ShowResultAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.9f);
        ShowResult();
    }

    // ------------------------------------------------------------------ clue list

    private void SetTab(CrosswordDirection direction)
    {
        currentTab = direction;
        bool across = direction == CrosswordDirection.Across;
        if (acrossScroll != null) acrossScroll.gameObject.SetActive(across);
        if (downScroll != null) downScroll.gameObject.SetActive(!across);
        if (acrossTabImage != null) acrossTabImage.color = across ? tabOnColor : tabOffColor;
        if (downTabImage != null) downTabImage.color = across ? tabOffColor : tabOnColor;
        if (acrossTabLabel != null) acrossTabLabel.color = across ? Color.white : new Color32(59, 42, 26, 255);
        if (downTabLabel != null) downTabLabel.color = across ? new Color32(59, 42, 26, 255) : Color.white;
    }

    private static void FitContent(ScrollRect scroll)
    {
        if (scroll == null || scroll.content == null)
        {
            return;
        }
        float bottom = 0f;
        foreach (CrosswordClueRow row in scroll.content.GetComponentsInChildren<CrosswordClueRow>(true))
        {
            RectTransform rt = (RectTransform)row.transform;
            bottom = Mathf.Max(bottom, -rt.anchoredPosition.y + rt.sizeDelta.y);
        }
        scroll.content.sizeDelta = new Vector2(scroll.content.sizeDelta.x, bottom + 8f);
    }

    private static void EnsureVisible(ScrollRect scroll, CrosswordWordEntry entry)
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null)
        {
            return;
        }
        foreach (CrosswordClueRow row in scroll.content.GetComponentsInChildren<CrosswordClueRow>(true))
        {
            if (row.Entry != entry)
            {
                continue;
            }
            RectTransform rt = (RectTransform)row.transform;
            float top = -rt.anchoredPosition.y;
            float bottom = top + rt.sizeDelta.y;
            float viewHeight = scroll.viewport.rect.height;
            float offset = scroll.content.anchoredPosition.y;
            if (top < offset)
            {
                offset = top - 8f;
            }
            else if (bottom > offset + viewHeight)
            {
                offset = bottom - viewHeight + 8f;
            }
            float maxOffset = Mathf.Max(0f, scroll.content.rect.height - viewHeight);
            offset = Mathf.Clamp(offset, 0f, maxOffset);
            scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, offset);
            return;
        }
    }

    // ------------------------------------------------------------------ keypad

    private void OnKeypadToggled(bool on)
    {
        keypadOn = on;
        SaveStore.SetInt(KeypadPrefKey, on ? 1 : 0);
        SaveStore.Save();
        ApplyKeypadState();
    }

    private void ApplyKeypadState()
    {
        if (keypadRoot != null) keypadRoot.SetActive(keypadOn);

        if (listCard != null)
        {
            float height = keypadOn ? listShortHeight : listTallHeight;
            listCard.sizeDelta = new Vector2(listCard.sizeDelta.x, height);
            ResizeScroll(acrossScroll, height - 98f);
            ResizeScroll(downScroll, height - 98f);
        }

        if (keypadTrack != null) keypadTrack.color = keypadOn ? switchOnColor : switchOffColor;
        if (keypadKnob != null) keypadKnob.anchoredPosition = new Vector2(keypadOn ? 72f : 4f, keypadKnob.anchoredPosition.y);

        if (input != null && input.ActiveEntry != null)
        {
            ScrollRect scroll = input.ActiveEntry.direction == CrosswordDirection.Across ? acrossScroll : downScroll;
            EnsureVisible(scroll, input.ActiveEntry);
        }
    }

    private static void ResizeScroll(ScrollRect scroll, float height)
    {
        if (scroll == null)
        {
            return;
        }
        RectTransform rt = (RectTransform)scroll.transform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(60f, height));
    }

    private void OnKeyPressed(WordleKeyButton key)
    {
        if (paused || resultShown || input == null)
        {
            return;
        }
        if (key.kind == WordleKeyKind.Letter)
        {
            input.TypeLetterFromUi(key.letter);
        }
        else if (key.kind == WordleKeyKind.Backspace)
        {
            input.BackspaceFromUi();
        }
    }

    // ------------------------------------------------------------------ tools

    private void RefreshToolLabels()
    {
        if (hintLabel != null) hintLabel.text = "Hint · " + hintsLeft;
        if (revealLabel != null) revealLabel.text = "Reveal · " + revealsLeft;
    }

    private void UseHint()
    {
        if (paused || resultShown || input == null)
        {
            return;
        }
        if (hintsLeft <= 0)
        {
            ShowToast("No hints left");
            return;
        }
        if (input.RevealOneLetter())
        {
            hintsLeft--;
            hintsUsed++;
            RefreshToolLabels();
        }
        else
        {
            ShowToast("This word is already right");
        }
    }

    private void UseCheck()
    {
        if (paused || resultShown || input == null)
        {
            return;
        }
        int wrong = input.CheckActiveWord();
        ShowToast(wrong == 0 ? "No wrong letters in this word" : (wrong == 1 ? "1 letter needs fixing" : wrong + " letters need fixing"));
        if (wrong > 0)
        {
            if (clearWrongRoutine != null) StopCoroutine(clearWrongRoutine);
            clearWrongRoutine = StartCoroutine(ClearWrongLater());
        }
    }

    private IEnumerator ClearWrongLater()
    {
        yield return new WaitForSecondsRealtime(2f);
        if (input != null) input.ClearWrongMarks();
        clearWrongRoutine = null;
    }

    private void UseReveal()
    {
        if (paused || resultShown || input == null)
        {
            return;
        }
        if (revealsLeft <= 0)
        {
            ShowToast("No reveals left");
            return;
        }
        if (input.RevealActiveWord())
        {
            revealsLeft--;
            hintsUsed++;
            RefreshToolLabels();
        }
        else
        {
            ShowToast("This word is already right");
        }
    }

    private void ShowToast(string message)
    {
        if (toast != null)
        {
            toast.Show(message);
        }
    }

    // ------------------------------------------------------------------ pause window

    /// <summary>Opens the pause window: stops the stopwatch, blocks typing and hides the board.</summary>
    public void OpenPause()
    {
        if (paused || resultShown || !ready)
        {
            return;
        }
        paused = true;
        if (input != null) input.InputEnabled = false;
        if (pauseTimeLabel != null) pauseTimeLabel.text = FormatTime(Mathf.FloorToInt(elapsed));
        if (board != null)
        {
            board.alpha = 0f;
            board.blocksRaycasts = false;
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
        if (input != null) input.InputEnabled = true;
        if (board != null)
        {
            board.alpha = 1f;
            board.blocksRaycasts = true;
        }
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // ------------------------------------------------------------------ result card

    private int LanguageIndex
    {
        get { return languageName == "Cebuano" ? 0 : (languageName == "Tagalog" ? 2 : 1); }
    }

    private void ShowResult()
    {
        if (resultShown)
        {
            return;
        }
        resultShown = true;
        if (input != null) input.InputEnabled = false;

        if (input != null && PlayerDatabase.Instance != null)
        {
            // counts toward the Profile screen (finished puzzles per language, best level, words learned)
            Match levelMatch = Regex.Match(gameObject.scene.name, @"^(.*\D)\s(\d+)\s(\S+)$");
            int level = levelMatch.Success ? int.Parse(levelMatch.Groups[2].Value) : 1;
            if (categoryMode)
            {
                level = categorySize + 1;
                // gold chip on the start screen and the "N of 3 solved" tags
                CrosswordProgressStore.MarkSolved(categoryLanguage, categoryIndex, categorySize);
                ResumeStore.MarkFinished(ResumeStore.GameCrossword, categoryLanguage, categoryIndex, categorySize);
            }
            var answers = new List<string>();
            foreach (CrosswordWordEntry entry in input.Entries)
            {
                answers.Add(entry.answer);
            }
            PlayerDatabase.Instance.RecordCrosswordWin(LanguageIndex, level, answers);
        }

        if (resultTitle != null)
        {
            resultTitle.text = (languageName == "Tagalog" ? "Tama!" : "Husto!") + " Puzzle done";
        }
        if (categoryMode && resultNextButton != null)
        {
            int nextCategory, nextSize;
            bool hasNext = CrosswordSession.TryGetNext(categoryLanguage, categoryIndex, categorySize, out nextCategory, out nextSize);
            TMP_Text nextLabel = resultNextButton.GetComponentInChildren<TMP_Text>();
            if (nextLabel != null) nextLabel.text = hasNext ? "Next puzzle" : "Back to topics";
        }
        if (statTime != null) statTime.text = FormatTime(Mathf.FloorToInt(elapsed));
        if (statHints != null) statHints.text = hintsUsed.ToString();
        if (statStreak != null)
        {
            int streak = Mathf.Max(1, StreakTracker.GetStreak());
            statStreak.text = streak == 1 ? "1 day" : streak + " days";
        }

        BuildLearnedList();
        if (confettiRoot != null) confettiRoot.gameObject.SetActive(true);
        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            StartCoroutine(PopIn(resultCard));
        }
    }

    private void BuildLearnedList()
    {
        if (resultListContent == null || resultRowTemplate == null || input == null)
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

        foreach (CrosswordWordEntry entry in input.Entries)
        {
            GameObject row = Instantiate(resultRowTemplate, resultListContent);
            row.SetActive(true);
            row.name = "Row_" + entry.answer;
            SetText(row.transform.Find("Word"), entry.answer);
            SetText(row.transform.Find("Meaning"), ShortMeaning(entry.clue));

            // Tap to hear: the speaker beside the star plays the recording of the word
            Transform speaker = row.transform.Find("Speaker");
            Button speakerButton = speaker != null ? speaker.GetComponent<Button>() : null;
            if (speakerButton != null)
            {
                if (WordAudio.Enabled)
                {
                    speaker.gameObject.SetActive(true);
                    string spoken = entry.answer;
                    speakerButton.onClick.AddListener(delegate { WordAudio.Play(LanguageIndex, spoken); });
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
                string word = entry.answer;
                if (starImage != null) starImage.color = LibraryFavoritesStore.IsFavorite(LanguageIndex, word) ? starOnColor : starOffColor;
                if (starButton != null)
                {
                    starButton.onClick.AddListener(delegate
                    {
                        bool now = LibraryFavoritesStore.Toggle(LanguageIndex, word);
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

    /// <summary>Turns a clue such as "This is a dish of vegetables..." into a short meaning: "a dish of vegetables...".</summary>
    private static string ShortMeaning(string clue)
    {
        string s = (clue ?? string.Empty).Trim();
        string[] leads =
        {
            "This word refers to ", "This word indicates ", "This word means ", "This refers to ", "This describes ",
            "This indicates ", "This means ", "This is ", "This word "
        };
        foreach (string lead in leads)
        {
            if (s.StartsWith(lead, System.StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(lead.Length);
                break;
            }
        }
        s = s.TrimEnd('.', ' ');
        if (s.Length > 0)
        {
            s = char.ToLowerInvariant(s[0]) + s.Substring(1);
        }
        return s;
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

    /// <summary>Loads another level of the same language (never the one being played).</summary>
    public void NextPuzzle()
    {
        if (categoryMode)
        {
            NextInCategory();
            return;
        }

        Match m = Regex.Match(gameObject.scene.name, @"^(.*\D)\s(\d+)\s(\S+)$");
        if (m.Success && levelsPerLanguage > 1)
        {
            int current = int.Parse(m.Groups[2].Value);
            int next = current;
            for (int tries = 0; tries < 20 && next == current; tries++)
            {
                next = Random.Range(1, levelsPerLanguage + 1);
            }
            string sceneName = m.Groups[1].Value + " " + next + " " + m.Groups[3].Value;
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                StreakTracker.RecordPlay();
                SceneManager.LoadScene(sceneName);
                return;
            }
        }
        BackToGames();
    }

    /// <summary>
    /// Category mode: after a win the next size of the category (then the next category) is loaded; from the pause window the player goes
    /// back to the start screen to choose a puzzle.
    /// </summary>
    private void NextInCategory()
    {
        if (!resultShown)
        {
            ChoosePuzzle();
            return;
        }

        int nextCategory, nextSize;
        if (CrosswordSession.TryGetNext(categoryLanguage, categoryIndex, categorySize, out nextCategory, out nextSize))
        {
            CrosswordSession.Category = nextCategory;
            CrosswordSession.Size = nextSize;
            StreakTracker.RecordPlay();
            SceneManager.LoadScene(CrosswordSession.PlayScene);
            return;
        }
        ChoosePuzzle();
    }

    /// <summary>Back to the Crossword start screen to pick another category or size.</summary>
    public void ChoosePuzzle()
    {
        SceneManager.LoadScene(CrosswordSession.StartScene);
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
