using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// LearnController
/// Runs the Learn screen (scene "Learn - Cards"): one card per word of a topic, so a new player meets the words before playing them. The card shows
/// the word, its English meaning, "Word 2 of 7" and a dot per word. A topic has 3 sets (A, B, C: the same word sets as the three Word Search
/// puzzles, 7 to 8 words each); the set chips at the top switch between them. Back and Next move through the words, "Start puzzle" starts the game
/// that opened the screen for this topic (Wordle: first unsolved level, Crossword: first unsolved size, Word Search: the puzzle of the set).
/// The top-left back button and Esc return to that game's start screen. What to show is read from LearnSession; the words come from
/// WordSearchData. References are assigned in the scene by LearnSceneBuilder.
/// </summary>
public class LearnController : MonoBehaviour
{
    [Header("Top bar")]
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text languageLabel;
    [SerializeField] private TMP_Text topicLabel;
    [SerializeField] private Button[] setButtons;
    [SerializeField] private Image[] setImages;

    [Header("Card")]
    [SerializeField] private TMP_Text counterLabel;
    [SerializeField] private TMP_Text wordLabel;
    [SerializeField] private TMP_Text meaningLabel;
    [SerializeField] private Image[] dots;

    [Header("Tap to hear")]
    [SerializeField] private Button speakButton;
    [SerializeField] private Image speakImage;

    [Header("Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button startButton;

    [Header("Colours")]
    [SerializeField] private Color chipOnColor = new Color32(232, 185, 35, 255);
    [SerializeField] private Color chipOffColor = new Color32(239, 231, 212, 255);
    [SerializeField] private Color dotOnColor = new Color32(93, 160, 42, 255);
    [SerializeField] private Color dotOffColor = new Color32(239, 231, 212, 255);

    private int language;
    private int category;
    private int set;
    private int index;
    private WordSearchPuzzleDef puzzle;

    private void Awake()
    {
        if (backButton != null) backButton.onClick.AddListener(Leave);
        if (previousButton != null) previousButton.onClick.AddListener(delegate { Step(-1); });
        if (nextButton != null) nextButton.onClick.AddListener(delegate { Step(1); });
        if (startButton != null) startButton.onClick.AddListener(StartPuzzle);
        if (speakButton != null)
        {
            speakButton.onClick.AddListener(Speak);
            if (Application.isPlaying) speakButton.gameObject.SetActive(WordAudio.Enabled);
        }
        if (setButtons != null)
        {
            for (int i = 0; i < setButtons.Length; i++)
            {
                int chosen = i;
                if (setButtons[i] != null) setButtons[i].onClick.AddListener(delegate { ChooseSet(chosen); });
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
            Leave();
        }
    }

    /// <summary>Re-reads LearnSession and shows the first word of the chosen set (also used for the edit-mode preview).</summary>
    [ContextMenu("Refresh preview")]
    public void Refresh()
    {
        language = Mathf.Clamp(LearnSession.Language, 0, 2);
        category = Mathf.Max(0, LearnSession.Category);
        set = Mathf.Max(0, LearnSession.Set);
        index = 0;
        ShowSet();
    }

    private void ShowSet()
    {
        WordSearchCategory topic = WordSearchData.Category(language, category);
        int sets = topic != null ? topic.puzzles.Count : 0;
        set = Mathf.Clamp(set, 0, Mathf.Max(0, sets - 1));
        LearnSession.Set = set;
        puzzle = WordSearchData.Puzzle(language, category, set);

        if (languageLabel != null) languageLabel.text = WordSearchData.LanguageNames[language];
        if (topicLabel != null) topicLabel.text = topic != null ? topic.name : string.Empty;
        for (int i = 0; setImages != null && i < setImages.Length; i++)
        {
            if (setImages[i] != null) setImages[i].color = i == set ? chipOnColor : chipOffColor;
            if (setButtons != null && i < setButtons.Length && setButtons[i] != null) setButtons[i].gameObject.SetActive(i < sets);
        }
        ShowWord();
    }

    private void ShowWord()
    {
        int count = puzzle != null ? puzzle.words.Count : 0;
        index = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        WordSearchWord w = count > 0 ? puzzle.words[index] : null;

        if (counterLabel != null) counterLabel.text = count > 0 ? "Word " + (index + 1) + " of " + count : string.Empty;
        if (wordLabel != null) wordLabel.text = w != null ? w.word : string.Empty;
        if (meaningLabel != null) meaningLabel.text = w != null ? w.meaning : string.Empty;
        for (int i = 0; dots != null && i < dots.Length; i++)
        {
            if (dots[i] == null) continue;
            dots[i].gameObject.SetActive(i < count);
            dots[i].color = i <= index ? dotOnColor : dotOffColor;
        }
        if (previousButton != null) previousButton.interactable = index > 0;
        if (nextButton != null) nextButton.interactable = index < count - 1;
        if (speakImage != null && Application.isPlaying)
        {
            // the Tap to hear button is dimmed while this word has no recording yet (tapping it still says so)
            Color c = speakImage.color;
            c.a = w != null && WordAudio.Has(language, w.word) ? 1f : 0.5f;
            speakImage.color = c;
        }
    }

    /// <summary>Tap to hear: plays the recording of the shown word.</summary>
    private void Speak()
    {
        WordSearchWord w = puzzle != null && index < puzzle.words.Count ? puzzle.words[index] : null;
        if (w != null)
        {
            WordAudio.Play(language, w.word);
        }
    }

    private void Step(int direction)
    {
        index += direction;
        ShowWord();
    }

    private void ChooseSet(int chosen)
    {
        set = chosen;
        index = 0;
        ShowSet();
    }

    /// <summary>Starts the game that opened the Learn screen, for this topic.</summary>
    private void StartPuzzle()
    {
        switch (LearnSession.Game)
        {
            case LearnGame.Crossword:
            {
                int sizes = Mathf.Max(1, CrosswordCategoryData.SizeCount(language, category));
                int size = 0;
                for (int s = 0; s < sizes; s++)
                {
                    if (!CrosswordProgressStore.IsSolved(language, category, s))
                    {
                        size = s;
                        break;
                    }
                }
                CrosswordSession.Language = language;
                CrosswordSession.Category = category;
                CrosswordSession.Size = size;
                StreakTracker.RecordPlay();
                SceneManager.LoadScene(CrosswordSession.PlayScene);
                break;
            }
            case LearnGame.WordSearch:
                WordSearchSession.Language = language;
                WordSearchSession.Category = category;
                WordSearchSession.Puzzle = set;
                SceneManager.LoadScene(WordSearchSession.PlayScene);
                break;
            default:
            {
                int levels = Mathf.Max(1, WordleData.LevelCount(language, category));
                int level = 0;
                for (int l = 0; l < levels; l++)
                {
                    if (!WordleProgressStore.IsSolved(language, category, l))
                    {
                        level = l;
                        break;
                    }
                }
                WordleSession.Language = language;
                WordleSession.Category = category;
                WordleSession.Level = level;
                StreakTracker.RecordPlay();
                SceneManager.LoadScene(WordleSession.PlayScene);
                break;
            }
        }
    }

    /// <summary>Back to the start screen of the game that opened the Learn screen.</summary>
    public void Leave()
    {
        SceneManager.LoadScene(LearnSession.ReturnScene());
    }
}
