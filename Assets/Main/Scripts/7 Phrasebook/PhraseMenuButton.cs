using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PhraseMenuButton
/// Put on the Phrasebook card of the main menu (1 Main_menu), next to a Button on the same object. Pressing it opens the Phrasebook topics screen
/// ("Phrasebook - Topics"), in the language the player picked there last time (Cebuano the first time). The language is chosen on the topics
/// screen itself, like the tabs of the Library.
/// </summary>
[RequireComponent(typeof(Button))]
public class PhraseMenuButton : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Open);
    }

    /// <summary>Opens the Phrasebook.</summary>
    public void Open()
    {
        PhraseSession.Language = PhraseSession.SavedLanguage();
        SceneManager.LoadScene(PhraseSession.TopicsScene);
    }
}
