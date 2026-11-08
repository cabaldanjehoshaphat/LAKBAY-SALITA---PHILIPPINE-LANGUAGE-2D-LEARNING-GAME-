using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WordleCategoryBootstrap
/// Sets up the Wordle category play scene ("Wordle - Play") before the game starts. That one scene plays every category level: it reads
/// the chosen language, category and level from WordleSession, gives the word, its meaning and its clue to the WordleWordVerifier, sets
/// the number of columns to the word's length (4-7 letters), sets the 5:00 timer, switches the WordleGameController into category mode
/// and fills the category chip in the top bar (name, colour dot and "Level N of 6"). It runs very early (Awake, execution order -200),
/// before the verifier, the generator, the timer and the controller start. If the scene is opened directly without a session it plays
/// level 1 of the first category. References are assigned in the scene by WordleCategoryBuilder.
/// </summary>
[DefaultExecutionOrder(-200)]
public class WordleCategoryBootstrap : MonoBehaviour
{
    [SerializeField] private WordleWordVerifier verifier;
    [SerializeField] private WordleRowsColumnGenerator generator;
    [SerializeField] private WordleTimer timer;
    [SerializeField] private WordleGameController controller;
    [SerializeField] private int timerSeconds = 300;

    [Header("Category chip (top bar)")]
    [SerializeField] private Image categoryDot;
    [SerializeField] private TMP_Text categoryNameLabel;
    [SerializeField] private TMP_Text categoryLevelLabel;

    private void Awake()
    {
        int language = Mathf.Clamp(WordleSession.Language, 0, 2);
        int category = WordleSession.Category;
        int level = WordleSession.Level;

        WordleLevelDef def = WordleData.Level(language, category, level);
        if (def == null)
        {
            category = 0;
            level = 0;
            WordleSession.Category = 0;
            WordleSession.Level = 0;
            def = WordleData.Level(language, 0, 0);
        }
        if (def == null)
        {
            Debug.LogError("WordleCategoryBootstrap: no Wordle level data (Data/Wordle/Levels, see Resources/LevelDataRegistry.asset).");
            return;
        }

        if (verifier != null) verifier.SetTarget(def.word, def.meaning, def.clue);
        if (generator != null) generator.columns = Mathf.Max(1, def.word.Length);
        if (timer != null) timer.OverrideDuration(timerSeconds);
        if (timer != null && !TimerModes.IsTimed(ScoreRules.GameWordle)) timer.UseStopwatch();
        if (controller != null) controller.ConfigureCategory(language, category, level);

        WordleCategoryDef categoryDef = WordleData.Category(language, category);
        if (categoryDot != null) categoryDot.color = WordleData.CategoryColor(category);
        if (categoryNameLabel != null && categoryDef != null) categoryNameLabel.text = categoryDef.name;
        if (categoryLevelLabel != null && categoryDef != null)
        {
            categoryLevelLabel.text = "Level " + (level + 1) + " of " + categoryDef.levels.Count;
        }
    }
}
