using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LearnTopicButton
/// Put on the "Learn" button of a topic card on a game's start screen (Wordle - Start, Crossword - Start, WordSearch - Start). Pressing it opens
/// the Learn screen for that topic: it takes the language the game's session holds, and the topic (category index) set on this component.
/// Needs a Button on the same object. The Learn buttons are added to the start scenes by LearnSceneBuilder (Tools > Learn).
/// </summary>
[RequireComponent(typeof(Button))]
public class LearnTopicButton : MonoBehaviour
{
    public LearnGame game;
    public int category;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Open);
    }

    /// <summary>Opens the Learn screen for this topic.</summary>
    public void Open()
    {
        int language;
        switch (game)
        {
            case LearnGame.Crossword: language = CrosswordSession.Language; break;
            case LearnGame.WordSearch: language = WordSearchSession.Language; break;
            default: language = WordleSession.Language; break;
        }
        LearnSession.Open(game, language, category);
    }
}
