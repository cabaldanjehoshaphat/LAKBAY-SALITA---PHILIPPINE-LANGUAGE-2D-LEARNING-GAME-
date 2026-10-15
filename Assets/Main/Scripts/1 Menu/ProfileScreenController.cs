using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ProfileScreenController
/// Drives the redesigned Profile screen (scene "2 Profile Icon"). It reads everything from PlayerDatabase and StreakTracker and
/// writes it onto the screen: avatar and name, "Learning since", the day streak and best streak, one row for each of Wordle, Crossword,
/// Word Search and the Phrasebook with how many of its puzzles are solved ("1 of 63 solved", or phrases learned: "3 of 53 learned") and a
/// progress bar with a count for each language (Cebuano, Ilonggo, Tagalog) side by side, the words learned, and the number of Library
/// favorites (the Favorites card opens the Library).
///
/// Edit profile opens a dialog to pick one of 10 avatar colours and change the name (saved to PlayerDatabase). "Reset progress"
/// opens a confirmation; confirming starts a fresh profile (PlayerDatabase.ResetAll, which also clears the streak) but keeps the
/// Library favorites. All references are assigned in the scene by ProfileDesignBuilder.
/// </summary>
public class ProfileScreenController : MonoBehaviour
{
    private const string LibraryScene = "4 Library_menu";
    private const int GameCount = 4;

    [Header("Profile card")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text joinedLabel;
    [SerializeField] private TMP_Text streakNumber;
    [SerializeField] private TMP_Text bestStreakLabel;
    [SerializeField] private Button editAvatarButton;
    [SerializeField] private Button editProfileButton;
    [SerializeField] private Button resetLinkButton;

    [Header("Game rows (Wordle, Crossword, Word Search, Phrasebook; each with Cebuano, Ilonggo, Tagalog)")]
    [Tooltip("One per game: the line under the game name, for example \"1 of 63 solved\".")]
    [SerializeField] private TMP_Text[] gameSummaryLabels;
    [Tooltip("Twelve labels, game by game and language by language (Wordle Cebuano, Ilonggo, Tagalog, then Crossword, then Word Search, then Phrasebook): \"1/21\".")]
    [SerializeField] private TMP_Text[] languageCountLabels;
    [Tooltip("The twelve progress bar fills, in the same order as the count labels.")]
    [SerializeField] private RectTransform[] languageBarFills;
    [SerializeField] private float barFullWidth = 190f;
    [SerializeField] private float minBarWidth = 24f;

    [Header("Words learned and favorites")]
    [SerializeField] private TMP_Text wordsLearnedValue;
    [SerializeField] private TMP_Text favoritesValue;
    [SerializeField] private Button favoritesButton;

    [Header("Edit profile dialog")]
    [SerializeField] private GameObject editPanel;
    [SerializeField] private Image[] avatarChoices;
    [SerializeField] private GameObject[] avatarChoiceRings;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;

    [Header("Reset dialog")]
    [SerializeField] private GameObject resetPanel;
    [SerializeField] private Button keepButton;
    [SerializeField] private Button resetButton;

    [Header("Avatar colours (10)")]
    [SerializeField]
    private Color[] avatarColors =
    {
        new Color32(91, 141, 220, 255), new Color32(93, 160, 42, 255), new Color32(232, 117, 26, 255), new Color32(138, 63, 199, 255),
        new Color32(212, 83, 126, 255), new Color32(232, 185, 35, 255), new Color32(29, 158, 117, 255), new Color32(55, 138, 221, 255),
        new Color32(153, 60, 29, 255), new Color32(95, 94, 90, 255)
    };

    private int selectedAvatar;

    private void Awake()
    {
        Hook(editAvatarButton, OpenEdit);
        Hook(editProfileButton, OpenEdit);
        Hook(resetLinkButton, OpenReset);
        Hook(favoritesButton, delegate { SceneManager.LoadScene(LibraryScene); });
        Hook(saveButton, SaveEdit);
        Hook(cancelButton, CloseEdit);
        Hook(keepButton, CloseReset);
        Hook(resetButton, ConfirmReset);

        if (avatarChoices != null)
        {
            for (int i = 0; i < avatarChoices.Length; i++)
            {
                int index = i;
                Button b = avatarChoices[i] != null ? avatarChoices[i].GetComponent<Button>() : null;
                if (b != null)
                {
                    b.onClick.AddListener(delegate { SelectAvatar(index); });
                }
            }
        }

        if (editPanel != null) editPanel.SetActive(false);
        if (resetPanel != null) resetPanel.SetActive(false);
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
        Refresh();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (editPanel != null && editPanel.activeSelf) CloseEdit();
            else if (resetPanel != null && resetPanel.activeSelf) CloseReset();
        }
    }

    /// <summary>Re-reads the saved profile and updates every label, bar and the avatar.</summary>
    public void Refresh()
    {
        PlayerDatabase db = PlayerDatabase.Instance;
        if (db == null)
        {
            return;
        }

        if (avatarImage != null) avatarImage.color = AvatarColor(db.AvatarIndex);
        if (nameLabel != null) nameLabel.text = DisplayName(db.PlayerNameOrId);
        if (joinedLabel != null)
        {
            joinedLabel.text = "Learning since " + db.JoinedDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        }

        int streak = StreakTracker.GetStreak();
        int best = StreakTracker.GetBestStreak();
        if (streakNumber != null) streakNumber.text = streak.ToString();
        if (bestStreakLabel != null) bestStreakLabel.text = "Best: " + best + (best == 1 ? " day" : " days");

        RefreshGameCards();

        if (wordsLearnedValue != null) wordsLearnedValue.text = db.WordsLearned.ToString();
        int favorites = LibraryFavoritesStore.Count();
        if (favoritesValue != null) favoritesValue.text = favorites + (favorites == 1 ? " word" : " words");
    }

    /// <summary>
    /// Fills the four game rows: "N of T solved" ("learned" for the Phrasebook) for each game (all languages together) and, for each
    /// language, the "solved/total" count and the progress bar. The numbers are the same ones the language hub tags show: the puzzles
    /// (levels) solved, each counted once.
    /// </summary>
    private void RefreshGameCards()
    {
        for (int game = 0; game < GameCount; game++)
        {
            int solvedAll = 0;
            int totalAll = 0;
            for (int language = 0; language < 3; language++)
            {
                int solved, total;
                GetProgress(game, language, out solved, out total);
                solvedAll += solved;
                totalAll += total;

                int index = game * 3 + language;
                if (languageCountLabels != null && index < languageCountLabels.Length && languageCountLabels[index] != null)
                {
                    languageCountLabels[index].text = solved + "/" + total;
                }
                if (languageBarFills != null && index < languageBarFills.Length && languageBarFills[index] != null)
                {
                    float share = total > 0 ? (float)solved / total : 0f;
                    float width = solved > 0 ? Mathf.Max(minBarWidth, barFullWidth * share) : 0f;
                    languageBarFills[index].sizeDelta = new Vector2(width, languageBarFills[index].sizeDelta.y);
                }
            }

            if (gameSummaryLabels != null && game < gameSummaryLabels.Length && gameSummaryLabels[game] != null)
            {
                gameSummaryLabels[game].text = solvedAll + " of " + totalAll + (game == 3 ? " learned" : " solved");
            }
        }
    }

    /// <summary>How many puzzles of a game are solved in one language and how many exist (game 0 Wordle, 1 Crossword, 2 Word Search, 3 Phrasebook phrases).</summary>
    private static void GetProgress(int game, int language, out int solved, out int total)
    {
        switch (game)
        {
            case 0:
                solved = WordleProgressStore.SolvedTotal(language);
                total = WordleData.TotalLevels(language);
                break;
            case 1:
                solved = CrosswordProgressStore.SolvedTotal(language);
                total = CrosswordCategoryData.TotalPuzzles(language);
                break;
            case 3:
                solved = PhraseProgressStore.LearnedTotal(language);
                total = PhraseData.TotalPhrases(language);
                break;
            default:
                solved = WordSearchProgressStore.SolvedTotal(language);
                total = WordSearchData.TotalPuzzles(language);
                break;
        }
    }

    private Color AvatarColor(int index)
    {
        if (avatarColors == null || avatarColors.Length == 0)
        {
            return Color.gray;
        }
        return avatarColors[Mathf.Clamp(index, 0, avatarColors.Length - 1)];
    }

    private static string DisplayName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Player";
        }
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    // ------------------------------------------------------------------ edit profile

    private void OpenEdit()
    {
        PlayerDatabase db = PlayerDatabase.Instance;
        if (db == null || editPanel == null)
        {
            return;
        }
        selectedAvatar = db.AvatarIndex;
        if (nameInput != null) nameInput.text = DisplayName(db.PlayerNameOrId);
        RefreshAvatarChoices();
        editPanel.SetActive(true);
    }

    private void CloseEdit()
    {
        if (editPanel != null) editPanel.SetActive(false);
    }

    private void SelectAvatar(int index)
    {
        selectedAvatar = index;
        RefreshAvatarChoices();
    }

    private void RefreshAvatarChoices()
    {
        if (avatarChoices == null)
        {
            return;
        }
        for (int i = 0; i < avatarChoices.Length; i++)
        {
            if (avatarChoices[i] != null) avatarChoices[i].color = AvatarColor(i);
            if (avatarChoiceRings != null && i < avatarChoiceRings.Length && avatarChoiceRings[i] != null)
            {
                avatarChoiceRings[i].SetActive(i == selectedAvatar);
            }
        }
    }

    private void SaveEdit()
    {
        PlayerDatabase db = PlayerDatabase.Instance;
        if (db != null)
        {
            string newName = nameInput != null ? nameInput.text.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(newName))
            {
                db.PlayerNameOrId = newName;
            }
            db.AvatarIndex = selectedAvatar;
        }
        CloseEdit();
        Refresh();
    }

    // ------------------------------------------------------------------ reset progress

    private void OpenReset()
    {
        if (resetPanel != null) resetPanel.SetActive(true);
    }

    private void CloseReset()
    {
        if (resetPanel != null) resetPanel.SetActive(false);
    }

    private void ConfirmReset()
    {
        if (PlayerDatabase.Instance != null)
        {
            PlayerDatabase.Instance.ResetAll();
        }
        CloseReset();
        Refresh();
    }
}
