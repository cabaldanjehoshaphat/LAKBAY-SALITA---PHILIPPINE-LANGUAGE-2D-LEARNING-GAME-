using UnityEngine;
using UnityEngine.SceneManagement;

// Centralized scene-navigation helper for the menu flow. Attach one instance to a
// "SceneManager" GameObject in each menu scene and wire UI Button OnClick events to
// its public methods to move between scenes (all scenes must be added to Build Settings).
public class SceneNavigator : MonoBehaviour
{
    [Header("Only needed on the Language Selection scene")]
    [SerializeField] private LanguageSelector languageSelector;
    [SerializeField] private string[] cebuanoWordleScenes;
    [SerializeField] private string[] ilonggoWordleScenes;
    [SerializeField] private string[] tagalogWordleScenes;
    [SerializeField] private string[] cebuanoCrosswordScenes;
    [SerializeField] private string[] ilonggoCrosswordScenes;
    [SerializeField] private string[] tagalogCrosswordScenes;
    [SerializeField] private string[] cebuanoWordSearchScenes;
    [SerializeField] private string[] ilonggoWordSearchScenes;
    [SerializeField] private string[] tagalogWordSearchScenes;
    [Tooltip("Optional pop-up used to say a minigame has no levels yet (see HubToast).")]
    [SerializeField] private HubToast hubToast;

    private const string MainMenuScene = "1 Main_menu";
    private const string ProfileIconScene = "2 Profile Icon";
    private const string LanguageSelectionScene = "3 Language_Selection_Menu";
    private const string LibraryMenuScene = "4 Library_menu";
    private const string OptionsScene = "5 Options";

    public void LoadMainMenu() => SceneManager.LoadScene(MainMenuScene);
    public void LoadProfileIcon() => SceneManager.LoadScene(ProfileIconScene);
    public void LoadLanguageSelectionMenu() => SceneManager.LoadScene(LanguageSelectionScene);
    public void LoadLibraryMenu() => SceneManager.LoadScene(LibraryMenuScene);
    public void LoadOptionsMenu() => SceneManager.LoadScene(OptionsScene);

    // Wired to the Language Selection scene's wordle_button. Remembers the checked language and opens the
    // Wordle start screen (scene "Wordle - Start": pick a category and a level). If that scene is not in
    // Build Settings it falls back to loading a random puzzle scene of the checked language.
    public void LoadRandomWordlePuzzle()
    {
        if (languageSelector == null)
        {
            Debug.LogWarning("SceneNavigator: languageSelector is not assigned.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(WordleSession.StartScene))
        {
            WordleSession.Language = (int)languageSelector.SelectedLanguage;
            SceneManager.LoadScene(WordleSession.StartScene);
            return;
        }

        string[] pool = languageSelector.SelectedLanguage switch
        {
            PuzzleLanguage.Cebuano => cebuanoWordleScenes,
            PuzzleLanguage.Ilonggo => ilonggoWordleScenes,
            PuzzleLanguage.Tagalog => tagalogWordleScenes,
            _ => null
        };

        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning("SceneNavigator: no puzzle scenes configured for the selected language.");
            return;
        }

        StreakTracker.RecordPlay();
        SceneManager.LoadScene(pool[Random.Range(0, pool.Length)]);
    }

    // Wired to the Language Selection scene's crossword_button. Remembers the checked language and opens the
    // Crossword start screen (scene "Crossword - Start": pick a category and a size). If that scene is not in
    // Build Settings it falls back to loading a random crossword scene (Assets/Main/Scenes/3 Crossword/<N> -
    // Puzzle - <Language>) of the checked language.
    public void LoadRandomCrosswordPuzzle()
    {
        if (languageSelector == null)
        {
            Debug.LogWarning("SceneNavigator: languageSelector is not assigned.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(CrosswordSession.StartScene))
        {
            CrosswordSession.Language = (int)languageSelector.SelectedLanguage;
            SceneManager.LoadScene(CrosswordSession.StartScene);
            return;
        }

        string[] pool = languageSelector.SelectedLanguage switch
        {
            PuzzleLanguage.Cebuano => cebuanoCrosswordScenes,
            PuzzleLanguage.Ilonggo => ilonggoCrosswordScenes,
            PuzzleLanguage.Tagalog => tagalogCrosswordScenes,
            _ => null
        };

        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning("SceneNavigator: no crossword scenes configured for the selected language.");
            return;
        }

        StreakTracker.RecordPlay();
        SceneManager.LoadScene(pool[Random.Range(0, pool.Length)]);
    }

    // Wired to the Language Selection scene's Word Search card. Remembers the checked language and opens
    // the Word Search start screen (scene "WordSearch - Start": pick a category, puzzle and mode). If that
    // scene is not in Build Settings it falls back to the old per-language scene lists, and finally to a
    // "coming soon" message.
    public void LoadRandomWordSearchPuzzle()
    {
        if (languageSelector == null)
        {
            Debug.LogWarning("SceneNavigator: languageSelector is not assigned.");
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(WordSearchSession.StartScene))
        {
            WordSearchSession.Language = (int)languageSelector.SelectedLanguage;
            SceneManager.LoadScene(WordSearchSession.StartScene);
            return;
        }

        string[] pool = languageSelector.SelectedLanguage switch
        {
            PuzzleLanguage.Cebuano => cebuanoWordSearchScenes,
            PuzzleLanguage.Ilonggo => ilonggoWordSearchScenes,
            PuzzleLanguage.Tagalog => tagalogWordSearchScenes,
            _ => null
        };

        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning("SceneNavigator: no Word Search scenes configured for the selected language.");
            if (hubToast != null)
            {
                hubToast.Show("Word Search is coming soon");
            }
            return;
        }

        StreakTracker.RecordPlay();
        SceneManager.LoadScene(pool[Random.Range(0, pool.Length)]);
    }
}
