using UnityEngine;

/// <summary>
/// LevelDataRegistry
/// Points the games to their per-language level files, so those files can live next to each game's scripts instead of in a
/// Resources folder. The one asset, Assets/Main/Resources/LevelDataRegistry.asset, holds three files per game, in language order
/// (0 Cebuano, 1 Ilonggo, 2 Tagalog):
///  - Wordle Files:      Assets/Main/Scripts/2 Wordle/Category Levels/&lt;N - Language&gt;/wordle_levels_&lt;language&gt;.json
///  - Crossword Files:   Assets/Main/Scripts/3 Crossword/Category Puzzles/&lt;N - Language&gt;/crossword_puzzles_&lt;language&gt;.json
///  - Word Search Files: Assets/Main/Scripts/5 Word Search/Category Puzzles/&lt;N - Language&gt;/wordsearch_puzzles_&lt;language&gt;.json
/// WordleData, CrosswordCategoryData and WordSearchData read their files through LevelDataRegistry.Instance. To swap a language's
/// file, drag another JSON into its slot on the asset in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "LevelDataRegistry", menuName = "Lakbay Salita/Level Data Registry")]
public class LevelDataRegistry : ScriptableObject
{
    private const string ResourcePath = "LevelDataRegistry";

    [Tooltip("Wordle category levels, one JSON per language: 0 Cebuano, 1 Ilonggo, 2 Tagalog.")]
    public TextAsset[] wordleFiles = new TextAsset[3];

    [Tooltip("Crossword category puzzles, one JSON per language: 0 Cebuano, 1 Ilonggo, 2 Tagalog.")]
    public TextAsset[] crosswordFiles = new TextAsset[3];

    [Tooltip("Word Search category puzzles, one JSON per language: 0 Cebuano, 1 Ilonggo, 2 Tagalog.")]
    public TextAsset[] wordSearchFiles = new TextAsset[3];

    [Tooltip("Phrasebook phrases (Assets/Main/Data/Phrasebook), one JSON per language: 0 Cebuano, 1 Ilonggo, 2 Tagalog.")]
    public TextAsset[] phrasebookFiles = new TextAsset[3];

    private static LevelDataRegistry _instance;

    /// <summary>The registry asset (Resources/LevelDataRegistry.asset), or null if it is missing.</summary>
    public static LevelDataRegistry Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<LevelDataRegistry>(ResourcePath);
                if (_instance == null)
                {
                    Debug.LogError("LevelDataRegistry: Resources/" + ResourcePath + ".asset is missing.");
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Parses every file of one game (in language order) into T, the per-language data shape. A missing or empty slot logs an
    /// error and is returned as null, so the language indexes stay in place.
    /// </summary>
    public static T[] Parse<T>(TextAsset[] files, string gameName) where T : class
    {
        if (files == null)
        {
            return new T[0];
        }
        var result = new T[files.Length];
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i] == null)
            {
                Debug.LogError("LevelDataRegistry: " + gameName + " file for language " + i + " is not assigned.");
                continue;
            }
            result[i] = JsonUtility.FromJson<T>(files[i].text);
        }
        return result;
    }
}
