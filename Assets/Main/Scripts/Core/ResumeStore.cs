using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ResumeStore
/// Remembers the last puzzle the player opened (the game, the language, the category and the level, size or puzzle) so the "Continue" card
/// on the Main menu can take them back to it. The play screens of Wordle, Crossword and Word Search call Record(...) when a puzzle starts;
/// the values are kept in SaveStore ("resume.game", "resume.language", "resume.category", "resume.index"), so they survive closing the game.
///  - Has: whether there is something to continue.
///  - Describe(...): the line shown on the card, for example "Wordle · Cebuano · Food and drink · Level 4", and whether that puzzle is
///    finished. While the remembered puzzle is not solved the card is full and continues it; once it is solved there is nothing to resume, so
///    the card turns hollow (an outline only) and can not be pressed, until the player opens another puzzle.
///  - Open(): fills the game's session (WordleSession / CrosswordSession / WordSearchSession) and loads its play scene (does nothing when the
///    puzzle is finished).
///  - Clear(): forgets it (used by "Reset progress").
/// A puzzle that was left half-way starts again from the beginning: the letters typed so far are not saved.
/// </summary>
public static class ResumeStore
{
    public const int GameNone = 0;
    public const int GameWordle = 1;
    public const int GameCrossword = 2;
    public const int GameWordSearch = 3;

    private const string GameKey = "resume.game";
    private const string LanguageKey = "resume.language";
    private const string CategoryKey = "resume.category";
    private const string IndexKey = "resume.index";
    private const string DoneKey = "resume.done";

    /// <summary>True when a puzzle is remembered.</summary>
    public static bool Has
    {
        get { return SaveStore.GetInt(GameKey, GameNone) != GameNone; }
    }

    /// <summary>
    /// Remembers the puzzle that was just opened (index = level, size or puzzle number, counted from 0). It counts as "not finished" again,
    /// even when the player solved the same puzzle before: finished only means that the puzzle that is open now was won.
    /// </summary>
    public static void Record(int game, int language, int category, int index)
    {
        SaveStore.SetInt(GameKey, game);
        SaveStore.SetInt(LanguageKey, language);
        SaveStore.SetInt(CategoryKey, category);
        SaveStore.SetInt(IndexKey, index);
        SaveStore.SetInt(DoneKey, 0);
    }

    /// <summary>Called when a puzzle is won: if it is the remembered one, the Continue card turns hollow.</summary>
    public static void MarkFinished(int game, int language, int category, int index)
    {
        if (SaveStore.GetInt(GameKey, GameNone) == game && SaveStore.GetInt(LanguageKey, -1) == language
            && SaveStore.GetInt(CategoryKey, -1) == category && SaveStore.GetInt(IndexKey, -1) == index)
        {
            SaveStore.SetInt(DoneKey, 1);
        }
    }

    /// <summary>Forgets the remembered puzzle.</summary>
    public static void Clear()
    {
        SaveStore.DeleteKey(GameKey);
        SaveStore.DeleteKey(LanguageKey);
        SaveStore.DeleteKey(CategoryKey);
        SaveStore.DeleteKey(IndexKey);
        SaveStore.DeleteKey(DoneKey);
    }

    /// <summary>
    /// The remembered puzzle, and whether the player has won it since it was opened (the "resume.done" flag). Returns false when nothing is remembered or the puzzle no longer exists.
    /// </summary>
    private static bool Target(out int game, out int language, out int category, out int index, out bool finished)
    {
        game = SaveStore.GetInt(GameKey, GameNone);
        language = Mathf.Clamp(SaveStore.GetInt(LanguageKey, 0), 0, 2);
        category = Mathf.Max(0, SaveStore.GetInt(CategoryKey, 0));
        index = Mathf.Max(0, SaveStore.GetInt(IndexKey, 0));
        finished = SaveStore.GetInt(DoneKey, 0) == 1;
        switch (game)
        {
            case GameWordle:
                return WordleData.Level(language, category, index) != null;
            case GameCrossword:
                return CrosswordCategoryData.Puzzle(language, category, index) != null;
            case GameWordSearch:
                return WordSearchData.Puzzle(language, category, index) != null;
            default:
                return false;
        }
    }

    /// <summary>
    /// The text for the card: title ("Continue") and a line such as "Wordle · Cebuano · Food and drink · Level 4"; finished is true when that puzzle
    /// is already solved (the card is then hollow and can not be pressed). False when nothing is remembered at all.
    /// </summary>
    public static bool Describe(out string title, out string line, out bool finished)
    {
        title = "Continue";
        line = string.Empty;
        int game, language, category, index;
        if (!Target(out game, out language, out category, out index, out finished))
        {
            return false;
        }
        string gameName, categoryName, part;
        switch (game)
        {
            case GameWordle:
                gameName = "Wordle";
                categoryName = WordleData.Category(language, category).name;
                part = "Level " + (index + 1);
                break;
            case GameCrossword:
                gameName = "Crossword";
                categoryName = CrosswordCategoryData.Category(language, category).name;
                part = CrosswordCategoryData.Puzzle(language, category, index).size;
                break;
            default:
                gameName = "Word Search";
                categoryName = WordSearchData.Category(language, category).name;
                part = "Puzzle " + WordSearchData.Puzzle(language, category, index).id;
                break;
        }
        line = gameName + " · " + WordleData.LanguageNames[language] + " · " + categoryName + " · " + part + (finished ? " · Finished" : string.Empty);
        return true;
    }

    /// <summary>Opens the remembered puzzle in its play scene. Does nothing when there is none or it is already finished.</summary>
    public static void Open()
    {
        int game, language, category, index;
        bool finished;
        if (!Target(out game, out language, out category, out index, out finished) || finished)
        {
            return;
        }
        switch (game)
        {
            case GameWordle:
                WordleSession.Language = language;
                WordleSession.Category = category;
                WordleSession.Level = index;
                SceneManager.LoadScene(WordleSession.PlayScene);
                break;
            case GameCrossword:
                CrosswordSession.Language = language;
                CrosswordSession.Category = category;
                CrosswordSession.Size = index;
                SceneManager.LoadScene(CrosswordSession.PlayScene);
                break;
            default:
                WordSearchSession.Language = language;
                WordSearchSession.Category = category;
                WordSearchSession.Puzzle = index;
                SceneManager.LoadScene(WordSearchSession.PlayScene);
                break;
        }
    }
}
