using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Which game's topic card opened the Learn screen (and where "Back" and "Start puzzle" lead).</summary>
public enum LearnGame
{
    Wordle,
    Crossword,
    WordSearch
}

/// <summary>
/// LearnSession
/// Static hand-over for the Learn screen (scene "Learn - Cards"): which game opened it, the language, the topic (category index) and the set
/// (0, 1, 2 = A, B, C, the same word sets as the three Word Search puzzles of the topic). The Learn button on a topic card (LearnTopicButton)
/// fills it through Open(), LearnController reads it. Language index everywhere: 0 Cebuano, 1 Ilonggo, 2 Tagalog.
/// </summary>
public static class LearnSession
{
    public const string LearnScene = "Learn - Cards";

    public static LearnGame Game;
    public static int Language;
    public static int Category;
    public static int Set;

    /// <summary>Stores what to learn and opens the Learn screen.</summary>
    public static void Open(LearnGame game, int language, int category)
    {
        Game = game;
        Language = Mathf.Clamp(language, 0, 2);
        Category = Mathf.Max(0, category);
        Set = 0;
        SceneManager.LoadScene(LearnScene);
    }

    /// <summary>The start (topic) screen of the game that opened the Learn screen.</summary>
    public static string ReturnScene()
    {
        switch (Game)
        {
            case LearnGame.Crossword: return CrosswordSession.StartScene;
            case LearnGame.WordSearch: return WordSearchSession.StartScene;
            default: return WordleSession.StartScene;
        }
    }
}
