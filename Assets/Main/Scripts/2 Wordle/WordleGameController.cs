using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// WordleGameController
/// Runs the redesigned Wordle screen on top of the existing WordleKeyboardTyper / WordleWordVerifier / WordleTimer:
///  - shows the language, the clue (definition from the puzzle's JSON) and an optional hint (first letter and length),
///  - drives the on-screen keyboard (the physical keyboard keeps working) and colours its keys green / yellow / gray,
///  - rejects words that are not in the language's dictionary (the row shakes, no try is used),
///  - shows the tries left, and turns the timer orange in the last 10 seconds,
///  - shows a result card when the word is found, the tries run out, or the time is up (word, meaning, time, tries,
///    streak, and buttons for the next word, the Library and the language hub),
///  - provides the pause window (Esc key, the pause button, or the app going to the background): the timer stops, typing is
///    blocked and the board is hidden so the clock cannot be paused just to think.
/// "Next word" loads another of the language's levels (scene "puzzle-wordle-generator N Language"), never the same one twice
/// in a row. In category mode (scene "Wordle - Play", set up by WordleCategoryBootstrap) it instead plays the levels of a category in
/// order, saves solved levels (WordleProgressStore) and the pause window offers "Choose level". Every reference is assigned in the scene by the Wordle design builder (Tools > Wordle); the scene keeps working
/// without internet or any extra data besides the dictionary JSON assigned to Dictionary Json.
/// </summary>
public class WordleGameController : MonoBehaviour
{
    private const string HubScene = "3 Language_Selection_Menu";
    private const string LibraryScene = "4 Library_menu";

    [Header("Game parts")]
    [SerializeField] private WordleKeyboardTyper typer;
    [SerializeField] private WordleWordVerifier verifier;
    [SerializeField] private WordleRowsColumnGenerator generator;
    [SerializeField] private WordleTimer timer;
    [Tooltip("Dictionary JSON of this puzzle's language, used to reject made-up words.")]
    [SerializeField] private TextAsset dictionaryJson;
    [Tooltip("Shown on the language chip and in messages. Left empty, it is read from the scene name.")]
    [SerializeField] private string languageName;
    [Tooltip("How many levels each language has (scene names \"puzzle-wordle-generator 1 Cebuano\" ... \"10 Cebuano\").")]
    [SerializeField] private int levelsPerLanguage = 10;
    [Tooltip("How many hints the Hint button gives per puzzle. Each hint names a letter the player does not have in the right place yet.")]
    [SerializeField] private int hintsPerPuzzle = 2;
    [Tooltip("Off (default): every full row is accepted, a wrong word moves to the next row and uses a try. On: a word that is not in the dictionary is rejected (the row shakes, no try is used).")]
    [SerializeField] private bool rejectUnknownWords = false;

    [Header("Top bar")]
    [SerializeField] private TMP_Text languageLabel;
    [SerializeField] private TMP_Text timerLabel;
    [SerializeField] private Image[] triesDots;
    [SerializeField] private Button pauseButton;
    [SerializeField] private Color triesLeftColor = new Color32(93, 160, 42, 255);
    [SerializeField] private Color triesUsedColor = new Color32(185, 183, 172, 255);
    [SerializeField] private Color timerNormalColor = new Color32(59, 42, 26, 255);
    [SerializeField] private Color timerWarningColor = new Color32(232, 117, 26, 255);

    [Header("Board")]
    [SerializeField] private CanvasGroup board;
    [SerializeField] private TMP_Text clueLabel;
    [SerializeField] private TMP_Text clueText;
    [SerializeField] private Button hintButton;
    [SerializeField] private TMP_Text hintButtonLabel;
    [SerializeField] private WordleKeyButton[] keys;
    [Tooltip("Off (default): the on-screen keypad stays plain after a guess. On: keys turn green / yellow / gray like the tiles.")]
    [SerializeField] private bool colorKeyboardKeys = false;
    [SerializeField] private HubToast toast;

    [Header("Pause window")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private TMP_Text pauseTimeLabel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseNewWordButton;
    [SerializeField] private Button pauseBackButton;

    [Header("Result card")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private RectTransform resultCard;
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text resultSubtitle;
    [SerializeField] private RectTransform resultTiles;
    [SerializeField] private WordleTileView resultTileTemplate;
    [SerializeField] private TMP_Text resultDefinition;
    [SerializeField] private TMP_Text resultMeaning;
    [Tooltip("Star beside the meaning: saves the answer word to the Library favorites (same list as the stars on the Crossword and Word Search result cards).")]
    [SerializeField] private Button resultSaveButton;
    [SerializeField] private Image resultSaveStar;
    [Tooltip("Speaker beside the meaning: plays the recording of the answer word (WordAudio).")]
    [SerializeField] private Button resultSpeakButton;
    [SerializeField] private Color saveStarOnColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color saveStarOffColor = new Color32(185, 183, 172, 255);
    [SerializeField] private GameObject statsRoot;
    [SerializeField] private TMP_Text statTime;
    [SerializeField] private TMP_Text statTries;
    [SerializeField] private TMP_Text statStreak;
    [SerializeField] private TMP_Text resultNextLabel;
    [SerializeField] private Button resultNextButton;
    [SerializeField] private Button resultLibraryButton;
    [SerializeField] private Button resultBackButton;
    [SerializeField] private RectTransform confettiRoot;

    private enum ResultKind
    {
        Win,
        OutOfTries,
        TimeUp
    }

    private WordleWordValidator validator;
    private bool paused;
    private bool resultShown;
    private bool resultIsWin;
    private int hintsLeft;
    private readonly HashSet<int> correctPositions = new HashSet<int>();
    private readonly HashSet<int> hintedPositions = new HashSet<int>();
    private readonly List<string> hintTexts = new List<string>();
    private bool subscribed;
    private bool inputLocked;
    private int saveLanguageIndex;
    private string saveWord = string.Empty;
    private bool leadInShown;

    // category mode (scene "Wordle - Play"): the word is one level of a category, set up by WordleCategoryBootstrap
    private bool categoryMode;
    private int categoryLanguage;
    private int categoryIndex;
    private int categoryLevel;

    /// <summary>
    /// Turns on category mode (called by WordleCategoryBootstrap before Start): the language comes from the session instead of the scene
    /// name, solved levels are saved, "Next word" moves to the next level of the category and the pause window offers "Choose level".
    /// </summary>
    public void ConfigureCategory(int language, int category, int level)
    {
        categoryMode = true;
        categoryLanguage = language;
        categoryIndex = category;
        categoryLevel = level;
        languageName = WordleData.LanguageNames[Mathf.Clamp(language, 0, 2)];
    }

    private void Awake()
    {
        Hook(pauseButton, OpenPause);
        Hook(hintButton, UseHint);
        Hook(resumeButton, ClosePause);
        Hook(pauseNewWordButton, NextWord);
        Hook(pauseBackButton, BackToGames);
        Hook(resultNextButton, NextWord);
        Hook(resultLibraryButton, OpenLibrary);
        Hook(resultBackButton, BackToGames);
        Hook(resultSaveButton, ToggleSaveWord);
        Hook(resultSpeakButton, SpeakWord);
        if (resultSpeakButton != null && Application.isPlaying) resultSpeakButton.gameObject.SetActive(WordAudio.Enabled);

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
        if (resultTileTemplate != null) resultTileTemplate.gameObject.SetActive(false);
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
        if (clueText != null && verifier != null) clueText.text = verifier.Definition;
        if (categoryMode && pauseNewWordButton != null)
        {
            TMP_Text pauseNewLabel = pauseNewWordButton.GetComponentInChildren<TMP_Text>();
            if (pauseNewLabel != null) pauseNewLabel.text = "Choose level";
        }

        ResetTries();
        hintsLeft = hintsPerPuzzle;
        RefreshHintLabel();

        validator = new WordleWordValidator(verifier != null ? verifier.TargetWord : string.Empty);
        if (typer != null)
        {
            typer.WordValidator = rejectUnknownWords ? (System.Func<string, bool>)IsAcceptedWord : null;
            typer.RowSubmitted += OnRowSubmitted;
            typer.InvalidWord += OnInvalidWord;
            typer.TooShort += OnTooShort;
            typer.OutOfTries += OnOutOfTries;
        }
        if (timer != null)
        {
            timer.Expired += OnTimeUp;
        }
        subscribed = true;

        if (rejectUnknownWords)
        {
            StartCoroutine(LoadDictionary());
        }
    }

    private void OnDestroy()
    {
        if (!subscribed)
        {
            return;
        }
        if (typer != null)
        {
            typer.RowSubmitted -= OnRowSubmitted;
            typer.InvalidWord -= OnInvalidWord;
            typer.TooShort -= OnTooShort;
            typer.OutOfTries -= OnOutOfTries;
        }
        if (timer != null)
        {
            timer.Expired -= OnTimeUp;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !resultShown)
        {
            if (paused) ClosePause(); else OpenPause();
        }

        UpdateInputLock();

        if (timerLabel != null && timer != null && typer != null)
        {
            bool warn = timer.IsCounting && !timer.IsPaused && timer.RemainingSeconds <= 10f && !typer.Finished;
            timerLabel.color = warn ? timerWarningColor : timerNormalColor;
        }

        if (resultShown && resultIsWin && confettiRoot != null)
        {
            AnimateConfetti();
        }
    }

    /// <summary>
    /// Typing (physical and on-screen keyboard) is blocked during the short "Get ready" lead-in before the real
    /// countdown starts, while the pause window or the result card is open, and once the puzzle is over.
    /// </summary>
    private void UpdateInputLock()
    {
        if (typer == null || timer == null)
        {
            return;
        }

        bool locked = paused || resultShown || timer.IsLeadIn;
        if (locked == inputLocked && leadInShown)
        {
            return;
        }

        if (!leadInShown && timer.IsLeadIn)
        {
            leadInShown = true;
            ShowToast("Get ready...", Mathf.Max(0.5f, timer.RemainingSeconds));
        }
        else if (!leadInShown)
        {
            leadInShown = true;
        }

        inputLocked = locked;
        typer.InputEnabled = !locked;
        typer.RefreshCursor();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            OpenPause();
        }
    }

    // ------------------------------------------------------------------ dictionary

    private IEnumerator LoadDictionary()
    {
        yield return null;
        if (dictionaryJson != null && generator != null)
        {
            validator.Load(dictionaryJson.text, generator.columns);
        }
    }

    private bool IsAcceptedWord(string guess)
    {
        return validator == null || validator.Contains(guess);
    }

    // ------------------------------------------------------------------ keyboard

    private void OnKeyPressed(WordleKeyButton key)
    {
        if (paused || resultShown || typer == null || (timer != null && timer.IsLeadIn))
        {
            return;
        }
        switch (key.kind)
        {
            case WordleKeyKind.Letter:
                typer.TypeLetter(key.letter);
                break;
            case WordleKeyKind.Enter:
                typer.TryAdvanceRow();
                break;
            default:
                typer.Backspace();
                break;
        }
    }

    // ------------------------------------------------------------------ guesses

    private void ResetTries()
    {
        if (triesDots == null)
        {
            return;
        }
        foreach (Image dot in triesDots)
        {
            if (dot != null) dot.color = triesLeftColor;
        }
    }

    private void OnRowSubmitted(int row, bool correct)
    {
        if (triesDots != null && row >= 0 && row < triesDots.Length && triesDots[row] != null)
        {
            triesDots[row].color = triesUsedColor;
        }

        if (verifier != null && verifier.LastStates != null)
        {
            for (int i = 0; i < verifier.LastStates.Length; i++)
            {
                if (verifier.LastStates[i] == WordleWordVerifier.LetterState.Correct)
                {
                    correctPositions.Add(i);
                }
            }
        }

        string letters = typer.ReadRow(row);
        WordleWordVerifier.LetterState[] states = verifier != null && verifier.LastStates != null
            ? (WordleWordVerifier.LetterState[])verifier.LastStates.Clone()
            : null;
        StartCoroutine(AfterReveal(letters, states, correct));
    }

    private IEnumerator AfterReveal(string letters, WordleWordVerifier.LetterState[] states, bool correct)
    {
        float wait = verifier != null ? verifier.RevealDuration : 0f;
        yield return new WaitForSecondsRealtime(wait);

        // keyboard colouring is switched off by default so players cannot read which letters are right or wrong from the keypad
        if (colorKeyboardKeys && states != null)
        {
            for (int i = 0; i < letters.Length && i < states.Length; i++)
            {
                WordleKeyButton key = FindKey(letters[i]);
                if (key == null)
                {
                    continue;
                }
                int rank = states[i] == WordleWordVerifier.LetterState.Correct ? 3 : (states[i] == WordleWordVerifier.LetterState.WrongPosition ? 2 : 1);
                key.SetState(rank, verifier.ColorFor(states[i]));
            }
        }

        if (correct)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            ShowResult(ResultKind.Win);
        }
        else if (typer != null && typer.Failed)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            ShowResult(ResultKind.OutOfTries);
        }
    }

    private WordleKeyButton FindKey(char letter)
    {
        char upper = char.ToUpperInvariant(letter);
        if (keys == null)
        {
            return null;
        }
        foreach (WordleKeyButton key in keys)
        {
            if (key != null && key.kind == WordleKeyKind.Letter && char.ToUpperInvariant(key.letter) == upper)
            {
                return key;
            }
        }
        return null;
    }

    private void OnInvalidWord(int row)
    {
        ShowToast("Not in the " + languageName + " dictionary");
        StartCoroutine(ShakeRow(row, true));
    }

    private void OnTooShort(int row)
    {
        ShowToast("Not enough letters");
        StartCoroutine(ShakeRow(row, false));
    }

    private void OnOutOfTries()
    {
        // The result card is shown by AfterReveal, once the last row has finished flipping.
    }

    private void OnTimeUp()
    {
        if (typer == null || typer.Finished || resultShown)
        {
            return;
        }
        typer.InputEnabled = false;
        typer.RefreshCursor();
        ShowResult(ResultKind.TimeUp);
    }

    private void ShowToast(string message)
    {
        if (toast != null)
        {
            toast.Show(message);
        }
    }

    private void ShowToast(string message, float seconds)
    {
        if (toast != null)
        {
            toast.Show(message, seconds);
        }
    }

    private IEnumerator ShakeRow(int row, bool markInvalid)
    {
        int columns = generator.columns;
        var tiles = new WordleTileView[columns];
        var rects = new RectTransform[columns];
        var basePos = new Vector2[columns];
        for (int c = 0; c < columns; c++)
        {
            int index = row * columns + c;
            if (index < generator.Cells.Count)
            {
                tiles[c] = generator.Cells[index].GetComponentInParent<WordleTileView>();
                if (tiles[c] != null)
                {
                    rects[c] = (RectTransform)tiles[c].transform;
                    basePos[c] = rects[c].anchoredPosition;
                    if (markInvalid) tiles[c].SetInvalid(true);
                }
            }
        }

        float t = 0f;
        const float duration = 0.42f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float offset = Mathf.Sin(t * 55f) * 14f * (1f - t / duration);
            for (int c = 0; c < columns; c++)
            {
                if (rects[c] != null) rects[c].anchoredPosition = basePos[c] + new Vector2(offset, 0f);
            }
            yield return null;
        }
        for (int c = 0; c < columns; c++)
        {
            if (rects[c] != null) rects[c].anchoredPosition = basePos[c];
        }

        if (markInvalid)
        {
            yield return new WaitForSecondsRealtime(0.5f);
            for (int c = 0; c < columns; c++)
            {
                if (tiles[c] != null) tiles[c].SetInvalid(false);
            }
        }
    }

    // ------------------------------------------------------------------ hint

    /// <summary>
    /// Hint: tells the player one letter of the word that they do not have in the right place yet. Positions already found
    /// (a green tile in an earlier row, or the right letter already typed in the current row) and positions already hinted are
    /// skipped, so every hint is a new letter. There are hintsPerPuzzle hints per puzzle.
    /// </summary>
    private void UseHint()
    {
        if (paused || resultShown || verifier == null || verifier.TargetWord.Length == 0 || (timer != null && timer.IsLeadIn))
        {
            return;
        }
        if (hintsLeft <= 0)
        {
            ShowToast("No hints left");
            return;
        }

        string word = verifier.TargetWord;
        var known = new HashSet<int>(correctPositions);
        known.UnionWith(hintedPositions);
        if (typer != null)
        {
            string typed = typer.ReadRow(typer.CurrentRow);
            for (int i = 0; i < typed.Length && i < word.Length; i++)
            {
                if (typed[i] == word[i])
                {
                    known.Add(i);
                }
            }
        }

        int position = -1;
        for (int i = 0; i < word.Length; i++)
        {
            if (!known.Contains(i))
            {
                position = i;
                break;
            }
        }
        if (position < 0)
        {
            ShowToast("You already have every letter in place");
            return;
        }

        hintedPositions.Add(position);
        hintsLeft--;
        // wording: the letter first, then the column it belongs in (columns count from 1, left to right)
        hintTexts.Add("letter " + word[position] + " is in column " + (position + 1));
        if (clueLabel != null) clueLabel.text = "Clue · " + string.Join(" · ", hintTexts.ToArray());
        RefreshHintLabel();
        ShowToast("Hint - Letter " + word[position] + " is in column " + (position + 1));
    }

    private void RefreshHintLabel()
    {
        if (hintButtonLabel != null)
        {
            hintButtonLabel.text = hintsLeft > 0 ? "Hint · " + hintsLeft : "Used";
        }
    }

    // ------------------------------------------------------------------ result card

    private void ShowResult(ResultKind kind)
    {
        if (resultShown)
        {
            return;
        }
        resultShown = true;
        resultIsWin = kind == ResultKind.Win;

        if (typer != null)
        {
            typer.InputEnabled = false;
            typer.RefreshCursor();
        }

        string word = verifier != null ? verifier.TargetWord : string.Empty;
        bool tagalog = languageName == "Tagalog";
        bool cebuano = languageName == "Cebuano";

        if (kind == ResultKind.Win && PlayerDatabase.Instance != null)
        {
            // counts toward the Profile screen (solved words per language, words learned)
            int languageIndex = cebuano ? 0 : (tagalog ? 2 : 1);
            PlayerDatabase.Instance.RecordWordleWin(languageIndex, word);
        }
        if (kind == ResultKind.Win && categoryMode)
        {
            // gold chip on the start screen and the "N of 6 solved" tags
            WordleProgressStore.MarkSolved(categoryLanguage, categoryIndex, categoryLevel);
        }

        if (resultTitle != null)
        {
            resultTitle.text = kind == ResultKind.Win ? (tagalog ? "Tama!" : "Husto!")
                : kind == ResultKind.OutOfTries ? (cebuano ? "Hapit na!" : "Malapit na!")
                : "Time's up";
        }
        if (resultSubtitle != null)
        {
            resultSubtitle.text = kind == ResultKind.Win ? "You got it" : "The word was";
        }
        if (resultDefinition != null && verifier != null) resultDefinition.text = verifier.Definition;
        if (resultMeaning != null)
        {
            // the English meaning on its own, big and green, so the player learns what the word means
            string meaning = verifier != null ? verifier.Translation : string.Empty;
            resultMeaning.text = meaning;
        }
        saveLanguageIndex = cebuano ? 0 : (tagalog ? 2 : 1);
        saveWord = word;
        RefreshSaveStar();

        BuildResultTiles(word);

        if (statsRoot != null) statsRoot.SetActive(kind == ResultKind.Win);
        if (kind == ResultKind.Win)
        {
            if (statTime != null && timer != null) statTime.text = WordleTimer.FormatTime(Mathf.RoundToInt(timer.ElapsedSeconds));
            if (statTries != null && typer != null) statTries.text = (typer.CurrentRow + 1) + " of " + generator.rows;
            if (statStreak != null)
            {
                int streak = Mathf.Max(1, StreakTracker.GetStreak());
                statStreak.text = streak == 1 ? "1 day" : streak + " days";
            }
        }
        if (resultNextLabel != null)
        {
            if (categoryMode)
            {
                int nextCategory, nextLevel;
                bool hasNext = WordleSession.TryGetNext(categoryLanguage, categoryIndex, categoryLevel, out nextCategory, out nextLevel);
                resultNextLabel.text = kind == ResultKind.Win ? (hasNext ? "Next level" : "Back to topics") : "Try again";
            }
            else
            {
                resultNextLabel.text = kind == ResultKind.Win ? "Next word" : "Try another";
            }
        }
        if (confettiRoot != null) confettiRoot.gameObject.SetActive(kind == ResultKind.Win);

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
            StartCoroutine(PopIn(resultCard));
        }
    }

    /// <summary>Tap to hear: plays the recording of the answer word.</summary>
    private void SpeakWord()
    {
        if (!string.IsNullOrEmpty(saveWord))
        {
            WordAudio.Play(saveLanguageIndex, saveWord);
        }
    }

    private void RefreshSaveStar()
    {
        if (resultSaveStar != null)
        {
            bool saved = !string.IsNullOrEmpty(saveWord) && LibraryFavoritesStore.IsFavorite(saveLanguageIndex, saveWord);
            resultSaveStar.color = saved ? saveStarOnColor : saveStarOffColor;
        }
    }

    /// <summary>Star pressed: adds the answer word to the Library favorites, or removes it.</summary>
    private void ToggleSaveWord()
    {
        if (string.IsNullOrEmpty(saveWord))
        {
            return;
        }
        LibraryFavoritesStore.Toggle(saveLanguageIndex, saveWord);
        RefreshSaveStar();
    }

    private void BuildResultTiles(string word)
    {
        if (resultTiles == null || resultTileTemplate == null)
        {
            return;
        }
        for (int i = resultTiles.childCount - 1; i >= 0; i--)
        {
            Transform child = resultTiles.GetChild(i);
            if (child != resultTileTemplate.transform)
            {
                Destroy(child.gameObject);
            }
        }

        int n = Mathf.Max(1, word.Length);
        float size = Mathf.Min(128f, (840f - 16f * (n - 1)) / n);
        Color green = verifier != null ? verifier.correctColor : new Color32(93, 160, 42, 255);
        for (int i = 0; i < word.Length; i++)
        {
            WordleTileView tile = Instantiate(resultTileTemplate, resultTiles);
            tile.gameObject.SetActive(true);
            tile.name = "ResultTile_" + word[i];
            ((RectTransform)tile.transform).sizeDelta = new Vector2(size, size);
            TMP_Text text = tile.GetComponentInChildren<TMP_Text>();
            if (text != null) text.fontSize = size * 0.55f;
            tile.ShowResultNow(green, word[i].ToString());
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

    // ------------------------------------------------------------------ pause window

    /// <summary>Opens the pause window: stops the timer, blocks typing and hides the board.</summary>
    public void OpenPause()
    {
        if (paused || resultShown || typer == null || typer.Finished)
        {
            return;
        }
        paused = true;
        if (timer != null)
        {
            timer.Pause();
            if (pauseTimeLabel != null) pauseTimeLabel.text = timer.DisplayText;
        }
        typer.InputEnabled = false;
        typer.RefreshCursor();
        if (board != null)
        {
            board.alpha = 0f;
            board.blocksRaycasts = false;
        }
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    /// <summary>Closes the pause window and continues the game.</summary>
    public void ClosePause()
    {
        if (!paused)
        {
            return;
        }
        paused = false;
        if (timer != null) timer.Resume();
        if (typer != null)
        {
            typer.InputEnabled = true;
            typer.RefreshCursor();
        }
        if (board != null)
        {
            board.alpha = 1f;
            board.blocksRaycasts = true;
        }
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    // ------------------------------------------------------------------ navigation

    /// <summary>Loads another level of the same language (never the one being played).</summary>
    public void NextWord()
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
    /// Category mode: after a win the next level of the category (then the next category) is loaded; after a loss the same level is tried
    /// again; from the pause window the player goes back to the start screen to choose a level.
    /// </summary>
    private void NextInCategory()
    {
        if (!resultShown)
        {
            ChooseLevel();
            return;
        }

        if (!resultIsWin)
        {
            StreakTracker.RecordPlay();
            SceneManager.LoadScene(WordleSession.PlayScene);
            return;
        }

        int nextCategory, nextLevel;
        if (WordleSession.TryGetNext(categoryLanguage, categoryIndex, categoryLevel, out nextCategory, out nextLevel))
        {
            WordleSession.Category = nextCategory;
            WordleSession.Level = nextLevel;
            StreakTracker.RecordPlay();
            SceneManager.LoadScene(WordleSession.PlayScene);
            return;
        }
        ChooseLevel();
    }

    /// <summary>Back to the Wordle start screen to pick another category or level.</summary>
    public void ChooseLevel()
    {
        SceneManager.LoadScene(WordleSession.StartScene);
    }

    /// <summary>Goes back to the language hub with the three minigames.</summary>
    public void BackToGames()
    {
        SceneManager.LoadScene(HubScene);
    }

    /// <summary>Opens the Library with this puzzle's word already searched.</summary>
    public void OpenLibrary()
    {
        if (verifier != null)
        {
            LibraryController.PendingSearch = verifier.TargetWord.ToLowerInvariant();
            LibraryController.PendingLanguage = languageName == "Cebuano" ? 0 : (languageName == "Tagalog" ? 2 : 1);
        }
        SceneManager.LoadScene(LibraryScene);
    }
}
